using System.Text;
using System.Text.Json;
using BgsControle.Nucleo;
using BgsControle.Nucleo.Leitura;
using BgsControle.Nucleo.Registro;
using BgsControle.Simulador;

namespace BgsControle.Telas.Testes;

/// <summary>O processador simulado: formato real, escrita com eco, memórias, senha e falhas injetáveis.</summary>
public sealed class SimuladorTestes
{
    private const string ArquivoReal = "Dados/leitura-2026-10-18_2145.json";

    private static readonly TemposSessao Tempos = new(
        Scan: TimeSpan.FromSeconds(1),
        TentativaConexao: TimeSpan.FromSeconds(1),
        PausaEntreTentativas: TimeSpan.FromMilliseconds(1),
        RespostaSenha: TimeSpan.FromSeconds(2),
        PrazoSenha: TimeSpan.FromSeconds(60));

    private static readonly TemposLeitura LeituraRapida = new(TimeSpan.Zero, TimeSpan.FromMilliseconds(100));

    private readonly RelogioFalso _relogio = new();
    private readonly ProcessadorSimulado _simulador;
    private readonly List<string> _recebido = [];
    private int _fragmentos;

    public SimuladorTestes()
    {
        _simulador = new ProcessadorSimulado(_relogio);
        _simulador.Notificacao += dados =>
        {
            lock (_recebido)
            {
                _recebido.Add(Encoding.ASCII.GetString(dados));
                _fragmentos++;
            }
        };
    }

    /// <summary>Tudo o que chegou pela notificação, junto.</summary>
    private string Recebido
    {
        get
        {
            lock (_recebido)
                return string.Concat(_recebido);
        }
    }

    private void Limpar()
    {
        lock (_recebido)
            _recebido.Clear();
    }

    private Task Escrever(string quadro) => _simulador.EscreverAsync(Encoding.ASCII.GetBytes(quadro), default);

    private async Task ConectarEAutenticar()
    {
        await _simulador.ConectarAsync(_simulador.Endereco!, default);
        Assert.Equal("#000#PRO2.4MIR003#\r\n", await EnviarSenha("AB12"));
        Assert.True(_simulador.Autenticado);
    }

    /// <summary>A senha: nada antes de ~23 ms, e então a resposta.</summary>
    private Task<string> EnviarSenha(string senha) => EscreverEEsperar($"*000*{senha}*", ProcessadorSimulado.AtrasoSenhaReal);

    /// <summary>Uma leitura: nada antes de ~25 ms, e então a resposta.</summary>
    private Task<string> Perguntar(string consulta) => EscreverEEsperar(consulta, ProcessadorSimulado.AtrasoLeituraReal);

    /// <summary>Uma escrita direta: nada antes de ~85 ms, e então o eco.</summary>
    private Task<string> EscreverEEsperarEco(string quadro) => EscreverEEsperar(quadro, ProcessadorSimulado.AtrasoEcoReal);

    /// <summary>O ack do 240 e do 241: ~1,17 s (medido no 240 em 20/10).</summary>
    private Task<string> EscreverEEsperarAck(string quadro) => EscreverEEsperar(quadro, ProcessadorSimulado.AtrasoAckSalvarReal);

    private async Task<string> EscreverEEsperar(string quadro, TimeSpan atraso)
    {
        await Escrever(quadro);
        _relogio.Avancar(atraso - TimeSpan.FromMilliseconds(1));
        await Task.Delay(30);
        Assert.Equal("", Recebido);
        _relogio.Avancar(TimeSpan.FromMilliseconds(1));
        await Apoio.Esperar(() => Recebido.Length > 0);
        var eco = Recebido;
        Limpar();
        return eco;
    }

    /// <summary>
    /// A sessão e o leitor de verdade. Eles esperam em tempo real e o relógio falso não anda
    /// enquanto leem: a senha e as leituras respondem na hora (o ritmo real tem o seu teste).
    /// </summary>
    private (Sessao Sessao, LeitorCompleto Leitor) SessaoComLeitor()
    {
        _simulador.AtrasoSenha = TimeSpan.Zero;
        _simulador.AtrasoLeitura = TimeSpan.Zero;
        var log = new LogBle();
        var sessao = new Sessao(_simulador, new CofreMemoria { Senha = "AB12" }, log, Tempos);
        return (sessao, new LeitorCompleto(sessao, log, LeituraRapida));
    }

    private async Task<Retrato> LerTudo()
    {
        var (sessao, leitor) = SessaoComLeitor();
        await sessao.ConectarAsync();
        Assert.Equal(EstadoSessao.Conectado, sessao.Estado);
        return await leitor.LerAsync();
    }

    private static List<(string Codigo, int? Canal, string Cru)> LeiturasReais()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, ArquivoReal)));
        return doc.RootElement.GetProperty("leituras").EnumerateArray()
            .Select(l => (
                l.GetProperty("codigo").GetString()!,
                l.GetProperty("canal").ValueKind == JsonValueKind.Null ? (int?)null : l.GetProperty("canal").GetInt32(),
                l.GetProperty("cru").GetString()!))
            .ToList();
    }

    // ----------------------------------------------------------------- leitura no formato real

    [Fact]
    public async Task A_leitura_completa_devolve_as_respostas_cruas_reais_84_de_84()
    {
        var r = await LerTudo();

        Assert.True(r.Completo);
        Assert.Equal("84 de 84", r.PlacarMarco1.Texto);
        Assert.Equal("11 de 12", r.PlacarExtras.Texto);
        Assert.Equal(["ERRO 0"], r.Orfaos);
        var reais = LeiturasReais();
        Assert.Equal(84, reais.Count);
        foreach (var (codigo, canal, cru) in reais)
            Assert.Equal(cru, r.Item(codigo, canal)?.Cru);

        Assert.Null(r.Item("231")); // o app não lê os nomes das memórias (29/10/2026); o simulador ainda os tem
        Assert.Equal("SPL", _simulador.Valor("231"));
        Assert.Equal("#200#-25.0#", r.Item("200")!.Cru);
        Assert.Equal("#252#1#", r.Item("252")!.Cru);
        Assert.Equal("005", Assert.Single(r.Falhas).Codigo);
    }

    [Fact]
    public async Task Leituras_respondem_com_crlf_e_a_banda_e_os_nomes_sem_separadores_nas_pontas()
    {
        await ConectarEAutenticar();

        const string esperado = "#080#+0.0#\r\n#081#0#+10.0#\r\n020#6#-1.0\r\n231#SPL\r\n232#MUSICA ORIG\r\nERRO 0\r\n";

        foreach (var consulta in new[] { "#080#", "#081#0#", "020#6", "#231#", "#232#", "#005#" })
            await Escrever(consulta);
        _relogio.Avancar(ProcessadorSimulado.AtrasoLeituraReal); // as 6 vencem no mesmo Avancar
        await Apoio.Esperar(() => Recebido.Length >= esperado.Length);

        Assert.Equal(esperado, Recebido);
    }

    [Fact]
    public async Task A_senha_e_a_leitura_respondem_no_atraso_medido_pelo_relogio_do_simulador()
    {
        await _simulador.ConectarAsync(_simulador.Endereco!, default);

        // EscreverEEsperar confere que nada chega 1 ms antes do atraso.
        Assert.Equal("#000#PRO2.4MIR003#\r\n", await EscreverEEsperar("*000*AB12*", TimeSpan.FromMilliseconds(23)));
        Assert.Equal("#080#+0.0#\r\n", await EscreverEEsperar("#080#", TimeSpan.FromMilliseconds(25)));
        Assert.Equal("ERRO 0\r\n", await EscreverEEsperar("#005#", TimeSpan.FromMilliseconds(25)));
    }

    [Fact]
    public async Task Uma_leitura_depois_de_uma_escrita_sai_na_fila_depois_do_eco()
    {
        await ConectarEAutenticar();

        await Escrever("*081*0*-3.0*");
        await Escrever("#080#");
        _relogio.Avancar(ProcessadorSimulado.AtrasoLeituraReal);
        await Task.Delay(30);
        Assert.Equal("", Recebido); // a leitura venceu, mas espera o eco que está na frente
        _relogio.Avancar(ProcessadorSimulado.AtrasoEcoReal - ProcessadorSimulado.AtrasoLeituraReal);
        await Apoio.Esperar(() => Recebido.Length >= "#081#0#-3.0#\r\n#080#+0.0#\r\n".Length);

        Assert.Equal("#081#0#-3.0#\r\n#080#+0.0#\r\n", Recebido);
    }

    [Fact]
    public async Task A_queda_joga_fora_as_respostas_que_ainda_nao_sairam()
    {
        await ConectarEAutenticar();
        await Escrever("#080#");
        await Escrever("*081*0*-3.0*");

        _simulador.Derrubar();
        await _simulador.ConectarAsync(_simulador.Endereco!, default);
        _relogio.Avancar(TimeSpan.FromSeconds(1));
        await Task.Delay(30);

        Assert.Equal("", Recebido);
        Assert.Equal("-3.0", _simulador.Valor("081", 0)); // a escrita foi aplicada; só o eco se perdeu
    }

    [Fact]
    public async Task Antes_da_senha_a_leitura_recebe_ERRO_0_e_a_escrita_nada()
    {
        // Medido em 24/10 (história 12a): a leitura recebe ERRO 0; a escrita não foi medida.
        await _simulador.ConectarAsync(_simulador.Endereco!, default);
        Assert.Equal("ERRO 0\r\n", await Perguntar("#080#"));
        Assert.Empty(_simulador.Recusadas);

        await Escrever("*081*0*-3.0*");
        _relogio.Avancar(TimeSpan.FromSeconds(1));
        await Task.Delay(30);

        Assert.Equal("", Recebido);
        Assert.Equal("+10.0", _simulador.Valor("081", 0));
        Assert.Contains(_simulador.Recusadas, r => r.Quadro == "*081*0*-3.0*");
    }

    // ----------------------------------------------------------------- senha

    [Fact]
    public async Task Firmware_e_senha_sao_configuraveis()
    {
        _simulador.Senha = "Z9Z9";
        _simulador.Firmware = "R004";
        await _simulador.ConectarAsync(_simulador.Endereco!, default);

        Assert.Equal("#000#ERRO#\r\n", await EnviarSenha("AB12"));
        Assert.Equal("#000#PRO2.4MIR004#\r\n", await EnviarSenha("Z9Z9"));
    }

    [Fact]
    public async Task Senha_errada_depois_da_certa_e_respondida_como_a_certa_e_a_sessao_continua()
    {
        // Medido em 24/10 (história 12a): já autenticado, a senha errada recebe a resposta da certa.
        await ConectarEAutenticar();

        Assert.Equal("#000#PRO2.4MIR003#\r\n", await EnviarSenha("ZZZZ"));
        Assert.True(_simulador.Autenticado);
        Assert.Equal("#080#+0.0#\r\n", await Perguntar("#080#"));
        _relogio.Avancar(TimeSpan.FromSeconds(40));
        await Task.Delay(30);

        Assert.True(_simulador.Conectado);
    }

    [Fact]
    public async Task Depois_de_um_ERRO_a_conexao_cai_sozinha_em_29_s()
    {
        var quedas = 0;
        _simulador.Caiu += () => Interlocked.Increment(ref quedas);
        await _simulador.ConectarAsync(_simulador.Endereco!, default);

        Assert.Equal("#000#ERRO#\r\n", await EnviarSenha("ZZZZ")); // o prazo conta desde a escrita

        _relogio.Avancar(TimeSpan.FromSeconds(28.9));
        Assert.True(_simulador.Conectado);
        Assert.Equal(0, quedas);

        _relogio.Avancar(TimeSpan.FromSeconds(0.1));
        Assert.False(_simulador.Conectado);
        Assert.Equal(1, quedas);
        Assert.Equal(1, _simulador.Quedas);
    }

    [Fact]
    public async Task Senha_certa_antes_dos_29_s_cancela_a_queda()
    {
        var quedas = 0;
        _simulador.Caiu += () => Interlocked.Increment(ref quedas);
        await _simulador.ConectarAsync(_simulador.Endereco!, default);
        await Escrever("*000*ZZZZ*");

        _relogio.Avancar(TimeSpan.FromSeconds(10));
        await Escrever("*000*AB12*");
        _relogio.Avancar(TimeSpan.FromSeconds(60));

        Assert.True(_simulador.Conectado);
        Assert.True(_simulador.Autenticado);
        Assert.Equal(0, quedas);
        Assert.Equal(0, _simulador.Quedas);
    }

    [Fact]
    public async Task Desconectar_antes_dos_29_s_cancela_a_queda()
    {
        var quedas = 0;
        _simulador.Caiu += () => quedas++;
        await _simulador.ConectarAsync(_simulador.Endereco!, default);
        await Escrever("*000*ZZZZ*");

        await _simulador.DesconectarAsync();
        _relogio.Avancar(TimeSpan.FromSeconds(60));

        Assert.Equal(0, quedas);
    }

    // ----------------------------------------------------------------- escrita

    [Fact]
    public async Task Escrita_valida_muda_o_estado_e_o_eco_chega_em_85_ms_sem_OK()
    {
        await ConectarEAutenticar();

        var eco = await EscreverEEsperarEco("*081*0*-3.0*");

        Assert.Equal("#081#0#-3.0#\r\n", eco);
        Assert.Equal("-3.0", _simulador.Valor("081", 0));
        Assert.DoesNotContain("OK", string.Concat(_simulador.Notificacoes));

        Assert.Equal("#081#0#-3.0#\r\n", await Perguntar("#081#0#"));
    }

    [Fact]
    public async Task Ecos_que_vencem_juntos_chegam_na_ordem_das_escritas()
    {
        await ConectarEAutenticar();
        var escritas = Enumerable.Range(0, 4).Select(c => $"*081*{c}*-{c + 1}.0*").ToArray();
        var esperado = string.Concat(Enumerable.Range(0, 4).Select(c => $"#081#{c}#-{c + 1}.0#\r\n"));

        for (var vez = 0; vez < 25; vez++)
        {
            foreach (var e in escritas)
                await Escrever(e);
            _relogio.Avancar(ProcessadorSimulado.AtrasoEcoReal); // os 4 vencem no mesmo Avancar
            await Apoio.Esperar(() => Recebido.Length >= esperado.Length);

            Assert.Equal(esperado, Recebido);
            Limpar();
        }
    }

    [Theory]
    [InlineData("*080*-6.5*", "#080#-6.5#")]
    [InlineData("*091*2*3.0*", "#091#2#+3.0#")]
    [InlineData("*092*1*2.5*", "#092#1#+2.5#")]
    [InlineData("020*3*-1.5", "020#3#-1.5")]
    [InlineData("*010*0*a+b*", "#010#0#a+b#")]
    [InlineData("*060*3*180*", "#060#3#180#")]
    [InlineData("*042*1*11*", "#042#1#11#")]
    [InlineData("*050*2*42.5*", "#050#2#42.5#")]
    [InlineData("*071*1*0.5*", "#071#1#+0.5#")]
    [InlineData("*073*0*720*", "#073#0#720#")]
    [InlineData("*100*3*1*", "#100#3#1#")]
    [InlineData("*220*255*", "#220#255#")]
    [InlineData("*202*1*", "#202#1#")]
    [InlineData("*200*-20.0*", "#200#-20.0#")]
    [InlineData("*213*2*", "#213#2#")]
    [InlineData("*251*0*", "#251#0#")]
    [InlineData("*252*0*", "#252#0#")]
    public async Task Cada_escrita_do_catalogo_volta_com_o_eco_no_formato_de_leitura(string escrita, string eco)
    {
        await ConectarEAutenticar();

        Assert.Equal(eco + "\r\n", await EscreverEEsperarEco(escrita));
    }

    [Theory]
    [InlineData("*081*9*-3.0*")] // canal 9
    [InlineData("*081*0*-46.0*")] // fora da faixa
    [InlineData("*081*0*-3*")] // fora do formato do codificador
    [InlineData("*081*-3.0*")] // sem canal
    [InlineData("*080*0*-3.0*")] // global com canal
    [InlineData("*005*120*")] // só leitura
    [InlineData("*150*1*")] // só leitura
    [InlineData("020*15*1.0")] // banda 15
    [InlineData("*060*0*90*")] // fase só 0 ou 180
    [InlineData("*010*0*c*")] // roteamento
    [InlineData("*999*1*")] // código desconhecido
    public async Task Escrita_invalida_nao_aplica_nao_responde_e_fica_registrada(string escrita)
    {
        await ConectarEAutenticar();
        var antes = new Dictionary<(string, int?), string?>(
            Catalogo.ConsultasMarco1.Select(c => KeyValuePair.Create<(string, int?), string?>(
                (c.Parametro.Codigo, c.Indice), _simulador.Valor(c.Parametro.Codigo, c.Indice))));

        await Escrever(escrita);
        _relogio.Avancar(TimeSpan.FromSeconds(1));
        await Task.Delay(30);

        Assert.Equal("", Recebido);
        Assert.Contains(escrita, _simulador.Escritas);
        Assert.Contains(_simulador.Recusadas, r => r.Quadro == escrita);
        foreach (var ((codigo, indice), valor) in antes)
            Assert.Equal(valor, _simulador.Valor(codigo, indice));
    }

    // ----------------------------------------------------------------- memórias e comandos do aparelho

    [Fact]
    public async Task Salvar_e_carregar_memoria()
    {
        await ConectarEAutenticar();

        await EscreverEEsperarEco("*081*0*-3.0*");
        Assert.Equal("#240#1#\r\n", await EscreverEEsperarAck("*240*1*"));
        Assert.Equal("-3.0", _simulador.Memoria(1)[("081", 0)]);

        await EscreverEEsperarEco("*081*0*5.0*");
        Assert.Equal("+5.0", _simulador.Valor("081", 0));

        Assert.Equal("#241#1#\r\n", await EscreverEEsperarAck("*241*1*"));
        Assert.Equal("-3.0", _simulador.Valor("081", 0));
        Assert.Equal("+10.0", _simulador.Memoria(0)[("081", 0)]); // as outras ficam como estavam
    }

    [Fact]
    public async Task A_memoria_guarda_o_ganho_e_a_frequencia_do_tom_mas_nao_os_nomes()
    {
        // Medido em 24/10 (história 12a): carregar a SPL trocou o 200 e o 201, e os nomes 23N
        // ficaram. O 202, o 210–213, o 251 e o 252 não deu para medir; o teste não diz nada deles.
        await ConectarEAutenticar();
        await EscreverEEsperarEco("*200*-40.0*");
        await EscreverEEsperarEco("*201*51*");
        Assert.Equal("#240#1#\r\n", await EscreverEEsperarAck("*240*1*"));

        await EscreverEEsperarEco("*200*-50.0*");
        await EscreverEEsperarEco("*201*1000*");
        _simulador.Definir("232", null, "OUTRO NOME");
        Assert.Equal("#241#1#\r\n", await EscreverEEsperarAck("*241*1*"));

        Assert.Equal("-40.0", _simulador.Valor("200"));
        Assert.Equal("51", _simulador.Valor("201"));
        Assert.Equal("OUTRO NOME", _simulador.Valor("232"));
    }

    [Fact]
    public async Task Nomear_memoria()
    {
        await ConectarEAutenticar();

        // Medido em 20/10: o nome é aceito sem eco; só a releitura responde.
        await Escrever("*231*CINEMA*");
        _relogio.Avancar(TimeSpan.FromSeconds(2));
        await Task.Delay(30);
        Assert.Equal("", Recebido);
        Assert.Equal("CINEMA", _simulador.Valor("231"));
        Assert.Equal("231#CINEMA\r\n", await Perguntar("#231#"));
    }

    [Fact]
    public async Task Reset_de_audio_volta_o_marco_1_a_fabrica_e_mantem_memorias_e_nomes()
    {
        await ConectarEAutenticar();

        Assert.Equal("#242#\r\n", await EscreverEEsperarEco("*242*"));

        foreach (var (chave, valor) in ProcessadorSimulado.Fabrica)
            Assert.Equal(valor, _simulador.Valor(chave.Codigo, chave.Indice));
        Assert.Equal("+10.0", _simulador.Memoria(0)[("081", 0)]);
        Assert.Equal("SPL", _simulador.Valor("231"));
    }

    [Fact]
    public async Task Reset_de_fabrica_apaga_memorias_nomes_e_volta_a_senha()
    {
        await ConectarEAutenticar();

        Assert.Equal("#004#\r\n", await EscreverEEsperarEco("*004*"));

        Assert.Equal(_simulador.SenhaDeFabrica, _simulador.Senha);
        Assert.Equal("+0.0", _simulador.Valor("081", 0));
        Assert.Equal("+0.0", _simulador.Memoria(2)[("081", 0)]);
        Assert.Equal("", _simulador.Valor("231"));
    }

    [Fact]
    public async Task Troca_de_senha()
    {
        await ConectarEAutenticar();

        Assert.Equal("#001#ERRO#\r\n", await EscreverEEsperarEco("*001*XXXX*CD34*"));
        Assert.Equal("AB12", _simulador.Senha);

        Assert.Equal("#001#OK#\r\n", await EscreverEEsperarEco("*001*AB12*CD34*"));
        Assert.Equal("CD34", _simulador.Senha);
    }

    [Fact]
    public async Task Clip_espontaneo()
    {
        await ConectarEAutenticar();

        _simulador.EmitirClip(0b0101);

        Assert.Equal("#150#5#\r\n", Recebido);
        Assert.Equal("5", _simulador.Valor("150"));
    }

    [Fact]
    public async Task Voltimetro_e_clip_da_entrada_espontaneos_e_a_consulta_do_005_continua_ERRO()
    {
        await ConectarEAutenticar();

        _simulador.EmitirVoltimetro(124);
        Assert.Equal("#005#124#\r\n", Recebido);
        Limpar();

        _simulador.EmitirClipEntrada(true);
        Assert.Equal("#111#1#\r\n", Recebido);
        Assert.Equal("1", _simulador.Valor("111"));
        Limpar();

        Assert.Equal(ProcessadorSimulado.RespostaVoltimetro + "\r\n", await Perguntar("#005#"));
    }

    [Fact]
    public async Task Nome_do_produto_responde_com_o_ack_e_nao_muda_a_resposta_da_senha()
    {
        await ConectarEAutenticar();

        Assert.Equal("#002#SALA 1#\r\n", await EscreverEEsperarEco("*002*SALA 1*"));
        Assert.Equal("SALA 1", _simulador.NomeProduto);

        await Escrever("*002*sala#1*"); // fora da regra: sem resposta, nada muda
        await Escrever("*002*sala 1*");
        _relogio.Avancar(TimeSpan.FromSeconds(1));
        await Task.Delay(30);
        Assert.Equal("", Recebido);
        Assert.Equal("SALA 1", _simulador.NomeProduto);
        Assert.Contains(_simulador.Recusadas, r => r.Quadro == "*002*sala 1*");

        // Suposição NOME-PRODUTO: a resposta 000 continua começando com PRO2.4MI.
        Assert.Equal("#000#PRO2.4MIR003#\r\n", await EnviarSenha("AB12"));
    }

    [Fact]
    public async Task O_gerador_guarda_a_linha_do_tempo_do_202()
    {
        await ConectarEAutenticar();
        var t0 = _relogio.GetUtcNow();

        Assert.Equal("#202#1#\r\n", await EscreverEEsperarEco("*202*1*"));
        Assert.True(_simulador.GeradorLigado);
        _relogio.Avancar(TimeSpan.FromSeconds(25));
        Assert.Equal("#202#0#\r\n", await EscreverEEsperarEco("*202*0*"));
        Assert.False(_simulador.GeradorLigado);
        await Escrever("*202*2*"); // recusada: não entra na linha do tempo
        _simulador.Definir("202", null, "1"); // direto no estado, sem escrita: também não

        Assert.Equal(
            [new MarcaGerador(t0, true), new MarcaGerador(t0 + ProcessadorSimulado.AtrasoEcoReal + TimeSpan.FromSeconds(25), false)],
            _simulador.LinhaDoTempoGerador);
    }

    // ----------------------------------------------------------------- notificações de até 20 bytes

    /// <summary>O tamanho de cada notificação que chegou, na ordem.</summary>
    private int[] Tamanhos()
    {
        lock (_recebido)
            return _recebido.Select(f => f.Length).ToArray();
    }

    [Fact]
    public async Task Por_padrao_cada_notificacao_leva_ate_20_bytes()
    {
        await _simulador.ConectarAsync(_simulador.Endereco!, default);
        Limpar();
        Assert.Equal("#000#PRO2.4MIR003#\r\n", await EnviarSenha("AB12"));
        Assert.Equal(1, _fragmentos); // 20 bytes: uma notificação só, como no log de 20/10

        _simulador.OrfaoAntesDeCadaResposta = "#111#1#";
        await Escrever("#081#0#");
        _relogio.Avancar(ProcessadorSimulado.AtrasoLeituraReal);
        await Apoio.Esperar(() => Recebido.Length >= 24);

        Assert.Equal("#111#1#\r\n#081#0#+10.0#\r\n", Recebido);
        Assert.Equal([20, 4], Tamanhos()); // 24 bytes: 20 e o resto
    }

    [Fact]
    public async Task O_tamanho_da_notificacao_e_o_sorteado_sao_opcionais_e_nunca_passam_de_20()
    {
        await ConectarEAutenticar();

        _simulador.TamanhoNotificacao = 3;
        await Escrever("#081#0#");
        _relogio.Avancar(ProcessadorSimulado.AtrasoLeituraReal);
        await Apoio.Esperar(() => Recebido.Length >= 15);
        Assert.Equal("#081#0#+10.0#\r\n", Recebido);
        Assert.Equal([3, 3, 3, 3, 3], Tamanhos());
        Limpar();

        _simulador.TamanhoMaximoFragmento = 5;
        await Escrever("#081#1#");
        _relogio.Avancar(ProcessadorSimulado.AtrasoLeituraReal);
        await Apoio.Esperar(() => Recebido.Length >= 14);
        Assert.Equal("#081#1#+7.0#\r\n", Recebido);
        Assert.All(Tamanhos(), t => Assert.InRange(t, 1, 5));

        Assert.Throws<ArgumentOutOfRangeException>(() => _simulador.TamanhoNotificacao = 21);
        Assert.Throws<ArgumentOutOfRangeException>(() => _simulador.TamanhoNotificacao = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => _simulador.TamanhoMaximoFragmento = 21);
        _simulador.TamanhoMaximoFragmento = null;
    }

    [Fact]
    public async Task No_ritmo_real_a_leitura_completa_chega_a_84_de_84_em_notificacoes_de_ate_20_bytes()
    {
        // O relógio do sistema: a senha leva 23 ms e cada leitura 25 ms de verdade.
        var simulador = new ProcessadorSimulado();
        var tamanhos = new List<int>();
        simulador.Notificacao += f =>
        {
            lock (tamanhos)
                tamanhos.Add(f.Length);
        };
        var log = new LogBle();
        var sessao = new Sessao(simulador, new CofreMemoria { Senha = "AB12" }, log, Tempos);
        // O prazo de 500 ms folga os 25 ms e só encurta a espera do voltímetro (ERRO 0, órfão).
        var leitor = new LeitorCompleto(sessao, log, new TemposLeitura(TimeSpan.Zero, TimeSpan.FromMilliseconds(500)));

        await sessao.ConectarAsync();
        Assert.Equal(EstadoSessao.Conectado, sessao.Estado);
        var r = await leitor.LerAsync();

        Assert.True(r.Completo);
        Assert.Equal("84 de 84", r.PlacarMarco1.Texto);
        Assert.Equal("11 de 12", r.PlacarExtras.Texto);
        Assert.Equal(["ERRO 0"], r.Orfaos);
        Assert.DoesNotContain(r.Lidos, l => l.Tentativas > 1);
        lock (tamanhos)
            Assert.All(tamanhos, t => Assert.InRange(t, 1, ProcessadorSimulado.MaximoPorNotificacao));
    }

    // ----------------------------------------------------------------- falhas injetáveis

    [Fact]
    public async Task Fragmentos_de_ate_20_bytes_o_leitor_junta_e_le_84_de_84()
    {
        _simulador.TamanhoMaximoFragmento = 20;

        var r = await LerTudo();

        Assert.True(r.Completo);
        Assert.Equal("84 de 84", r.PlacarMarco1.Texto);
        Assert.Equal("11 de 12", r.PlacarExtras.Texto);
        Assert.Equal(["ERRO 0"], r.Orfaos);
        Assert.DoesNotContain(r.Lidos, l => l.Tentativas > 1);
        // Houve mesmo quebra: mais notificações do que mensagens.
        Assert.True(_fragmentos > _simulador.Notificacoes.Count, $"{_fragmentos} fragmentos");
        Assert.All(_recebido, f => Assert.InRange(f.Length, 1, 20));
    }

    [Fact]
    public async Task Queda_na_N_esima_escrita()
    {
        _simulador.CairNaEscrita = 5; // a senha e 3 consultas passam

        var r = await LerTudo();

        Assert.False(r.Completo);
        Assert.Equal("3 de 84", r.PlacarMarco1.Texto);
        Assert.Equal(1, _simulador.Quedas);
        Assert.False(_simulador.Conectado);
    }

    [Fact]
    public async Task Silencio_numa_consulta_vira_falha_sem_resposta()
    {
        _simulador.Silenciar.Add("#081#2#");

        var r = await LerTudo();

        Assert.True(r.Completo);
        Assert.Equal("83 de 84", r.PlacarMarco1.Texto);
        Assert.Equal(LeitorCompleto.MotivoSemResposta, r.FalhaDe("081", 2)!.Motivo);
        Assert.Equal(2, _simulador.Escritas.Count(e => e == "#081#2#")); // com a nova tentativa
    }

    [Fact]
    public async Task Mudo_nao_responde_nem_a_senha()
    {
        _simulador.Mudo = true;
        var (sessao, _) = SessaoComLeitor();

        await sessao.ConectarAsync();

        Assert.Equal(EstadoSessao.Desconectado, sessao.Estado);
        Assert.Equal(Sessao.MsgSemResposta, sessao.Mensagem);
    }

    [Fact]
    public async Task Eco_diferente_do_pedido()
    {
        _simulador.EcosDiferentes["*081*0*-3.0*"] = "#081#0#-2.9#";
        await ConectarEAutenticar();

        Assert.Equal("#081#0#-2.9#\r\n", await EscreverEEsperarEco("*081*0*-3.0*"));
        Assert.Equal("-3.0", _simulador.Valor("081", 0));
    }

    [Fact]
    public async Task Orfaos_antes_de_cada_resposta_nao_atrapalham_o_marco_1()
    {
        _simulador.OrfaoAntesDeCadaResposta = "#111#1#";

        var r = await LerTudo();

        Assert.Equal("84 de 84", r.PlacarMarco1.Texto);
        Assert.Contains("#111#1#", r.Orfaos);
    }

    [Fact]
    public async Task Sem_anuncio_o_scan_nao_acha()
    {
        _simulador.Endereco = null;
        var (sessao, _) = SessaoComLeitor();

        await sessao.ConectarAsync();

        Assert.Equal(Sessao.MsgNaoAchou, sessao.Mensagem);
        Assert.Equal(1, _simulador.Procuras);
        Assert.Equal(0, _simulador.Conexoes);
    }
}
