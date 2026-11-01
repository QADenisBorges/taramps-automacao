using System.Text.Json;
using Agente.ModeloFalso;
using Agente.Porta;
using Agente.Servico;
using Agente.Servico.Programa;

namespace Agente.Testes;

/// <summary>
/// O lançador de verdade (a pasta de trabalho isolada, fora do repositório), a fábrica do tipo do lançador, a validação da
/// configuração, o código 2 do executável e o log que gira.
/// </summary>
public sealed class LancadorTestes
{
    private static ConfigServico ConfigCom(string json) => ConfigServico.DeJson(json);

    [Fact]
    public void O_Claude_roda_numa_pasta_de_trabalho_fora_do_repositorio_com_as_tarefas_copiadas_e_a_porta_no_PATH()
    {
        using var local = new LocalTemporario();
        var config = new ConfigLancador { Comando = "claude", Argumentos = ["-p", "{prompt}", "--tarefa={tarefa}"], PastaPorta = "bin" };
        var lancador = new LancadorClaude(config, Caminhos.Agente, local.Local);

        var inicio = lancador.InicioPara("criar-memoria");

        var trabalho = Path.Combine(Path.GetFullPath(local.Local), LancadorClaude.Trabalho);
        Assert.Equal(trabalho, inicio.WorkingDirectory);
        var repositorio = LancadorClaude.RaizDoRepositorio(Caminhos.Agente);
        Assert.NotNull(repositorio);
        Assert.False(inicio.WorkingDirectory.StartsWith(repositorio + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
        Assert.True(File.Exists(Path.Combine(trabalho, "tarefas", "criar-memoria.md")));
        Assert.True(File.Exists(Path.Combine(trabalho, "tarefas", "ficha-tecnica.md")));
        Assert.StartsWith(Path.Combine(Path.GetFullPath(local.Local), "bin") + Path.PathSeparator, inicio.Environment["PATH"]);
        Assert.Equal(Path.GetFullPath(local.Local), inicio.Environment[PastaLocal.Variavel]);
        Assert.Equal("claude", inicio.FileName);
        Assert.Equal("-p", inicio.ArgumentList[0]);
        Assert.StartsWith("# Tarefa: criar uma memória", inicio.ArgumentList[1]);
        Assert.Equal("--tarefa=criar-memoria", inicio.ArgumentList[2]);
        // A chave não está na pasta de trabalho.
        Assert.False(File.Exists(Path.Combine(trabalho, PareamentoLocal.Arquivo)));
    }

    [Fact]
    public void A_copia_das_tarefas_e_nova_a_cada_lancamento()
    {
        using var local = new LocalTemporario();
        var lancador = new LancadorClaude(new ConfigLancador(), Caminhos.Agente, local.Local);
        var velha = Path.Combine(lancador.PastaTrabalho, "tarefas", "velha.md");
        Directory.CreateDirectory(Path.GetDirectoryName(velha)!);
        File.WriteAllText(velha, "sobrou");

        lancador.InicioPara("criar-memoria");

        Assert.False(File.Exists(velha));
    }

    [Fact]
    public void Uma_pasta_de_trabalho_dentro_do_repositorio_e_recusada_sem_criar_nada()
    {
        var dentro = Path.Combine(Caminhos.Agente, "local-de-teste-" + Guid.NewGuid().ToString("N"));
        var lancador = new LancadorClaude(new ConfigLancador(), Caminhos.Agente, dentro);

        Assert.Throws<InvalidOperationException>(() => lancador.InicioPara("criar-memoria"));
        Assert.False(Directory.Exists(dentro));
    }

    [Fact]
    public void A_pasta_local_padrao_fica_fora_do_repositorio()
    {
        var repositorio = LancadorClaude.RaizDoRepositorio(Caminhos.Agente)!;
        Assert.EndsWith(PastaLocal.NomePadrao, PastaLocal.Padrao);
        Assert.False(Path.GetFullPath(PastaLocal.Padrao).StartsWith(repositorio, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void A_fabrica_so_conhece_claude_e_modelo_falso()
    {
        using var local = new LocalTemporario();
        var claude = Caminhos.Config;
        var falso = claude with { Lancador = claude.Lancador with { Tipo = ConfigLancador.TipoModeloFalso } };
        var outro = claude with { Lancador = claude.Lancador with { Tipo = "cluade" } };

        Assert.IsType<LancadorClaude>(FabricaLancador.Criar(claude, Caminhos.Agente, local.Local));
        Assert.IsType<LancadorModeloFalso>(FabricaLancador.Criar(falso, Caminhos.Agente, local.Local));
        var erro = Assert.Throws<ConfigInvalidaException>(() => FabricaLancador.Criar(outro, Caminhos.Agente, local.Local));
        Assert.Contains("cluade", erro.Message);
    }

    [Theory]
    [InlineData("""{ "intervaloS": 0 }""")]
    [InlineData("""{ "intervaloS": -1 }""")]
    [InlineData("""{ "esperaQuedaS": 0 }""")]
    [InlineData("""{ "prazoAgenteMin": 0 }""")]
    [InlineData("""{ "prazoAgenteMin": 1441 }""")]
    [InlineData("""{ "gatilhos": null }""")]
    [InlineData("""{ "lancador": null }""")]
    [InlineData("""{ "lancador": { "tipo": "outro" } }""")]
    [InlineData("""{ "lancador": { "tipo": "claude", "comando": "" } }""")]
    [InlineData("""{ "lancador": { "tipo": "claude", "argumentos": null } }""")]
    [InlineData("""{ "lancador": { "tipo": "modelo-falso", "roteiros": null } }""")]
    public void Configuracao_fora_da_faixa_e_recusada(string json) =>
        Assert.Throws<ConfigInvalidaException>(() => ConfigCom(json));

    [Fact]
    public void A_configuracao_padrao_e_a_do_repositorio_passam()
    {
        ConfigCom("{}");
        Caminhos.Config.Validar();
        Assert.ThrowsAny<JsonException>(() => ConfigCom("{ não é json"));
    }

    [Theory]
    [InlineData("{ não é json", "não é um JSON válido")]
    [InlineData("""{ "lancador": { "tipo": "cluade" } }""", "lancador.tipo")]
    public async Task O_servico_com_a_configuracao_invalida_para_com_o_codigo_2_e_a_mensagem(string json, string mensagem)
    {
        using var local = new LocalTemporario();
        var agente = Path.Combine(local.Raiz, "agente");
        Directory.CreateDirectory(agente);
        File.WriteAllText(Path.Combine(agente, ConfigServico.Arquivo), json);

        var (codigo, erro) = await RodarServicoAsync(agente, local.Local);

        Assert.Equal(2, codigo);
        Assert.Contains(mensagem, erro);
    }

    private static async Task<(int Codigo, string Erro)> RodarServicoAsync(string agente, string pastaLocal)
    {
        var inicio = Executavel.De(Path.Combine(AppContext.BaseDirectory, "servico.dll"));
        inicio.ArgumentList.Add("--agente");
        inicio.ArgumentList.Add(agente);
        inicio.ArgumentList.Add("--local");
        inicio.ArgumentList.Add(pastaLocal);
        inicio.UseShellExecute = false;
        inicio.RedirectStandardOutput = true;
        inicio.RedirectStandardError = true;
        inicio.StandardErrorEncoding = System.Text.Encoding.UTF8;
        using var p = System.Diagnostics.Process.Start(inicio)!;
        var saida = p.StandardOutput.ReadToEndAsync();
        var erro = await p.StandardError.ReadToEndAsync().WaitAsync(TimeSpan.FromSeconds(60));
        await saida.WaitAsync(TimeSpan.FromSeconds(60));
        if (!p.WaitForExit(TimeSpan.FromSeconds(30)))
        {
            p.Kill(entireProcessTree: true);
            Assert.Fail("o serviço não parou com a configuração inválida");
        }
        return (p.ExitCode, erro);
    }

    [Fact]
    public void O_log_passado_do_maximo_vira_o_ponto_1_e_um_novo_comeca()
    {
        using var local = new LocalTemporario();
        var caminho = Path.Combine(local.Local, "teste.log");

        ArquivoLog.Anexar(caminho, "primeira", maximo: 10);
        ArquivoLog.Anexar(caminho, "segunda", maximo: 10);

        Assert.Equal("primeira" + Environment.NewLine, File.ReadAllText(caminho + ".1"));
        Assert.Equal("segunda" + Environment.NewLine, File.ReadAllText(caminho));
    }

    [Fact]
    public void Uma_falha_ao_escrever_o_log_nao_derruba_quem_escreve()
    {
        using var local = new LocalTemporario();
        var pasta = Path.Combine(local.Local, "uma-pasta");
        Directory.CreateDirectory(pasta);

        ArquivoLog.Anexar(pasta, "não cabe: é uma pasta"); // UnauthorizedAccessException no Windows
        new LogServico(pasta).Escrever("também não");
    }
}
