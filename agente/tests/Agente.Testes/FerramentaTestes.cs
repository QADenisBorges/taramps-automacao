using System.Text.Json.Nodes;
using Agente.Porta;
using BgsControle.Nucleo.Porta;

namespace Agente.Testes;

/// <summary>
/// A ferramenta da porta (história 14a), como processo, contra a <see cref="PortaComandos"/> do núcleo sobre o simulador:
/// as linhas Parear, Ler e Recusa da matriz, mais o formato da entrada.
/// </summary>
public sealed class FerramentaTestes
{
    private static string Parear(string endereco, string? codigo) =>
        new JsonObject { ["acao"] = "parear", ["endereco"] = endereco, ["codigo"] = codigo }.ToJsonString();

    [Fact]
    public async Task Parear_com_o_codigo_da_tela_guarda_o_endereco_e_a_chave_e_a_saida_nao_mostra_a_chave()
    {
        await using var bancada = await Bancada.ProntaAsync();
        using var local = new LocalTemporario();

        var (codigo, saida, json) = await PortaCli.RodarAsync(Parear(bancada.Endereco, bancada.Pareamento.Codigo), local.Local);

        Assert.Equal(0, codigo);
        Assert.True(json["ok"]!.GetValue<bool>(), saida);
        Assert.True(bancada.Pareamento.Pareado);
        var guardado = PareamentoLocal.Ler(local.Local);
        Assert.NotNull(guardado);
        Assert.Equal(bancada.Endereco, guardado.Endereco);
        Assert.False(string.IsNullOrWhiteSpace(guardado.Chave));
        Assert.DoesNotContain(guardado.Chave, saida);
        Assert.DoesNotContain(guardado.Chave, guardado.ToString());
        // Só o pareamento fica guardado.
        Assert.Equal([PareamentoLocal.Arquivo], Directory.GetFiles(local.Local).Select(Path.GetFileName));

        // Com o pareamento, um GET passa e a chave não aparece na saída.
        var (codigoLer, saidaLer, _) = await PortaCli.RodarAsync("""{"acao":"ler","alvo":"/estado"}""", local.Local);
        Assert.Equal(0, codigoLer);
        Assert.DoesNotContain(guardado.Chave, saidaLer);
    }

    [Fact]
    public async Task Parear_com_o_codigo_errado_devolve_o_motivo_da_porta_e_nada_fica_guardado()
    {
        await using var bancada = await Bancada.ProntaAsync();
        using var local = new LocalTemporario();
        var errado = bancada.Pareamento.Codigo == "000000" ? "000001" : "000000";

        var (codigo, _, json) = await PortaCli.RodarAsync(Parear(bancada.Endereco, errado), local.Local);

        Assert.Equal(1, codigo);
        Assert.False(json["ok"]!.GetValue<bool>());
        Assert.Equal(401, json["status"]!.GetValue<int>());
        Assert.Equal(Pareamento.MsgCodigoErrado, json["motivo"]!.GetValue<string>());
        Assert.Null(PareamentoLocal.Ler(local.Local));
        Assert.False(bancada.Pareamento.Pareado);
    }

    [Fact]
    public async Task Ler_o_estado_devolve_o_corpo_da_porta_e_o_atalho_ler_e_o_mesmo_GET()
    {
        await using var bancada = await Bancada.ProntaAsync();
        using var local = new LocalTemporario();
        Assert.Equal(0, (await PortaCli.RodarAsync(Parear(bancada.Endereco, bancada.Pareamento.Codigo), local.Local)).Codigo);

        var (codigo, saida, json) = await PortaCli.RodarAsync("""{"acao":"ler","alvo":"/estado"}""", local.Local);

        Assert.Equal(0, codigo);
        Assert.True(json["ok"]!.GetValue<bool>());
        Assert.Equal(200, json["status"]!.GetValue<int>());
        var corpo = json["corpo"]!.AsObject();
        Assert.True(corpo["conexao"]!["conectado"]!.GetValue<bool>());
        Assert.NotNull(corpo["setup"]!["nome"]);
        Assert.NotEmpty(corpo["parametros"]!.AsArray());
        Assert.DoesNotContain(bancada.Simulador.Senha, saida); // a senha do processador nunca passa pela porta

        var (_, _, comoPedir) = await PortaCli.RodarAsync("""{"acao":"pedir","metodo":"GET","alvo":"/estado"}""", local.Local);
        Assert.Equal(200, comoPedir["status"]!.GetValue<int>());
        Assert.Equal(corpo["setup"]!.ToJsonString(), comoPedir["corpo"]!["setup"]!.ToJsonString());
    }

    [Fact]
    public async Task Ler_sem_pareamento_devolve_o_motivo_e_codigo_1()
    {
        using var local = new LocalTemporario();

        var (codigo, saida, json) = await PortaCli.RodarAsync("""{"acao":"ler","alvo":"/estado"}""", local.Local);

        Assert.Equal(1, codigo);
        Assert.Equal("""{"ok":false,"motivo":"Sem pareamento: rode porta parear."}""", saida);
        Assert.False(json["ok"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Ajuste_fora_da_cerca_e_recusado_com_409_o_limite_e_codigo_1_e_nada_sai()
    {
        await using var bancada = await Bancada.ProntaAsync();
        using var local = new LocalTemporario();
        Assert.Equal(0, (await PortaCli.RodarAsync(Parear(bancada.Endereco, bancada.Pareamento.Codigo), local.Local)).Codigo);
        var antes = bancada.Simulador.Valor("071", 0);
        var escritas = bancada.Simulador.Escritas.Count;

        // O limitador da saída 1 desligado: a cerca não deixa.
        var (codigo, _, json) = await PortaCli.RodarAsync(
            """{"acao":"pedir","metodo":"POST","alvo":"/ajuste","corpo":{"codigo":"071","indice":0,"valor":0.5}}""", local.Local);

        Assert.Equal(1, codigo);
        Assert.False(json["ok"]!.GetValue<bool>());
        Assert.Equal(409, json["status"]!.GetValue<int>());
        Assert.StartsWith("Limite: ", json["motivo"]!.GetValue<string>());
        Assert.Null(json["corpo"]);
        Assert.Equal(antes, bancada.Simulador.Valor("071", 0));
        Assert.DoesNotContain(bancada.Simulador.Escritas.Skip(escritas), e => e.StartsWith("*071*", StringComparison.Ordinal));
        bancada.NadaDeMemoriaDoAparelho();
    }

    [Theory]
    [InlineData("", "Entrada vazia")]
    [InlineData("não é json", "não é um JSON válido")]
    [InlineData("""{"acao":"voar"}""", "A \"acao\" é ler, pedir ou parear.")]
    [InlineData("""{"acao":"ler","alvo":"//outra-maquina/estado"}""", "O \"alvo\" é um caminho da porta")]
    [InlineData("""{"acao":"ler","alvo":"http://outra-maquina/estado"}""", "O \"alvo\" é um caminho da porta")]
    [InlineData("""{"acao":"ler","metodo":"POST","alvo":"/ajuste"}""", "O \"ler\" é sempre um GET")]
    [InlineData("""{"acao":"pedir","metodo":"PUT","alvo":"/ajuste"}""", "O \"metodo\" é GET, POST ou DELETE.")]
    [InlineData("""{"acao":"pedir","metodo":"POST","alvo":"/ajuste","corpo":[1]}""", "O \"corpo\" é um objeto JSON.")]
    [InlineData("""{"acao":"parear","endereco":"sem-porta","codigo":"123456"}""", "O \"endereco\" é o ip:porta")]
    [InlineData("""{"acao":"parear","endereco":"maquina:8787/caminho","codigo":"123456"}""", "O \"endereco\" é o ip:porta")]
    [InlineData("""{"acao":"parear","endereco":"maquina:8787"}""", "Falta o \"codigo\"")]
    public async Task Entrada_mal_formada_sai_como_recusa_com_o_motivo(string entrada, string motivo)
    {
        using var local = new LocalTemporario();

        var r = await FerramentaPorta.ExecutarAsync(entrada, local.Local);

        Assert.False(r.Ok);
        Assert.Null(r.Status);
        Assert.Contains(motivo, r.Motivo);
        Assert.Equal(1, r.CodigoSaida);
    }

    [Fact]
    public async Task Porta_sem_resposta_da_o_motivo_sem_status_e_sem_a_chave()
    {
        using var local = new LocalTemporario();
        PareamentoLocal.Gravar(local.Local, new ClientePareado("127.0.0.1:1", "chave-de-teste-que-nao-aparece"));

        var r = await FerramentaPorta.ExecutarAsync("""{"acao":"ler","alvo":"/estado"}""", local.Local);

        Assert.False(r.Ok);
        Assert.Null(r.Status);
        Assert.Equal(ClientePorta.MsgSemResposta("127.0.0.1:1"), r.Motivo);
        Assert.DoesNotContain("chave-de-teste-que-nao-aparece", r.ParaJson());
    }

    private const string SemPareamento = """{"ok":false,"motivo":"Sem pareamento: rode porta parear."}""";

    [Fact]
    public async Task A_entrada_pelo_stdin_da_uma_linha_so_e_o_codigo_de_saida()
    {
        using var local = new LocalTemporario();

        var (codigo, saida) = await PortaCli.RodarCruAsync([], """{"acao":"ler","alvo":"/estado"}""", local.Local);

        Assert.Equal(1, codigo);
        Assert.Equal(SemPareamento + Environment.NewLine, saida);
    }

    [Fact]
    public async Task A_entrada_como_argumento_solto_da_uma_linha_so_e_o_codigo_de_saida()
    {
        using var local = new LocalTemporario();

        var (codigo, saida) = await PortaCli.RodarCruAsync(["""{"acao":"ler","alvo":"/estado"}"""], null, local.Local);

        Assert.Equal(1, codigo);
        Assert.Equal(SemPareamento + Environment.NewLine, saida);
    }

    [Fact]
    public async Task Um_porta_json_com_endereco_que_desvia_a_chave_conta_como_sem_pareamento()
    {
        using var local = new LocalTemporario();
        File.WriteAllText(PareamentoLocal.Caminho(local.Local), """{"endereco":"127.0.0.1:8787/x?y#","chave":"k"}""");

        var (codigo, saida) = await PortaCli.RodarCruAsync(["--json", """{"acao":"ler","alvo":"/estado"}"""], null, local.Local);

        Assert.Equal(1, codigo);
        Assert.Equal(SemPareamento + Environment.NewLine, saida);
        File.WriteAllText(PareamentoLocal.Caminho(local.Local), """{"endereco":"127.0.0.1:8787","chave":12}""");
        Assert.Null(PareamentoLocal.Ler(local.Local)); // a chave que não é texto também
    }

    [Fact]
    public async Task Uma_pasta_local_invalida_ainda_sai_numa_linha_com_o_codigo_1()
    {
        using var local = new LocalTemporario();
        var arquivo = Path.Combine(local.Raiz, "um-arquivo");
        File.WriteAllText(arquivo, "");

        // A "pasta" local fica dentro de um arquivo: ainda assim, uma linha de JSON e o código 1.
        var (codigo, saida) = await PortaCli.RodarCruAsync(
            ["--local", Path.Combine(arquivo, "dentro"), "--json", """{"acao":"ler","alvo":"/estado"}"""], null, local.Local);

        Assert.Equal(1, codigo);
        var linhas = saida.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Single(linhas);
        Assert.False(JsonNode.Parse(linhas[0])!["ok"]!.GetValue<bool>());
    }

    [Theory]
    [InlineData("""{"acao":"parear","endereco":"maquina:8787","codigo":12345}""", "O \"codigo\" vai entre aspas")]
    [InlineData("""{"acao":"parear","endereco":"maquina:8787?x=1","codigo":"123456"}""", "O \"endereco\" é o ip:porta")]
    [InlineData("""{"acao":"parear","endereco":"maquina:8787#x","codigo":"123456"}""", "O \"endereco\" é o ip:porta")]
    public async Task Codigo_numerico_e_endereco_com_consulta_sao_recusados(string entrada, string motivo)
    {
        using var local = new LocalTemporario();

        var r = await FerramentaPorta.ExecutarAsync(entrada, local.Local);

        Assert.False(r.Ok);
        Assert.Contains(motivo, r.Motivo);
    }

    [Fact]
    public async Task Uma_porta_que_aceita_e_nunca_responde_da_o_motivo_do_prazo()
    {
        var escuta = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        escuta.Start();
        try
        {
            var numero = ((System.Net.IPEndPoint)escuta.LocalEndpoint).Port;
            var aceito = escuta.AcceptTcpClientAsync(); // aceita e fica calado
            using var cliente = new ClientePorta(prazo: TimeSpan.FromSeconds(1));

            var r = await cliente.PedirAsync(new ClientePareado($"127.0.0.1:{numero}", "k"), "GET", "/estado", null);

            Assert.False(r.Ok);
            Assert.Null(r.Status);
            Assert.Equal(ClientePorta.MsgPrazo(TimeSpan.FromSeconds(1)), r.Motivo);
            (await aceito).Dispose();
        }
        finally
        {
            escuta.Stop();
        }
    }

    [Fact]
    public void O_prazo_de_um_pedido_e_de_70_s_para_caber_a_folha_de_risco_de_60_s()
    {
        Assert.Equal(TimeSpan.FromSeconds(70), ClientePorta.PrazoPadrao);
        Assert.True(ClientePorta.PrazoPadrao > TimeSpan.FromSeconds(60));
    }
}
