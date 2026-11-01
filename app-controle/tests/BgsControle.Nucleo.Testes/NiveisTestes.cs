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
/// História 13c, os níveis Baixo a Máximo contra o processador simulado: o Casa 2.1 da fixture calibrado, com o
/// Bluetooth em −13 dB, e a memória PLANA em uso (o ganho +9 nas JBL, LR24 em 200 Hz, lembrando o preset Plano). Uma
/// linha da matriz por teste, com os números da JBL dentro de 0,2 dB; o <see cref="GerenteNiveis"/> refaz a conta na
/// hora.
/// </summary>
public sealed class NiveisTestes : IDisposable
{
    private static readonly TemposSessao Tempos = new(
        Scan: TimeSpan.FromSeconds(1),
        TentativaConexao: TimeSpan.FromSeconds(1),
        PausaEntreTentativas: TimeSpan.FromMilliseconds(1),
        RespostaSenha: TimeSpan.FromSeconds(2),
        PrazoSenha: TimeSpan.FromSeconds(60));

    private static readonly TemposLeitura LeituraRapida = new(TimeSpan.Zero, TimeSpan.FromMilliseconds(100));
    private static readonly Regex Senha = new(@"^\*000\*[0-9A-Z]{4}\*$");

    private const string IdPlana = "memoria-plana";
    private const string IdPlano = "preset-plano";
    private const string IdGrave = "preset-grave";
    private const decimal Bluetooth = -13m;
    private const decimal GanhoModulo = 19.5m;
    private const double TermicoJbl = 17.9;

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
    private readonly GerenteNiveis? _niveis;

    private static Parametro Master => Catalogo.ParametroDe("080")!;
    private static Parametro Limitador => Catalogo.ParametroDe("071")!;
    private static Parametro EqSaidaGanho => Catalogo.ParametroDe("091")!;

    public NiveisTestes() : this(CasaDoDenis.SetupCasaComBluetooth)
    {
    }

    /// <param name="nivel">O nível guardado na PLANA (com o MASTER da regulagem em 0).</param>
    /// <param name="comNiveis">Com o <see cref="GerenteNiveis"/> (a conta refeita na hora).</param>
    /// <param name="limitadorJblL">O limitador da JBL L na PLANA (e no aparelho), ou null (o da leitura de 18/10).</param>
    private NiveisTestes(Setup setup, decimal ganhoJbl = 9m, NivelRendimento? nivel = null, bool comNiveis = true, string? limitadorJblL = null)
    {
        var regulagem = PiorCasoTestes.Plana(ganhoJbl)
            .Where(v => Camadas.De(v.Codigo) == Camada.Regulagem)
            .Select(v => limitadorJblL is not null && v is { Codigo: "071", Indice: 0 } ? v with { NoFio = limitadorJblL } : v)
            .ToArray();
        var plano = new Preset(IdPlano, "Plano", CasaDoDenis.QuandoFilme, GerentePresets.BandasPlanas);
        var grave = new Preset(IdGrave, "Grave", CasaDoDenis.QuandoFilme,
            GerentePresets.BandasPlanas.Select(v => v.Indice == 5 ? v with { NoFio = "+4.0" } : v).ToArray());
        var memoria = new MemoriaCelular(IdPlana, CasaDoDenis.IdSetupCasa, "PLANA", CasaDoDenis.QuandoFilme, regulagem, IdPlano) { Nivel = nivel };
        var dados = new DadosSetups([setup], [memoria], CasaDoDenis.IdSetupCasa,
            new Dictionary<string, UsoDoSetup> { [CasaDoDenis.IdSetupCasa] = new([IdPlana], [IdPlana, null, null]) });

        _simulador = new ProcessadorSimulado(_relogio) { AtrasoSenha = TimeSpan.Zero, AtrasoLeitura = TimeSpan.Zero };
        // O aparelho já está na PLANA com o Plano: a conferência confere.
        foreach (var v in regulagem.Concat(plano.Valores))
            _simulador.Definir(v.Codigo, v.Indice, v.NoFio);
        _chaves = new Chaves(new RepositorioMemoria());
        _sessao = new Sessao(_simulador, new CofreFalso { Senha = "AB12" }, _log, Tempos, _chaves);
        _leitura = new LeituraAutomatica(_sessao, new LeitorCompleto(_sessao, _log, LeituraRapida));
        _estado = new EstadoAparelho(_sessao, _leitura);
        _escritor = new Escritor(_sessao, _estado, _log, relogio: _relogio);
        _copia = new CopiaAntes(_sessao, _estado, _escritor, _leitura, new TextoMemoria(), _log, _relogio);
        _link = new LinkCanais(_sessao, _estado, _escritor, new TextoMemoria(), _log);
        _presets = new GerentePresets(_sessao, _estado, _escritor, _leitura, _copia, new TextoMemoria(GerentePresets.ParaJson([plano, grave])),
            _log, _relogio);
        _setups = new GerenteSetups(_sessao, _estado, _escritor, _leitura, _copia, _link, _presets, new GerentePlaylists(new TextoMemoria(), _log),
            _chaves, new TextoMemoria(GerenteSetups.ParaJson(dados)), new TextoMemoria(), _log, _relogio);
        _niveis = comNiveis ? new GerenteNiveis(_estado, _escritor, _setups, _presets, _copia, _log) : null;
    }

    /// <summary>Critério de aceite: nada além do catálogo chega ao simulador; nenhum 240, 241 ou 23N.</summary>
    public void Dispose()
    {
        Assert.DoesNotContain(_simulador.Escritas, e => e.StartsWith("*24", StringComparison.Ordinal) || e.StartsWith("*23", StringComparison.Ordinal));
        Assert.All(_simulador.Escritas, e => Assert.True(Senha.IsMatch(e) || Catalogo.EhLeitura(e) || Catalogo.EscritaDeAudio(e) is not null, e));
    }

    // ----------------------------------------------------------------- apoio

    private async Task Conectar()
    {
        await _chaves.Carregada;
        await _copia.Carregada;
        await _link.Carregada;
        await _presets.Carregada;
        await _setups.Carregada;
        await _sessao.ConectarAsync();
        Assert.Equal(EstadoSessao.Conectado, _sessao.Estado);
        await Calmo();
        Assert.Equal(CasaDoDenis.IdSetupCasa, _setups.SetupEmUso?.Id);
        Assert.Equal(IdPlana, _setups.MemoriaEmUso?.Id);
    }

    /// <summary>Anda até a leitura, a cópia, o Setup e a conta do nível pararem.</summary>
    private Task Calmo() =>
        AndarAte(() => _estado.Pronto && !_estado.Lendo && !_leitura.Lendo && !_copia.EmAndamento && _setups.Conferido && !_setups.Ocupado
                       && !_presets.Ocupado && _niveis?.Refazendo != true);

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

    private async Task<ResultadoEscrita> Escrever(Parametro p, int? indice, decimal valor, OrigemComando origem = OrigemComando.App)
    {
        using (Origem.Como(origem))
            return await Rodar(_escritor.EscreverAsync(p, indice, valor));
    }

    private async Task Nivel(NivelRendimento? nivel)
    {
        var r = await Rodar(_setups.DefinirNivelAsync(IdPlana, nivel));
        Assert.True(r.Ok, r.Motivo);
        await Calmo();
    }

    /// <summary>Anda o relógio sem esperar condição (a conta na hora tem tempo de rodar).</summary>
    private async Task Andar(int passos = 100)
    {
        for (var i = 0; i < passos; i++)
        {
            _relogio.Avancar(TimeSpan.FromMilliseconds(10));
            await Task.Delay(1);
        }
    }

    private decimal MasterNoAparelho => decimal.Parse(_simulador.Valor("080")!, System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>O pior caso da JBL L agora (o master do aparelho), em dB relativo a <paramref name="volts"/> − margem.</summary>
    private double ErroDaJbl(double volts, double margem)
    {
        var db = PiorCaso.ComMaster(_estado.Valor, 0, Bluetooth)!.Value;
        return 20 * Math.Log10(PiorCaso.Volts(db, GanhoModulo) / (volts * Math.Pow(10, -margem / 20)));
    }

    private List<string> AudioDesde(int inicio) =>
        _simulador.Escritas.Skip(inicio).Where(e => Catalogo.EscritaDeAudio(e) is not null).ToList();

    // ----------------------------------------------------------------- a matriz

    [Theory]
    [InlineData(NivelRendimento.Baixo, 6.0, "-8.8")]
    [InlineData(NivelRendimento.Medio, 3.0, "-5.8")]
    [InlineData(NivelRendimento.Alto, 1.0, "-3.8")]
    public async Task JBL_do_Baixo_ao_Alto_fica_no_termico_menos_a_margem_e_o_master_vai_ao_aparelho(NivelRendimento nivel, double margem,
        string master)
    {
        await Conectar();

        await Nivel(nivel);

        Assert.Equal(master, _simulador.Valor("080"));
        Assert.InRange(ErroDaJbl(TermicoJbl, margem), -0.2, 0.2);
        var memoria = _setups.MemoriaEmUso!;
        Assert.Equal(nivel, memoria.Nivel);
        Assert.Equal(MasterNoAparelho, memoria.Masters[IdPlano]);
        Assert.Equal(master, memoria.Regulagem.Single(v => v.Codigo == "080").NoFio);
    }

    [Fact]
    public async Task Maximo_fica_no_menor_de_curso_e_ceifamento_e_a_cerca_vai_a_min_X_C()
    {
        await Conectar();
        await Nivel(NivelRendimento.Alto);
        // No Alto, o limitador da JBL L em −5,0 dB passa do térmico (−6,8): a cerca recusa.
        var alto = await Escrever(Limitador, 0, -5.0m);
        Assert.False(alto.Ok);
        Assert.Equal("Limite: JBL L, −6,8 dB, pelo térmico.", alto.Motivo);

        await Nivel(NivelRendimento.Maximo);

        Assert.InRange(ErroDaJbl(Math.Min(22.2, 23.0), 1.0), -0.2, 0.2);
        Assert.Equal(NivelRendimento.Maximo, _setups.NivelEmUso);
        // No Máximo, o teto da cerca é min(X, C) (o curso do cone, uns 22 V): −5,0 passa e 0,1 dB acima do teto não.
        var jbl = _setups.LimitesEmUso.Single(l => l.Nome == "JBL L");
        Assert.Equal(TipoLimite.Excursao, jbl.Manda);
        var teto = jbl.TetoCerca!.Value;
        Assert.InRange(teto, -5.0m, -4.6m);
        Assert.True((await Escrever(Limitador, 0, -5.0m)).Ok);
        var acima = await Escrever(Limitador, 0, teto + 0.1m);
        Assert.False(acima.Ok);
        Assert.Equal($"Limite: JBL L, {LimiteFisico.Db(teto)} dB, pelo curso do cone.", acima.Motivo);
        Assert.Equal("-5.0", _simulador.Valor("071", 0));
    }

    [Fact]
    public async Task Reforco_de_3_dB_no_EQ_de_saida_da_JBL_baixa_o_master_3_dB_na_hora()
    {
        await Conectar();
        await Nivel(NivelRendimento.Alto);
        var antes = MasterNoAparelho;

        Assert.True((await Escrever(EqSaidaGanho, 0, 3.0m, OrigemComando.Tela)).Ok);
        await AndarAte(() => MasterNoAparelho < antes);
        await Calmo();

        Assert.InRange(antes - MasterNoAparelho, 2.9m, 3.1m);
        Assert.InRange(ErroDaJbl(TermicoJbl, 1.0), -0.2, 0.2);
        Assert.Equal(MasterNoAparelho, _setups.MemoriaEmUso!.Masters[IdPlano]);
    }

    [Fact]
    public async Task Trocar_o_preset_poe_o_master_da_combinacao_nova_e_guarda_os_dois()
    {
        await Conectar();
        await Nivel(NivelRendimento.Alto);
        var comPlano = MasterNoAparelho;

        var r = await Rodar(_presets.AplicarAsync(IdGrave));
        Assert.True(r.Ok, r.Motivo);
        await Calmo();

        Assert.Equal("+4.0", _simulador.Valor("020", 5));
        Assert.Equal(_setups.Conta(NivelRendimento.Alto)!.Master, MasterNoAparelho);
        Assert.True(MasterNoAparelho < comPlano);
        Assert.InRange(ErroDaJbl(TermicoJbl, 1.0), -0.2, 0.2);
        var memoria = _setups.MemoriaEmUso!;
        Assert.Equal(IdGrave, memoria.IdPreset);
        Assert.Equal(comPlano, memoria.Masters[IdPlano]);
        Assert.Equal(MasterNoAparelho, memoria.Masters[IdGrave]);
    }

    [Fact]
    public async Task Aplicar_a_memoria_num_nivel_escreve_o_master_da_conta_e_nao_o_guardado()
    {
        // A PLANA no Baixo, com o MASTER guardado em 0 dB, sem a conta na hora: só o aplicar põe o master.
        using var t = new NiveisTestes(CasaDoDenis.SetupCasaComBluetooth, nivel: NivelRendimento.Baixo, comNiveis: false);
        await t.Conectar();
        Assert.Equal("+0.0", t._simulador.Valor("080"));

        var r = await t.Rodar(t._setups.AplicarAsync(IdPlana));
        await t.Calmo();

        Assert.True(r.Ok, r.Motivo);
        Assert.Equal("-8.8", t._simulador.Valor("080"));
        Assert.InRange(t.ErroDaJbl(TermicoJbl, 6.0), -0.2, 0.2);
        var memoria = t._setups.MemoriaEmUso!;
        Assert.Equal(-8.8m, memoria.Masters[IdPlano]);
        Assert.Equal("-8.8", memoria.Regulagem.Single(v => v.Codigo == "080").NoFio);
    }

    [Fact]
    public async Task Sem_calibracao_os_niveis_nao_sao_oferecidos_e_nada_sai()
    {
        using var t = new NiveisTestes(CasaDoDenis.SetupCasaComBluetooth with { Equipamentos = CasaDoDenis.EquipamentosSemCalibracao });
        await t.Conectar();
        var inicio = t._simulador.Escritas.Count;

        var r = await t.Rodar(t._setups.DefinirNivelAsync(IdPlana, NivelRendimento.Alto));

        Assert.False(r.Ok);
        Assert.Equal("Calibre a saída JBL L.", r.Motivo);
        Assert.Equal("Calibre a saída JBL L.", t._setups.NivelIndisponivel);
        Assert.Null(t._setups.MemoriaEmUso!.Nivel);
        Assert.Empty(t.AudioDesde(inicio));
    }

    [Fact]
    public async Task Sem_o_Bluetooth_medido_os_niveis_nao_sao_oferecidos()
    {
        using var t = new NiveisTestes(CasaDoDenis.SetupCasa);
        await t.Conectar();
        var inicio = t._simulador.Escritas.Count;

        var r = await t.Rodar(t._setups.DefinirNivelAsync(IdPlana, NivelRendimento.Baixo));

        Assert.False(r.Ok);
        Assert.Equal("Meça o nível do Bluetooth.", r.Motivo);
        Assert.Null(t._setups.Conta(NivelRendimento.Baixo));
        Assert.Empty(t.AudioDesde(inicio));
    }

    [Fact]
    public async Task Master_acima_da_conta_e_recusado_pela_tela_e_pela_porta_sem_envio_e_abaixo_passa()
    {
        await Conectar();
        await Nivel(NivelRendimento.Alto);
        Assert.Equal(-3.8m, MasterNoAparelho);
        var inicio = _simulador.Escritas.Count;

        var tela = await Escrever(Master, null, -3.3m, OrigemComando.Tela);
        var porta = await Escrever(Master, null, -3.3m, OrigemComando.Ia);

        Assert.False(tela.Ok);
        Assert.Equal("Nível Alto: o master vai até −3,8 dB.", tela.Motivo);
        Assert.False(porta.Ok);
        Assert.Equal("Nível Alto: o master vai até −3,8 dB.", porta.Motivo);
        Assert.Empty(AudioDesde(inicio));
        Assert.Equal(-3.8m, MasterNoAparelho);

        var abaixo = await Escrever(Master, null, -10.0m, OrigemComando.Tela);

        Assert.True(abaixo.Ok, abaixo.Motivo);
        Assert.Equal(-10.0m, MasterNoAparelho);
    }

    [Fact]
    public async Task Sem_ganho_na_memoria_o_master_fica_em_0_e_o_nivel_avisa()
    {
        using var t = new NiveisTestes(CasaDoDenis.SetupCasaComBluetooth, ganhoJbl: 0m);
        await t.Conectar();
        t._simulador.Definir("080", null, "-10.0");
        Assert.True((await t.Rodar(t._leitura.LerNovaAsync()))?.Completo);
        await t.Calmo();

        var r = await t.Rodar(t._setups.DefinirNivelAsync(IdPlana, NivelRendimento.Alto));
        await t.Calmo();

        Assert.True(r.Ok, r.Motivo);
        Assert.Equal("Nível Alto na PLANA. O nível não chega ao teto: falta ganho na memória. Saída: JBL L.", r.Mensagem);
        Assert.Equal("+0.0", t._simulador.Valor("080"));
    }

    [Fact]
    public async Task Ficha_que_baixa_o_Xmax_da_JBL_refaz_o_master_na_hora()
    {
        await Conectar();
        await Nivel(NivelRendimento.Alto);
        var antes = MasterNoAparelho;
        var jbl = CasaDoDenis.EquipamentoCasa(CasaDoDenis.IdJblL)!;

        var r = await Rodar(_setups.EditarEquipamentoAsync(CasaDoDenis.IdSetupCasa,
            jbl with { Dados = jbl.Dados with { Xmax = new DadoTecnico(0.5m, OrigemDado.Oficial, "Ficha nova") } }));
        Assert.True(r.Ok, r.Motivo);
        await AndarAte(() => MasterNoAparelho < antes);
        await Calmo();

        var conta = _setups.Conta(NivelRendimento.Alto)!;
        Assert.Equal(conta.Master, MasterNoAparelho);
        Assert.Equal(TipoLimite.Excursao, conta.QueManda!.Manda);
        Assert.Equal(["JBL L"], conta.SaidasQueMandam);
    }

    [Fact]
    public async Task Sem_nivel_volta_a_null_sem_mexer_no_master()
    {
        await Conectar();
        await Nivel(NivelRendimento.Medio);
        var master = _simulador.Valor("080");
        var inicio = _simulador.Escritas.Count;

        await Nivel(null);

        Assert.Null(_setups.MemoriaEmUso!.Nivel);
        Assert.Null(_setups.NivelEmUso);
        Assert.Equal(master, _simulador.Valor("080"));
        Assert.Empty(AudioDesde(inicio));
        // Sem nível, o master é do usuário: subir passa, e a conta não o devolve.
        Assert.True((await Escrever(Master, null, 0m, OrigemComando.Tela)).Ok);
        await Calmo();
        Assert.Equal("+0.0", _simulador.Valor("080"));
    }

    [Fact]
    public async Task O_corte_no_EQ_de_saida_sobe_o_master_de_volta_na_hora()
    {
        await Conectar();
        await Nivel(NivelRendimento.Alto);
        Assert.True((await Escrever(EqSaidaGanho, 0, 3.0m, OrigemComando.Tela)).Ok);
        await AndarAte(() => MasterNoAparelho == -6.8m);

        Assert.True((await Escrever(EqSaidaGanho, 0, 0m, OrigemComando.Tela)).Ok);
        await AndarAte(() => MasterNoAparelho == -3.8m);
        await Calmo();

        Assert.Equal(-3.8m, MasterNoAparelho);
    }

    [Fact]
    public async Task O_master_baixado_a_mao_fica_num_aviso_que_nao_muda_a_cadeia_e_a_cadeia_o_move_pelo_mesmo_tanto()
    {
        await Conectar();
        await Nivel(NivelRendimento.Alto);
        Assert.True((await Escrever(Master, null, -10.0m, OrigemComando.Tela)).Ok);
        Assert.Equal(6.2m, _setups.CorteDoMaster);

        // Avisos que não mudam a cadeia: o Setup, a leitura e a conta pedida de novo.
        Assert.True((await Rodar(_setups.DefinirAmbienteAsync(CasaDoDenis.IdSetupCasa, AmbienteSetup.Sala))).Ok);
        Assert.True((await Rodar(_leitura.LerNovaAsync()))?.Completo);
        _niveis!.Pedir();
        await Calmo();
        Assert.Equal(-10.0m, MasterNoAparelho);

        // A cadeia ainda move o master, com o corte do usuário: +3 no EQ baixa 3 dB; voltar o EQ sobe 3 dB.
        Assert.True((await Escrever(EqSaidaGanho, 0, 3.0m, OrigemComando.Tela)).Ok);
        await AndarAte(() => MasterNoAparelho == -13.0m);
        Assert.True((await Escrever(EqSaidaGanho, 0, 0m, OrigemComando.Tela)).Ok);
        await AndarAte(() => MasterNoAparelho == -10.0m);
        await Calmo();
        Assert.Equal(-10.0m, MasterNoAparelho);

        // Escolher o nível de novo zera o corte.
        await Nivel(NivelRendimento.Alto);
        Assert.Equal(0m, _setups.CorteDoMaster);
        Assert.Equal(-3.8m, MasterNoAparelho);
    }

    [Fact]
    public async Task Trocar_o_preset_que_baixa_o_master_escreve_o_master_antes_das_bandas_e_guarda_o_master_do_preset()
    {
        using var t = new NiveisTestes(CasaDoDenis.SetupCasaComBluetooth, comNiveis: false);
        await t.Conectar();
        await t.Nivel(NivelRendimento.Alto);
        var inicio = t._simulador.Escritas.Count;

        var r = await t.Rodar(t._presets.AplicarAsync(IdGrave));
        await t.Calmo();

        Assert.True(r.Ok, r.Motivo);
        var audio = t.AudioDesde(inicio);
        var master = audio.FindIndex(e => e.StartsWith("*080*", StringComparison.Ordinal));
        var banda = audio.FindIndex(e => Catalogo.EscritaDeAudio(e)?.Parametro.Codigo == "020");
        Assert.True(master >= 0 && banda >= 0 && master < banda, string.Join(" ", audio));
        Assert.True(t.MasterNoAparelho < -3.8m);
        Assert.Equal(t.MasterNoAparelho, t._setups.MemoriaEmUso!.Masters[IdGrave]);
    }

    [Fact]
    public async Task Aplicar_uma_memoria_no_Maximo_com_o_limitador_acima_do_termico_passa_e_a_volta_mantem_o_limitador()
    {
        using var t = new NiveisTestes(CasaDoDenis.SetupCasaComBluetooth, nivel: NivelRendimento.Maximo, comNiveis: false, limitadorJblL: "-5.0");
        await t.Conectar();
        // O limitador mexido por fora: o aplicar o devolve a −5,0 dB (acima do térmico, abaixo de min(X, C)).
        t._simulador.Definir("071", 0, "-15.0");
        Assert.True((await t.Rodar(t._leitura.LerNovaAsync()))?.Completo);
        await t.Calmo();

        var r = await t.Rodar(t._setups.AplicarAsync(IdPlana));
        await t.Calmo();

        Assert.True(r.Ok, r.Motivo);
        Assert.Equal("-5.0", t._simulador.Valor("071", 0));
        var limitador = Catalogo.ParametroDe("071")!.Decodificar("-5.0").Valor;
        var setup = t._setups.SetupEmUso!;
        var volta = new[] { (new Endereco("071", 0), limitador) };
        Assert.Empty(Cerca.NaVolta(setup.Equipamentos, t._setups.SaidasEmUso, t._estado.Copiar(), volta, NivelRendimento.Maximo));
        Assert.Contains(Cerca.NaVolta(setup.Equipamentos, t._setups.SaidasEmUso, t._estado.Copiar(), volta),
            v => v.Endereco == new Endereco("071", 0) && v.Teto == -6.8m);
    }

    [Fact]
    public async Task Sair_do_Maximo_com_o_limitador_acima_do_termico_e_recusado()
    {
        using var t = new NiveisTestes(CasaDoDenis.SetupCasaComBluetooth, nivel: NivelRendimento.Maximo, comNiveis: false, limitadorJblL: "-5.0");
        await t.Conectar();

        var r = await t.Rodar(t._setups.DefinirNivelAsync(IdPlana, NivelRendimento.Alto));
        var semNivel = await t.Rodar(t._setups.DefinirNivelAsync(IdPlana, null));

        Assert.False(r.Ok);
        Assert.Equal("Baixe o limitador da JBL L para −6,8 dB antes de sair do Máximo.", r.Motivo);
        Assert.False(semNivel.Ok);
        Assert.Equal(NivelRendimento.Maximo, t._setups.NivelEmUso);
    }

    [Fact]
    public async Task A_conta_na_hora_espera_a_marca_da_copia_sair()
    {
        await Conectar();
        await Nivel(NivelRendimento.Alto);
        Assert.True((await Rodar(_copia.IniciarAsync(new OperacaoPendente("teste-13c", "x", "Teste da 13c")))).Ok);

        // Com a marca, o EQ muda (como numa operação) e o master fica.
        Assert.True((await Rodar(_escritor.EscreverAsync(EqSaidaGanho, 0, 3.0m, espelhar: false, daCopia: true))).Ok);
        await Andar();
        Assert.Equal(-3.8m, MasterNoAparelho);

        await _copia.ConcluirAsync();
        await AndarAte(() => MasterNoAparelho == -6.8m);
        await Calmo();

        Assert.Equal(-6.8m, MasterNoAparelho);
    }

    [Fact]
    public async Task Com_o_nivel_sem_conta_o_master_a_mao_so_desce_e_o_aplicar_usa_o_menor()
    {
        // A PLANA no Alto num Setup sem o Bluetooth medido: o nível não tem conta.
        using var t = new NiveisTestes(CasaDoDenis.SetupCasa, nivel: NivelRendimento.Alto, comNiveis: false);
        await t.Conectar();
        Assert.True((await t.Escrever(Master, null, -12.0m, OrigemComando.Tela)).Ok);

        var acima = await t.Escrever(Master, null, -11.0m, OrigemComando.Tela);
        var abaixo = await t.Escrever(Master, null, -13.0m, OrigemComando.Ia);

        Assert.False(acima.Ok);
        Assert.Equal("Meça o nível do Bluetooth.", acima.Motivo);
        Assert.True(abaixo.Ok, abaixo.Motivo);
        Assert.Equal(-13.0m, t.MasterNoAparelho);

        // O aplicar não sobe ao master guardado (0 dB): fica no de agora.
        var r = await t.Rodar(t._setups.AplicarAsync(IdPlana));
        await t.Calmo();

        Assert.True(r.Ok, r.Motivo);
        Assert.Equal(-13.0m, t.MasterNoAparelho);
    }

    [Fact]
    public async Task O_nivel_e_os_masters_sobrevivem_ao_reabrir_o_app()
    {
        await Conectar();
        await Nivel(NivelRendimento.Alto);

        var json = GerenteSetups.ParaJson(_setups.Dados);
        var relida = GerenteSetups.DeJson(json)!.Memorias.Single();

        Assert.Contains("\"nivel\": \"alto\"", json);
        Assert.Equal(NivelRendimento.Alto, relida.Nivel);
        Assert.Equal(-3.8m, relida.Masters[IdPlano]);
    }
}
