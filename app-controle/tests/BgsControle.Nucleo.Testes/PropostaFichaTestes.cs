using System.Net;
using System.Text.Json;
using BgsControle.Nucleo.Aparelho;
using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Gerador;
using BgsControle.Nucleo.Leitura;
using BgsControle.Nucleo.Player;
using BgsControle.Nucleo.Porta;
using BgsControle.Nucleo.Presets;
using BgsControle.Nucleo.Registro;
using BgsControle.Nucleo.Seguranca;
using BgsControle.Nucleo.Setups;
using BgsControle.Nucleo.Uso;
using BgsControle.Simulador;

namespace BgsControle.Nucleo.Testes;

/// <summary>
/// A história 14b no núcleo: o pedido de ficha de um modelo fora da base (<c>ficha-pedida</c>), a proposta pela porta
/// (<c>POST /ficha/proposta</c>, com o prazo de 60 s) e o salvar dos dados usados, que só o toque do usuário faz. Uma
/// linha da matriz por teste. A proposta não mexe no aparelho: tudo aqui roda sem conectar. Os dados das propostas são
/// genéricos.
/// </summary>
public sealed class PropostaFichaTestes : IDisposable
{
    private static readonly TemposSessao Tempos = new(
        Scan: TimeSpan.FromSeconds(1),
        TentativaConexao: TimeSpan.FromSeconds(1),
        PausaEntreTentativas: TimeSpan.FromMilliseconds(1),
        RespostaSenha: TimeSpan.FromSeconds(2),
        PrazoSenha: TimeSpan.FromSeconds(60));

    private static readonly IPAddress Casa = IPAddress.Parse("192.168.0.10");
    private const string Modelo = "ABC 123";
    private const string Fonte = "https://exemplo.com/abc-123.pdf";

    private readonly RelogioFalso _relogio = new();
    private readonly ProcessadorSimulado _simulador;
    private readonly Chaves _chaves;
    private readonly LogBle _log = new();
    private readonly TextoMemoria _guardadoModelos = new(CenarioFilme.ModelosJson);
    private readonly BaseModelos _modelos;
    private readonly GerenteSetups _setups;
    private readonly Pareamento _pareamento;
    private readonly PropostaFicha _ficha;
    private readonly PortaComandos _porta;
    private readonly Sessao _sessao;

    public PropostaFichaTestes()
    {
        _simulador = new ProcessadorSimulado(_relogio) { AtrasoSenha = TimeSpan.Zero, AtrasoLeitura = TimeSpan.Zero };
        var cofre = new CofreFalso { Senha = "AB12" };
        _chaves = new Chaves(new RepositorioMemoria());
        var historico = new Historico(new TextoMemoria(), _relogio, _log);
        var uso = new UsoCompartilhado(_relogio, _log);
        var sessao = _sessao = new Sessao(_simulador, cofre, _log, Tempos, _chaves, historico, uso);
        var leitor = new LeitorCompleto(sessao, _log, new TemposLeitura(TimeSpan.Zero, TimeSpan.FromMilliseconds(100)));
        var leitura = new LeituraAutomatica(sessao, leitor);
        var estado = new EstadoAparelho(sessao, leitura);
        var escritor = new Escritor(sessao, estado, _log, relogio: _relogio);
        var copia = new CopiaAntes(sessao, estado, escritor, leitura, new TextoMemoria(), _log, _relogio);
        var link = new LinkCanais(sessao, estado, escritor, new TextoMemoria(), _log);
        var presets = new GerentePresets(sessao, estado, escritor, leitura, copia, new TextoMemoria(CenarioFilme.PresetsJson), _log, _relogio);
        _modelos = new BaseModelos(_guardadoModelos, _log, _relogio);
        _setups = new GerenteSetups(sessao, estado, escritor, leitura, copia, link, presets, new GerentePlaylists(new TextoMemoria(), _log), _chaves,
            new TextoMemoria(CenarioFilme.SetupsJson), new TextoMemoria(), _log, _relogio, modelos: _modelos);
        var gerador = new GeradorSinal(sessao, estado, leitura, copia, _log, relogio: _relogio);
        var monitor = new MonitorAparelho(sessao, leitura, _relogio);
        var comandos = new ComandosAparelho(sessao, estado, leitura, copia, cofre, _log, gerador, relogio: _relogio);
        _pareamento = new Pareamento(new TextoMemoria(), _relogio, _log);
        _ficha = new PropostaFicha(_setups, _log, _relogio);
        _porta = new PortaComandos(sessao, _chaves, uso, historico, _pareamento, new PedidosConfirmacao(_relogio, _log), new RedeFalsa(), estado,
            escritor, _setups, gerador, comandos, monitor, _log, _relogio, 0, ficha: _ficha);
    }

    /// <summary>Nenhum ajuste vai ao aparelho: a ficha é só do celular (só o teste que conecta lê o aparelho).</summary>
    public void Dispose()
    {
        _porta.Dispose();
        _ficha.Dispose();
        Assert.DoesNotContain(_simulador.Escritas, e => Catalogo.EscritaDeAudio(e) is not null && !e.StartsWith("*100*", StringComparison.Ordinal));
    }

    // ----------------------------------------------------------------- apoio

    private async Task<string> Parear()
    {
        await _chaves.Carregada;
        await _pareamento.Carregado;
        await _setups.Carregada;
        await _modelos.Carregada;
        Assert.True((await _chaves.DefinirAsync(Chave.ControlePelaRede, true, OrigemComando.Tela)).Ok);
        await _porta.AtualizarAsync();
        var r = await Pedir("POST", "/parear", $$"""{"codigo":"{{_pareamento.Codigo}}","nome":"Claude no notebook"}""");
        Assert.Equal(200, r.Status);
        return Json(r).GetProperty("chave").GetString()!;
    }

    private Task<RespostaPorta> Pedir(string metodo, string alvo, string? corpo = null, string? chave = null) =>
        _porta.AtenderAsync(PedidoPorta.De(metodo, alvo, Casa, corpo, chave));

    private static JsonElement Json(RespostaPorta r) => JsonDocument.Parse(r.Json).RootElement;

    private static string? Motivo(RespostaPorta r) => Json(r).TryGetProperty("motivo", out var m) ? m.GetString() : null;

    private static Equipamento Woofer(string? modelo = Modelo, DadosTecnicos? dados = null) =>
        new("x", GrupoEquipamento.AltoFalante, "woofer", "Woofer extra", modelo) { Dados = dados ?? DadosTecnicos.Nenhum };

    /// <summary>Adiciona o woofer ao Casa 2.1 e devolve o id dele.</summary>
    private async Task<string> Adicionar(Equipamento e)
    {
        var r = await _setups.AdicionarEquipamentoAsync(CasaDoDenis.IdSetupCasa, e);
        Assert.True(r.Ok, r.Mensagem);
        return r.Id!;
    }

    private Equipamento Equipamento(string id) => _setups.Setup(CasaDoDenis.IdSetupCasa)!.Equipamentos.Single(e => e.Id == id);

    private Task<RespostaPorta> Proposta(string chave, string id, string dados) =>
        Pedir("POST", "/ficha/proposta", $$"""{"idEquipamento":"{{id}}","dados":[{{dados}}]}""", chave);

    private static string Dado(string campo, decimal valor, string origem = "oficial", string fonte = Fonte, string? aviso = null) =>
        $$"""{"campo":"{{campo}}","valor":{{valor.ToString(System.Globalization.CultureInfo.InvariantCulture)}},"origem":"{{origem}}","fonte":"{{fonte}}"{{(aviso is null ? "" : $",\"aviso\":\"{aviso}\"")}}}""";

    private static readonly string TresDados = string.Join(",",
        Dado("fs", 46m),
        Dado("qes", 0.42m, "outra-fonte", "Fórum de áudio, tópico do ABC 123"),
        Dado("xmax", 6.5m, aviso: "Qts não fecha com Qes e Qms"));

    // ----------------------------------------------------------------- matriz

    [Fact]
    public async Task Ficha_pedida_ao_salvar_um_equipamento_com_o_modelo_fora_da_base()
    {
        var chave = await Parear();

        var id = await Adicionar(Woofer());

        var pedido = Assert.Single(_porta.Eventos(), e => e.Tipo == PropostaFicha.EventoPedida).Ficha!;
        Assert.Equal((CasaDoDenis.IdSetupCasa, id, Modelo, GrupoEquipamento.AltoFalante, "woofer"),
            (pedido.IdSetup, pedido.IdEquipamento, pedido.Modelo, pedido.Grupo, pedido.Categoria));
        var json = Json(await Pedir("GET", "/eventos?desde=0", chave: chave)).GetProperty("eventos").EnumerateArray()
            .Single(e => e.GetProperty("tipo").GetString() == PropostaFicha.EventoPedida);
        Assert.Equal(id, json.GetProperty("idEquipamento").GetString());
        Assert.Equal(CasaDoDenis.IdSetupCasa, json.GetProperty("idSetup").GetString());
        Assert.Equal(Modelo, json.GetProperty("modelo").GetString());
        Assert.Equal("alto-falante", json.GetProperty("grupo").GetString());
        Assert.Equal("woofer", json.GetProperty("categoria").GetString());
        Assert.Single(_ficha.Pendentes);

        // O modelo entrou na base só com o nome (sem ficha): salvar de novo pede de novo; um genérico, não.
        Assert.True((await _setups.EditarEquipamentoAsync(CasaDoDenis.IdSetupCasa, Equipamento(id) with { Nome = "Woofer 2" })).Ok);
        await Adicionar(Woofer(modelo: null));
        Assert.Equal(2, _porta.Eventos().Count(e => e.Tipo == PropostaFicha.EventoPedida));
    }

    [Fact]
    public async Task Depois_do_prazo_salvar_de_novo_o_mesmo_modelo_pede_a_ficha_de_novo()
    {
        var chave = await Parear();
        var id = await Adicionar(Woofer());
        _relogio.Avancar(TimeSpan.FromSeconds(61));
        Assert.Empty(_ficha.Pendentes); // o prazo tirou o pedido
        Assert.Equal(409, (await Proposta(chave, id, TresDados)).Status);

        Assert.True((await _setups.EditarEquipamentoAsync(CasaDoDenis.IdSetupCasa, Equipamento(id) with { Nome = "Woofer 2" })).Ok);

        Assert.Equal(2, _porta.Eventos().Count(e => e.Tipo == PropostaFicha.EventoPedida));
        Assert.Single(_ficha.Pendentes);
        Assert.Equal(200, (await Proposta(chave, id, TresDados)).Status);
    }

    [Fact]
    public async Task Com_o_modelo_mudado_depois_do_pedido_a_proposta_sai_e_nada_e_gravado()
    {
        var chave = await Parear();
        var id = await Adicionar(Woofer());
        Assert.True((await _setups.EditarEquipamentoAsync(CasaDoDenis.IdSetupCasa, Equipamento(id) with { Modelo = CasaDoDenis.ModeloJbl })).Ok);

        // O pedido do ABC 123 ficou, mas o equipamento agora é o da JBL (com ficha na base, sem pedido novo).
        var r = await Proposta(chave, id, TresDados);
        Assert.Equal(409, r.Status);
        Assert.Equal(PropostaFicha.MsgPrazo, Motivo(r));
        Assert.Null(_ficha.Atual);

        // A proposta já na folha e o modelo trocado antes do Salvar: ela sai sem gravar.
        var outro = await Adicionar(Woofer(modelo: "DEF 4"));
        Assert.Equal(200, (await Proposta(chave, outro, TresDados)).Status);
        Assert.True((await _setups.EditarEquipamentoAsync(CasaDoDenis.IdSetupCasa, Equipamento(outro) with { Modelo = CasaDoDenis.ModeloSub })).Ok);
        var salvo = await _ficha.SalvarAsync(["fs"]);
        Assert.False(salvo.Ok);
        Assert.Equal(PropostaFicha.MsgPrazo, salvo.Motivo);
        Assert.Null(_ficha.Atual);
        Assert.Null(Equipamento(outro).Dados.Fs);
        Assert.True(_modelos.Ficha("DEF 4")!.Dados.Vazia);
    }

    [Fact]
    public async Task A_proposta_passa_com_o_Setup_em_duvida_e_o_ajuste_nao()
    {
        // O atraso do Sub difere da FILME: o Setup fica em dúvida na conexão.
        _simulador.Definir("050", 2, "0.0");
        await _chaves.Carregada;
        await _sessao.ConectarAsync();
        for (var i = 0; i < 3000 && !(_setups.Estado == EstadoConferencia.Duvida && !_setups.Ocupado); i++)
        {
            _relogio.Avancar(TimeSpan.FromMilliseconds(10));
            await Task.Delay(1);
        }
        Assert.Equal(EstadoConferencia.Duvida, _setups.Estado);
        var chave = await Parear();
        Assert.NotNull(_setups.MotivoBloqueioIa);
        var id = await Adicionar(Woofer());

        Assert.Equal(200, (await Proposta(chave, id, TresDados)).Status);
        var ajuste = await Pedir("POST", "/ajuste", """{"codigo":"081","indice":0,"valor":1.0}""", chave);
        Assert.Equal(409, ajuste.Status);
        Assert.Equal(_setups.MotivoBloqueioIa, Motivo(ajuste));
    }

    [Fact]
    public async Task Com_a_chave_desligada_salvar_o_modelo_fora_da_base_nao_pede_nada()
    {
        await _chaves.Carregada;
        Assert.False(_chaves.Ligada(Chave.ControlePelaRede));

        await Adicionar(Woofer());

        Assert.DoesNotContain(_porta.Eventos(), e => e.Tipo == PropostaFicha.EventoPedida);
        Assert.Empty(_ficha.Pendentes);
    }

    [Fact]
    public async Task Proposta_com_3_dados_vai_para_a_folha_e_so_o_Salvar_grava_os_usados_na_ficha_e_na_base()
    {
        var chave = await Parear();
        // A ficha já tem o Qes digitado: recusado, ele fica como está.
        var id = await Adicionar(Woofer(dados: DadosTecnicos.Nenhum with { Qes = DadoTecnico.Digitado(0.5m) }));
        var gravacoes = _guardadoModelos.Gravacoes;

        var r = await Proposta(chave, id, TresDados);

        Assert.Equal(200, r.Status);
        var p = _ficha.Atual!;
        Assert.Equal("Woofer extra", p.NomeEquipamento);
        Assert.Equal(["fs", "qes", "xmax"], p.Dados.Select(d => d.Campo));
        Assert.Equal(OrigemDado.OutraFonte, p.Dados[1].Origem);
        Assert.Equal("Qts não fecha com Qes e Qms", p.Dados[2].Aviso);
        // A porta não escreve: a ficha e a base ficam como estavam até o toque.
        Assert.Null(Equipamento(id).Dados.Fs);
        Assert.Equal(gravacoes, _guardadoModelos.Gravacoes);
        Assert.Empty(_ficha.Pendentes);

        var salvo = await _ficha.SalvarAsync(["fs", "xmax"]);

        Assert.True(salvo.Ok, salvo.Mensagem);
        var dados = Equipamento(id).Dados;
        Assert.Equal(new DadoTecnico(46m, OrigemDado.Oficial, Fonte), dados.Fs);
        Assert.Equal(new DadoTecnico(6.5m, OrigemDado.Oficial, Fonte), dados.Xmax);
        Assert.Equal(DadoTecnico.Digitado(0.5m), dados.Qes); // o recusado não troca o que a ficha tinha
        var naBase = _modelos.Ficha(Modelo)!.Dados;
        Assert.Equal(new DadoTecnico(46m, OrigemDado.Oficial, Fonte), naBase.Fs);
        Assert.Equal(new DadoTecnico(6.5m, OrigemDado.Oficial, Fonte), naBase.Xmax);
        Assert.Null(naBase.Qes); // o digitado não vai à base
        Assert.Null(_ficha.Atual);
    }

    [Fact]
    public async Task Campo_invalido_da_400_e_o_pedido_continua_esperando()
    {
        var chave = await Parear();
        var id = await Adicionar(Woofer());

        foreach (var (dados, motivo) in new[]
                 {
                     (Dado("qts", 0.4m), PropostaFicha.MsgCampo("qts")),
                     (Dado("ganho", 19.5m), PropostaFicha.MsgCampo("ganho")), // um dado do módulo, não do alto-falante
                     (Dado("fs", 0m), PropostaFicha.MsgFaixa(PropostaFicha.FaixaDe("fs")!)),
                     (Dado("xmax", 0.0065m), PropostaFicha.MsgFaixa(PropostaFicha.FaixaDe("xmax")!)), // em metros, não em mm
                     (Dado("sd", 0.022m), PropostaFicha.MsgFaixa(PropostaFicha.FaixaDe("sd")!)), // em m², não em cm²
                     (Dado("le", 0.0000005m), PropostaFicha.MsgFaixa(PropostaFicha.FaixaDe("le")!)), // em H, não em mH
                     (Dado("fs", 46m, origem: "medido"), PropostaFicha.MsgOrigem("fs")),
                     (Dado("fs", 46m, origem: "qualquer"), PropostaFicha.MsgOrigem("fs")),
                     (Dado("fs", 46m, fonte: ""), PropostaFicha.MsgFonte("fs")),
                     (Dado("fs", 46m, fonte: new string('a', 121)), PropostaFicha.MsgFonte("fs")),
                     (Dado("fs", 46m) + "," + Dado("fs", 47m), PropostaFicha.MsgCampoRepetido("fs")),
                 })
        {
            var r = await Proposta(chave, id, dados);
            Assert.Equal(400, r.Status);
            Assert.Equal(motivo, Motivo(r));
        }
        Assert.Equal(400, (await Pedir("POST", "/ficha/proposta", $$"""{"idEquipamento":"{{id}}","dados":[]}""", chave)).Status);
        Assert.Null(_ficha.Atual);

        Assert.Equal(200, (await Proposta(chave, id, Dado("fs", 46m))).Status);
        Assert.NotNull(_ficha.Atual);
    }

    [Fact]
    public async Task Proposta_depois_de_60_s_da_409_e_nada_chega_a_folha()
    {
        var chave = await Parear();
        var id = await Adicionar(Woofer());

        _relogio.Avancar(TimeSpan.FromSeconds(61));
        var r = await Proposta(chave, id, TresDados);

        Assert.Equal(409, r.Status);
        Assert.Equal(PropostaFicha.MsgPrazo, Motivo(r));
        Assert.Null(_ficha.Atual);
        Assert.Empty(_ficha.Pendentes);
        // Sem pedido para o equipamento: o mesmo 409.
        var outro = await Proposta(chave, "equipamento-que-nao-pediu", TresDados);
        Assert.Equal(409, outro.Status);
        Assert.Equal(PropostaFicha.MsgPrazo, Motivo(outro));
        // A ficha ficou como estava, sem aviso de erro.
        Assert.True(Equipamento(id).Dados.Vazia);
    }

    [Fact]
    public async Task Fechar_a_folha_sem_salvar_nao_grava_nada()
    {
        var chave = await Parear();
        var id = await Adicionar(Woofer());
        Assert.Equal(200, (await Proposta(chave, id, TresDados)).Status);
        var gravacoes = _guardadoModelos.Gravacoes;

        _ficha.Fechar();

        Assert.Null(_ficha.Atual);
        Assert.True(Equipamento(id).Dados.Vazia);
        Assert.True(_modelos.Ficha(Modelo)!.Dados.Vazia);
        Assert.Equal(gravacoes, _guardadoModelos.Gravacoes);
        Assert.False((await _ficha.SalvarAsync(["fs"])).Ok); // nada mais para salvar
    }
}
