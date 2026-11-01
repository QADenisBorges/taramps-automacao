using System.Text.RegularExpressions;
using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Leitura;
using BgsControle.Nucleo.Player;
using BgsControle.Nucleo.Presets;
using BgsControle.Nucleo.Registro;
using BgsControle.Nucleo.Rendimento;
using BgsControle.Nucleo.Seguranca;
using BgsControle.Nucleo.Setups;
using BgsControle.Nucleo.Uso;
using BgsControle.Simulador;

namespace BgsControle.Nucleo.Testes;

/// <summary>
/// História 13a, a cerca contra o processador simulado: com o Casa 2.1 da fixture calibrado em uso, uma
/// escrita do limitador ou do passa-alta que deixe uma saída com o limitador OFF ou acima do teto é
/// recusada antes de qualquer envio, venha de onde vier (o escritor, o link, o aplicar de uma memória), e
/// a releitura fica igual. Uma linha da matriz por teste (a tela, a porta e a criação ficam nas bancadas
/// delas).
/// </summary>
public sealed class CercaTestes : IDisposable
{
    private static readonly TemposSessao Tempos = new(
        Scan: TimeSpan.FromSeconds(1),
        TentativaConexao: TimeSpan.FromSeconds(1),
        PausaEntreTentativas: TimeSpan.FromMilliseconds(1),
        RespostaSenha: TimeSpan.FromSeconds(2),
        PrazoSenha: TimeSpan.FromSeconds(60));

    private static readonly TemposLeitura LeituraRapida = new(TimeSpan.Zero, TimeSpan.FromMilliseconds(100));
    private static readonly Regex Senha = new(@"^\*000\*[0-9A-Z]{4}\*$");

    private const string IdAlta = "memoria-alta";

    private readonly RelogioFalso _relogio = new();
    private readonly ProcessadorSimulado _simulador;
    private readonly Chaves _chaves;
    private readonly LogBle _log = new();
    private readonly Sessao _sessao;
    private readonly LeituraAutomatica _leitura;
    private readonly EstadoAparelho _estado;
    private readonly Escritor _escritor;
    private readonly CopiaAntes _copia;
    private readonly LinkCanais _link;
    private readonly GerentePresets _presets;
    private readonly GerenteSetups _setups;

    private static Parametro Limitador => Catalogo.ParametroDe("071")!;
    private static Parametro PassaAlta => Catalogo.ParametroDe("040")!;
    private static Parametro Ganho => Catalogo.ParametroDe("081")!;

    /// <summary>O Casa 2.1 com a FILME (ativa) e a ALTA: a FILME com o limitador da JBL L em −3 dB.</summary>
    public CercaTestes() : this(Dados(CasaDoDenis.EquipamentosCasa))
    {
    }

    private CercaTestes(DadosSetups dados)
    {
        _simulador = new ProcessadorSimulado(_relogio) { AtrasoSenha = TimeSpan.Zero, AtrasoLeitura = TimeSpan.Zero };
        _chaves = new Chaves(new RepositorioMemoria());
        _sessao = new Sessao(_simulador, new CofreFalso { Senha = "AB12" }, _log, Tempos, _chaves);
        _leitura = new LeituraAutomatica(_sessao, new LeitorCompleto(_sessao, _log, LeituraRapida));
        _estado = new EstadoAparelho(_sessao, _leitura);
        _escritor = new Escritor(_sessao, _estado, _log, relogio: _relogio);
        _copia = new CopiaAntes(_sessao, _estado, _escritor, _leitura, new TextoMemoria(), _log, _relogio);
        _link = new LinkCanais(_sessao, _estado, _escritor, new TextoMemoria(), _log);
        _presets = new GerentePresets(_sessao, _estado, _escritor, _leitura, _copia, new TextoMemoria(CenarioFilme.PresetsJson), _log, _relogio);
        _setups = new GerenteSetups(_sessao, _estado, _escritor, _leitura, _copia, _link, _presets, new GerentePlaylists(new TextoMemoria(), _log),
            _chaves, new TextoMemoria(GerenteSetups.ParaJson(dados)), new TextoMemoria(), _log, _relogio);
    }

    private static DadosSetups Dados(IReadOnlyList<Equipamento> equipamentos, string ativa = CenarioFilme.IdMemoria)
    {
        var alta = CenarioFilme.Memoria with
        {
            Id = IdAlta,
            Nome = "ALTA",
            Regulagem = CenarioFilme.Regulagem
                .Select(v => v is { Codigo: "071", Indice: 0 } ? v with { NoFio = "-3.0" } : v)
                .ToArray(),
        };
        return CenarioFilme.Dados with
        {
            Setups = [CasaDoDenis.SetupCasa with { Equipamentos = equipamentos }],
            Memorias = [CenarioFilme.Memoria, alta],
            Uso = new Dictionary<string, UsoDoSetup>
            {
                [CasaDoDenis.IdSetupCasa] = new([ativa], [CenarioFilme.IdMemoria, IdAlta, null]),
            },
        };
    }

    /// <summary>Critério de aceite: nada além do catálogo chega ao simulador; nenhum 240, 241 ou 23N.</summary>
    public void Dispose()
    {
        Assert.DoesNotContain(_simulador.Escritas, e => e.StartsWith("*24", StringComparison.Ordinal) || e.StartsWith("*23", StringComparison.Ordinal));
        Assert.All(_simulador.Escritas, e => Assert.True(Senha.IsMatch(e) || Catalogo.EhLeitura(e) || Catalogo.EscritaDeAudio(e) is not null, e));
    }

    // ----------------------------------------------------------------- apoio

    private async Task Conectar(bool emUso = true)
    {
        await _chaves.Carregada;
        await _copia.Carregada;
        await _link.Carregada;
        await _presets.Carregada;
        await _setups.Carregada;
        await _sessao.ConectarAsync();
        Assert.Equal(EstadoSessao.Conectado, _sessao.Estado);
        await AndarAte(() => _estado.Pronto && !_estado.Lendo && !_leitura.Lendo && !_copia.EmAndamento && _setups.Conferido);
        if (emUso)
            Assert.Equal(CasaDoDenis.IdSetupCasa, _setups.SetupEmUso?.Id);
    }

    private async Task AndarAte(Func<bool> condicao)
    {
        for (var i = 0; i < 3000 && !condicao(); i++)
        {
            _relogio.Avancar(TimeSpan.FromMilliseconds(10));
            await Task.Delay(1);
        }
        Assert.True(condicao(), "a condição não ficou verdadeira");
    }

    private async Task<T> Rodar<T>(Task<T> tarefa)
    {
        await AndarAte(() => tarefa.IsCompleted);
        return await tarefa;
    }

    /// <summary>Uma escrita como o app (sem o "usuário mexendo").</summary>
    private async Task<ResultadoEscrita> Escrever(Parametro p, int canal, decimal valor)
    {
        using (Origem.Como(OrigemComando.App))
            return await Rodar(_escritor.EscreverAsync(p, canal, valor));
    }

    /// <summary>O passa-alta da JBL L em 200 Hz (LR24 da FILME): o curso do cone fica acima do térmico.</summary>
    private async Task JblLEm200Hz() => Assert.True((await Escrever(PassaAlta, 0, 200m)).Ok);

    private List<string> AudioDesde(int inicio) =>
        _simulador.Escritas.Skip(inicio).Where(e => Catalogo.EscritaDeAudio(e) is not null).ToList();

    /// <summary>A releitura completa traz o valor do simulador para o espelho.</summary>
    private async Task Reler()
    {
        var retrato = await Rodar(_leitura.LerNovaAsync());
        Assert.True(retrato?.Completo);
    }

    // ----------------------------------------------------------------- a matriz

    [Fact]
    public async Task Limitador_0_1_dB_acima_do_teto_e_recusado_sem_envio_e_a_releitura_fica_igual()
    {
        await Conectar();
        await JblLEm200Hz();
        var inicio = _simulador.Escritas.Count;

        var r = await Escrever(Limitador, 0, -6.7m);

        Assert.False(r.Ok);
        Assert.Equal("Limite: JBL L, −6,8 dB, pelo térmico.", r.Motivo);
        Assert.Empty(AudioDesde(inicio));
        await Reler();
        Assert.Equal("-15.0", _simulador.Valor("071", 0));
        Assert.Equal(-15.0m, _estado.Valor("071", 0)!.Numero);

        var noTeto = await Escrever(Limitador, 0, -6.8m);

        Assert.True(noTeto.Ok, noTeto.Motivo);
        Assert.Equal("-6.8", _simulador.Valor("071", 0));
    }

    [Fact]
    public async Task Limitador_OFF_e_recusado()
    {
        await Conectar();
        var inicio = _simulador.Escritas.Count;

        var r = await Escrever(Limitador, 1, Cerca.LimitadorOff);

        Assert.False(r.Ok);
        Assert.Equal("Limite: JBL R, o limitador não pode ficar desligado.", r.Motivo);
        Assert.Empty(AudioDesde(inicio));
        Assert.Equal("-18.0", _simulador.Valor("071", 1));
    }

    [Fact]
    public async Task Baixar_o_corte_que_baixa_o_X_e_recusado_e_passa_depois_de_baixar_o_limitador()
    {
        await Conectar();
        await JblLEm200Hz();
        Assert.True((await Escrever(Limitador, 0, -6.8m)).Ok);
        var inicio = _simulador.Escritas.Count;

        var corte = await Escrever(PassaAlta, 0, 150m);

        Assert.False(corte.Ok);
        Assert.StartsWith("Limite: JBL L, −9,", corte.Motivo);
        Assert.EndsWith("dB, pelo curso do cone. Baixe o limitador antes de mudar o corte.", corte.Motivo);
        Assert.Empty(AudioDesde(inicio));
        Assert.Equal("200", _simulador.Valor("040", 0));

        Assert.True((await Escrever(Limitador, 0, -10.0m)).Ok);
        var depois = await Escrever(PassaAlta, 0, 150m);

        Assert.True(depois.Ok, depois.Motivo);
        Assert.Equal("150", _simulador.Valor("040", 0));
    }

    [Fact]
    public async Task O_link_confere_os_dois_canais_do_Sub_e_a_copia_do_link_passa()
    {
        await Conectar();
        var inicio = _simulador.Escritas.Count;

        var acima = await Escrever(Limitador, 2, -7.7m);

        Assert.False(acima.Ok);
        Assert.Equal("Limite: Sub (CH3), −7,8 dB, pelo curso do cone.", acima.Motivo);
        Assert.Empty(AudioDesde(inicio));

        var noTeto = await Escrever(Limitador, 2, -7.8m);

        Assert.True(noTeto.Ok, noTeto.Motivo);
        Assert.Null(noTeto.Aviso);
        Assert.Equal("-7.8", _simulador.Valor("071", 2));
        Assert.Equal("-7.8", _simulador.Valor("071", 3));
    }

    [Fact]
    public async Task Outras_escritas_nao_passam_pela_cerca()
    {
        await Conectar();

        // O ganho e o passa-baixa não mudam o limite (o EQ e o ganho não são vigiados).
        Assert.True((await Escrever(Ganho, 0, 6.0m)).Ok);
        Assert.True((await Escrever(Catalogo.ParametroDe("041")!, 0, 300m)).Ok);
    }

    [Fact]
    public async Task Aplicar_uma_memoria_com_o_limitador_acima_do_teto_nao_envia_nada_e_falha_com_a_mensagem()
    {
        await Conectar();
        Assert.True((await Escrever(Ganho, 0, 1.0m)).Ok); // algo diferente além do limitador
        var inicio = _simulador.Escritas.Count;

        var r = await Rodar(_setups.AplicarAsync(IdAlta));

        Assert.False(r.Ok);
        Assert.StartsWith("Limite: JBL L, ", r.Mensagem);
        Assert.EndsWith("dB, pelo curso do cone.", r.Mensagem);
        Assert.Empty(AudioDesde(inicio));
        Assert.Null(_copia.PelaMetade); // nada pela metade
        Assert.Null(_copia.Ultima?.Operacao);
        Assert.Equal("-15.0", _simulador.Valor("071", 0));
        Assert.Equal("+1.0", _simulador.Valor("081", 0));
    }

    [Fact]
    public async Task Aplicar_uma_memoria_dentro_do_teto_escreve_o_lote_inteiro()
    {
        await Conectar();
        Assert.True((await Escrever(Limitador, 0, -20.0m)).Ok);

        var r = await Rodar(_setups.AplicarAsync(CenarioFilme.IdMemoria));

        Assert.True(r.Ok, r.Mensagem);
        Assert.Equal("-15.0", _simulador.Valor("071", 0));
    }

    [Fact]
    public async Task Sem_calibracao_a_cerca_deixa_passar()
    {
        using var t = new CercaTestes(Dados(CasaDoDenis.EquipamentosSemCalibracao));
        await t.Conectar();

        var r = await t.Escrever(Limitador, 0, Cerca.LimitadorOff);

        Assert.True(r.Ok, r.Motivo);
        Assert.Equal("+0.5", t._simulador.Valor("071", 0));
        var jbl = t._setups.LimitesEmUso.Single(l => l.Nome == "JBL L");
        Assert.False(jbl.Calibrada);
        Assert.Null(jbl.TetoDb);
        Assert.NotNull(jbl.TetoV);
    }

    [Fact]
    public async Task Os_limites_em_uso_seguem_o_corte_do_espelho()
    {
        await Conectar();
        var antes = _setups.LimitesEmUso.Single(l => l.Nome == "JBL L");
        Assert.Equal(TipoLimite.Excursao, antes.Manda); // LR24 em 150 Hz na FILME

        await JblLEm200Hz();
        var depois = _setups.LimitesEmUso.Single(l => l.Nome == "JBL L");

        Assert.Equal(TipoLimite.Termico, depois.Manda);
        Assert.Equal(-6.8m, depois.TetoDb);
        Assert.Equal(["JBL L", "JBL R", "Sub"], _setups.LimitesEmUso.Select(l => l.Nome));
    }

    [Fact]
    public async Task Filtro_do_passa_alta_em_OFF_baixa_o_X_e_e_recusado_com_o_que_fazer()
    {
        await Conectar();
        var inicio = _simulador.Escritas.Count;

        // O limitador da JBL L em −15 dB (FILME); sem passa-alta, o curso do cone baixa o teto para perto de −19 dB.
        var r = await Escrever(Catalogo.ParametroDe("042")!, 0, 0m);

        Assert.False(r.Ok);
        Assert.StartsWith("Limite: JBL L, −1", r.Motivo);
        Assert.EndsWith("dB, pelo curso do cone. Baixe o limitador antes de mudar o corte.", r.Motivo);
        Assert.Empty(AudioDesde(inicio));
        Assert.Equal("3", _simulador.Valor("042", 0));
    }

    [Fact]
    public async Task Uma_falha_no_calculo_recusa_com_a_mensagem_e_nada_sai()
    {
        await Conectar();
        _escritor.Cerca = _ => throw new InvalidOperationException("teste");
        var inicio = _simulador.Escritas.Count;

        var r = await Escrever(Limitador, 0, -20.0m);

        Assert.False(r.Ok);
        Assert.Equal(Cerca.MsgFalhou, r.Motivo);
        Assert.Empty(AudioDesde(inicio));
    }

    [Fact]
    public async Task Voltar_a_copia_com_o_limitador_da_JBL_L_em_OFF_volta_no_teto_e_o_resto_igual_a_copia()
    {
        await Conectar();
        // O som de antes (de fora do app) tinha o limitador da JBL L desligado.
        _simulador.Definir("071", 0, "+0.5");
        await Reler();
        Assert.Equal(Cerca.LimitadorOff, _estado.Valor("071", 0)!.Numero);
        var ganhoAntes = _simulador.Valor("081", 0);
        var teto = _setups.LimitesEmUso.Single(l => l.Nome == "JBL L").TetoCerca!.Value;

        // Uma operação mexe no limitador e no ganho e fica pela metade.
        Assert.True((await Rodar(_copia.IniciarAsync(new OperacaoPendente("teste-13a", "x", "Teste da volta")))).Ok);
        using (Origem.Como(OrigemComando.App))
        {
            Assert.True((await Rodar(_escritor.EscreverAsync(Limitador, 0, -20.0m, espelhar: false, daCopia: true))).Ok);
            Assert.True((await Rodar(_escritor.EscreverAsync(Ganho, 0, 1.0m, espelhar: false, daCopia: true))).Ok);
        }
        _copia.MarcarPelaMetade();
        Assert.NotNull(_copia.PelaMetade);

        ResultadoCopia r;
        using (Origem.Como(OrigemComando.App))
            r = await Rodar(_copia.VoltarAsync());

        Assert.True(r.Ok, r.Motivo);
        Assert.Equal(teto, _estado.Valor("071", 0)!.Numero); // no teto, e não no OFF da cópia
        Assert.Equal(teto, Limitador.Decodificar(_simulador.Valor("071", 0)!).Valor!.Numero);
        Assert.Equal(ganhoAntes, _simulador.Valor("081", 0)); // o resto igual à cópia
        Assert.Null(_copia.PelaMetade);
        Assert.Null(_copia.Ultima!.Operacao); // a marca sai
        Assert.Contains(_log.Linhas, l => l.Contains("o limitador de JBL L volta no teto", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Escolher_o_Setup_em_duvida_com_a_memoria_ativa_acima_do_teto_nao_envia_nada_e_falha_com_a_mensagem()
    {
        using var t = new CercaTestes(Dados(CasaDoDenis.EquipamentosCasa, ativa: IdAlta));
        t._simulador.Definir("050", 2, "0.0"); // um código que muta: o Setup fica em dúvida
        await t.Conectar(emUso: false);
        Assert.Equal(EstadoConferencia.Duvida, t._setups.Estado);
        var inicio = t._simulador.Escritas.Count;

        var r = await t.Rodar(t._setups.EscolherAsync(CasaDoDenis.IdSetupCasa));

        Assert.False(r.Ok);
        Assert.StartsWith("Limite: JBL L, ", r.Mensagem);
        Assert.EndsWith("dB, pelo curso do cone.", r.Mensagem);
        Assert.Empty(t.AudioDesde(inicio));
        Assert.Null(t._copia.Ultima?.Operacao);
        Assert.Equal("-15.0", t._simulador.Valor("071", 0));
    }

    [Fact]
    public async Task A_reaplicacao_na_conferencia_acima_do_teto_nao_envia_nada()
    {
        using var t = new CercaTestes(Dados(CasaDoDenis.EquipamentosCasa, ativa: IdAlta));

        await t.Conectar();

        // A ALTA (ativa) difere só no limitador da JBL L (−3 dB): a reaplicação sozinha é recusada pela cerca.
        Assert.DoesNotContain(t._simulador.Escritas, e => e.StartsWith("*071*", StringComparison.Ordinal));
        Assert.Equal("-15.0", t._simulador.Valor("071", 0));
        Assert.Null(t._copia.Ultima?.Operacao);
        Assert.Contains(t._log.Linhas, l => l.Contains("recusado pela cerca", StringComparison.Ordinal));
    }

    /// <summary>
    /// O som de antes (de fora do app) tinha o limitador da JBL L desligado; uma operação guarda a cópia e
    /// escreve o limitador em −20 dB (a marca fica, com a operação andando).
    /// </summary>
    private async Task CopiaComAJblLEmOffEUmaOperacao()
    {
        _simulador.Definir("071", 0, "+0.5");
        await Reler();
        Assert.Equal(Cerca.LimitadorOff, _estado.Valor("071", 0)!.Numero);
        Assert.True((await Rodar(_copia.IniciarAsync(new OperacaoPendente("teste-13a", "x", "Teste da volta")))).Ok);
        using (Origem.Como(OrigemComando.App))
            Assert.True((await Rodar(_escritor.EscreverAsync(Limitador, 0, -20.0m, espelhar: false, daCopia: true))).Ok);
    }

    private async Task<ResultadoCopia> Voltar()
    {
        using (Origem.Como(OrigemComando.App))
            return await Rodar(_copia.VoltarAsync());
    }

    [Fact]
    public async Task Duas_escritas_pendentes_o_corte_que_sai_depois_do_limitador_e_recusado_na_hora_do_envio()
    {
        await Conectar();
        await JblLEm200Hz(); // o passa-alta acabou de ser escrito: o próximo espera o arrasto, e o limitador sai antes

        Task<ResultadoEscrita> limitador, corte;
        using (Origem.Como(OrigemComando.App))
        {
            limitador = _escritor.EscreverAsync(Limitador, 0, -6.8m);
            corte = _escritor.EscreverAsync(PassaAlta, 0, 150m);
        }
        var rLimitador = await Rodar(limitador);
        var rCorte = await Rodar(corte);

        Assert.True(rLimitador.Ok, rLimitador.Motivo);
        Assert.False(rCorte.Ok);
        Assert.EndsWith("Baixe o limitador antes de mudar o corte.", rCorte.Motivo);
        Assert.Equal("-6.8", _simulador.Valor("071", 0));
        Assert.Equal("200", _simulador.Valor("040", 0));
    }

    [Fact]
    public async Task Aplicar_uma_memoria_que_baixa_o_corte_e_o_limitador_juntos_escreve_os_dois_com_o_limitador_antes()
    {
        await Conectar();
        await JblLEm200Hz();
        Assert.True((await Escrever(Limitador, 0, -6.8m)).Ok);
        var inicio = _simulador.Escritas.Count;

        // A FILME tem a JBL L em 150 Hz e −15 dB.
        var r = await Rodar(_setups.AplicarAsync(CenarioFilme.IdMemoria));

        Assert.True(r.Ok, r.Mensagem);
        Assert.Equal("150", _simulador.Valor("040", 0));
        Assert.Equal("-15.0", _simulador.Valor("071", 0));
        var audio = AudioDesde(inicio);
        Assert.True(audio.FindIndex(e => e.StartsWith("*071*0*", StringComparison.Ordinal))
                    < audio.FindIndex(e => e.StartsWith("*040*0*", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Uma_falha_no_calculo_da_volta_nao_envia_nada_e_a_marca_fica()
    {
        await Conectar();
        await CopiaComAJblLEmOffEUmaOperacao();
        _copia.MarcarPelaMetade();
        _escritor.CercaNaVolta = _ => throw new InvalidOperationException("teste");
        var inicio = _simulador.Escritas.Count;

        var r = await Voltar();

        Assert.False(r.Ok);
        Assert.Equal(Cerca.MsgFalhou, r.Motivo);
        Assert.Empty(AudioDesde(inicio));
        Assert.NotNull(_copia.PelaMetade);
        Assert.NotNull(_copia.Ultima!.Operacao);
        Assert.Equal("-20.0", _simulador.Valor("071", 0));
    }

    [Fact]
    public async Task Desfazer_com_o_limitador_da_copia_em_OFF_volta_no_teto()
    {
        await Conectar();
        var teto = _setups.LimitesEmUso.Single(l => l.Nome == "JBL L").TetoCerca!.Value;
        await CopiaComAJblLEmOffEUmaOperacao();

        ResultadoCopia r;
        using (Origem.Como(OrigemComando.App))
            r = await Rodar(_copia.DesfazerAsync());

        Assert.True(r.Ok, r.Motivo);
        Assert.Equal(teto, _estado.Valor("071", 0)!.Numero);
        Assert.Null(_copia.Ultima!.Operacao);
        Assert.Null(_copia.PelaMetade);
    }

    [Fact]
    public async Task Na_volta_um_teto_fisico_acima_de_0_dB_escreve_o_limitador_em_0()
    {
        // O ganho do módulo medido em ×1: a JBL aguenta mais do que o limitador alcança (teto acima de 0 dB).
        var lista = CasaDoDenis.EquipamentosCasa
            .Select(e => e.Id == CasaDoDenis.IdModuloCasa
                ? e with { Dados = e.Dados with { Ganho = new DadoTecnico(1m, OrigemDado.Medido) } }
                : e)
            .ToArray();
        using var t = new CercaTestes(Dados(lista));
        await t.Conectar();
        var jbl = t._setups.LimitesEmUso.Single(l => l.Nome == "JBL L");
        Assert.True(jbl.TetoDb > 0m);
        Assert.Equal(0m, jbl.TetoCerca);
        await t.CopiaComAJblLEmOffEUmaOperacao();
        t._copia.MarcarPelaMetade();

        var r = await t.Voltar();

        Assert.True(r.Ok, r.Motivo);
        Assert.Equal(0m, t._estado.Valor("071", 0)!.Numero);
        Assert.Null(t._copia.PelaMetade);
    }
}
