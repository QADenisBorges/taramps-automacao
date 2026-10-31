using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Leitura;
using BgsControle.Nucleo.Player;
using BgsControle.Nucleo.Presets;
using BgsControle.Nucleo.Registro;
using BgsControle.Nucleo.Seguranca;
using BgsControle.Nucleo.Setups;
using BgsControle.Simulador;
using System.Text.RegularExpressions;

namespace BgsControle.Nucleo.Testes;

/// <summary>
/// As histórias 8c, 8d e 8f no núcleo, contra o processador simulado: os Setups (só o físico), as
/// memórias do celular com toda a regulagem, a conferência ao conectar contra a memória ativa (muta só
/// com rota, atraso, fase ou corte diferentes), o Salvar por camadas (regulagem → memória, 15 bandas →
/// preset), o preset lembrado por cada memória e as playlists por memória. O app nasce sem memórias e
/// sem presets; a maior parte dos testes guarda a FILME e o Cinema do <see cref="CenarioFilme"/>, criados
/// da leitura de 18/10 com que o simulador nasce.
/// </summary>
public sealed partial class SetupsTestes : IDisposable
{
    private static readonly TemposSessao Tempos = new(
        Scan: TimeSpan.FromSeconds(1),
        TentativaConexao: TimeSpan.FromSeconds(1),
        PausaEntreTentativas: TimeSpan.FromMilliseconds(1),
        RespostaSenha: TimeSpan.FromSeconds(2),
        PrazoSenha: TimeSpan.FromSeconds(60));

    private static readonly TemposLeitura LeituraRapida = new(TimeSpan.Zero, TimeSpan.FromMilliseconds(100));
    private static readonly Regex Senha = new(@"^\*000\*[0-9A-Z]{4}\*$");

    private readonly RelogioFalso _relogio = new();
    private readonly ProcessadorSimulado _simulador;
    private readonly Chaves _chaves;
    private readonly LogBle _log = new();
    private readonly Sessao _sessao;
    private readonly LeituraAutomatica _leitura;
    private readonly EstadoAparelho _estado;
    private readonly Escritor _escritor;
    private readonly TextoMemoria _guardadoCopia;
    private readonly TextoMemoria _guardadoLink = new();
    private readonly TextoMemoria _guardadoSetups;
    private readonly TextoMemoria _guardadoMudo;
    private readonly TextoMemoria _guardadoPresets;
    private readonly TextoMemoria _guardadoPlaylists;
    private readonly TextoMemoria _guardadoModelos;
    private readonly CopiaAntes _copia;
    private readonly LinkCanais _link;
    private readonly GerentePresets _presets;
    private readonly GerentePlaylists _playlists;
    private readonly BaseModelos _modelos;
    private readonly GerenteSetups _setups;

    private static Parametro Ganho => Catalogo.ParametroDe("081")!;
    private static Parametro Limiter => Catalogo.ParametroDe("071")!;
    private static Parametro Atraso => Catalogo.ParametroDe("050")!;
    private static Parametro Banda => Catalogo.ParametroDe("020")!;

    private static string Casa => CasaDoDenis.IdSetupCasa;
    private static string Filme => CenarioFilme.IdMemoria;
    private static string Cinema => CenarioFilme.IdPreset;

    /// <summary>O Casa 2.1 da fixture com a FILME (ativa), o Cinema e os 3 modelos guardados.</summary>
    public SetupsTestes() : this(CenarioFilme.SetupsJson, null, presetsGuardado: CenarioFilme.PresetsJson, modelosGuardado: CenarioFilme.ModelosJson)
    {
    }

    private SetupsTestes(string? setupsGuardado, string? mudoGuardado, string? playlistsGuardado = null, string? presetsGuardado = null,
        string? copiaGuardada = null, string? modelosGuardado = null)
    {
        _guardadoModelos = new TextoMemoria(modelosGuardado);
        _guardadoCopia = new TextoMemoria(copiaGuardada);
        _guardadoSetups = new TextoMemoria(setupsGuardado);
        _guardadoMudo = new TextoMemoria(mudoGuardado);
        _guardadoPresets = new TextoMemoria(presetsGuardado);
        _guardadoPlaylists = new TextoMemoria(playlistsGuardado);
        _simulador = new ProcessadorSimulado(_relogio) { AtrasoSenha = TimeSpan.Zero, AtrasoLeitura = TimeSpan.Zero };
        _chaves = new Chaves(new RepositorioMemoria());
        _sessao = new Sessao(_simulador, new CofreFalso { Senha = "AB12" }, _log, Tempos, _chaves);
        var leitor = new LeitorCompleto(_sessao, _log, LeituraRapida);
        _leitura = new LeituraAutomatica(_sessao, leitor);
        _estado = new EstadoAparelho(_sessao, _leitura);
        _escritor = new Escritor(_sessao, _estado, _log, relogio: _relogio);
        _copia = new CopiaAntes(_sessao, _estado, _escritor, _leitura, _guardadoCopia, _log, _relogio);
        _link = new LinkCanais(_sessao, _estado, _escritor, _guardadoLink, _log);
        _presets = new GerentePresets(_sessao, _estado, _escritor, _leitura, _copia, _guardadoPresets, _log, _relogio);
        _playlists = new GerentePlaylists(_guardadoPlaylists, _log);
        _modelos = new BaseModelos(_guardadoModelos, _log, _relogio);
        _setups = new GerenteSetups(_sessao, _estado, _escritor, _leitura, _copia, _link, _presets, _playlists, _chaves,
            _guardadoSetups, _guardadoMudo, _log, _relogio, _modelos);
    }

    /// <summary>O app sem nenhum arquivo: a primeira abertura (o app nasce zerado).</summary>
    private static SetupsTestes SemArquivos(string? playlists = null) => new(null, null, playlists);

    /// <summary>Só o Casa 2.1 da fixture (sem memórias) e os 3 modelos dela guardados.</summary>
    private static SetupsTestes SoCasa(string? playlists = null) =>
        new(CasaDoDenis.SetupsJson, null, playlists, modelosGuardado: CasaDoDenis.ModelosJson);

    /// <summary>Critério de aceite: nenhum quadro <c>*240*</c>, <c>*241*</c> ou <c>*23N*</c> chega ao simulador, em nenhum teste.</summary>
    public void Dispose()
    {
        Assert.DoesNotContain(_simulador.Escritas, e => e.StartsWith("*24", StringComparison.Ordinal) || e.StartsWith("*23", StringComparison.Ordinal));
        Assert.All(_simulador.Escritas, e =>
            Assert.True(Senha.IsMatch(e) || Catalogo.EhLeitura(e) || Catalogo.EscritaDeAudio(e) is not null, e));
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
        await AndarAte(() => _estado.Pronto && !_estado.Lendo && !_leitura.Lendo && !_copia.EmAndamento && _setups.Conferido);
    }

    /// <summary>
    /// Conecta com a chave "Aplicar memória do celular" desligada e liga de novo depois da conferência:
    /// numa diferença leve, o app não reaplica sozinho e fica o som do aparelho (<see cref="EstadoConferencia.SomDiferente"/>),
    /// como antes da história 9c.
    /// </summary>
    private async Task ConectarSemReaplicar()
    {
        await _chaves.Carregada;
        Assert.True((await _chaves.DefinirAsync(Chave.AplicarMemoriaCelular, false, OrigemComando.Tela)).Ok);
        await Conectar();
        Assert.True((await _chaves.DefinirAsync(Chave.AplicarMemoriaCelular, true, OrigemComando.Tela)).Ok);
    }

    private async Task Reconectar()
    {
        await _sessao.DesconectarAsync();
        await Conectar();
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

    private List<string> EscritasDesde(int inicio) => _simulador.Escritas.Skip(inicio).ToList();

    private List<string> AudioDesde(int inicio) =>
        EscritasDesde(inicio).Where(e => Catalogo.EscritaDeAudio(e) is not null).ToList();

    private bool Mutados(bool mutado) =>
        Enumerable.Range(0, 4).All(c => _simulador.Valor("100", c) == (mutado ? "1" : "0"));

    /// <summary>A regulagem do aparelho fica diferente da FILME num código que muta: o atraso da saída 3 em 0 (80 na FILME).</summary>
    private void MexerNoAtraso() => _simulador.Definir("050", 2, "0.0");

    private async Task<string> EntrarNaDuvida()
    {
        MexerNoAtraso();
        await Conectar();
        Assert.Equal(EstadoConferencia.Duvida, _setups.Estado);
        return _setups.Referencia!.Id;
    }

    private static string? Valor(IReadOnlyList<ValorCopiado> valores, string codigo, int? indice) =>
        valores.FirstOrDefault(v => v.Codigo == codigo && v.Indice == indice)?.NoFio;

    private static readonly string[] Desmutar = ["*100*0*0*", "*100*1*0*", "*100*2*0*", "*100*3*0*"];

    // ----------------------------------------------------------------- as camadas (8f)

    [Theory]
    [InlineData("010", Camada.Regulagem, true)]
    [InlineData("060", Camada.Regulagem, true)]
    [InlineData("040", Camada.Regulagem, true)]
    [InlineData("043", Camada.Regulagem, true)]
    [InlineData("050", Camada.Regulagem, true)]
    [InlineData("030", Camada.Regulagem, false)]
    [InlineData("080", Camada.Regulagem, false)]
    [InlineData("081", Camada.Regulagem, false)]
    [InlineData("071", Camada.Regulagem, false)]
    [InlineData("073", Camada.Regulagem, false)]
    [InlineData("092", Camada.Regulagem, false)]
    [InlineData("020", Camada.Bandas, false)]
    [InlineData("220", Camada.Bandas, false)]
    [InlineData("100", Camada.Fora, false)]
    public void A_camada_de_cada_codigo_e_quem_muta_na_conferencia(string codigo, Camada camada, bool muta)
    {
        Assert.Equal(camada, Camadas.De(codigo));
        Assert.Equal(muta, Camadas.MutaNaConferencia(codigo));
    }

    [Fact]
    public void A_regulagem_e_as_bandas_cobrem_o_Marco_1_menos_o_mute_e_o_preset_do_aparelho()
    {
        Assert.Equal(Catalogo.ConsultasMarco1.Count - 4 - 1 - 15,
            Camadas.Consultas(Camada.Regulagem).Count);
        Assert.Equal(16, Camadas.Consultas(Camada.Bandas).Count); // 15 bandas e o 220
    }

    // ----------------------------------------------------------------- a primeira abertura (CAP-6)

    [Fact]
    public async Task Primeira_abertura_sem_arquivos_nenhum_Setup_nenhuma_memoria_nenhum_preset_e_nenhum_modelo()
    {
        using var t = SemArquivos();
        await t._setups.Carregada;
        await t._presets.Carregada;
        await t._modelos.Carregada;

        Assert.Empty(t._setups.Setups);
        Assert.Empty(t._setups.Dados.Memorias);
        Assert.Null(t._setups.UltimoSetup);
        Assert.Empty(t._presets.Presets);
        Assert.Empty(t._modelos.Modelos);
        Assert.Empty(t._modelos.Sugerir("JBL"));
        Assert.Equal(0, t._guardadoSetups.Gravacoes);
        Assert.Equal(0, t._guardadoModelos.Gravacoes);
    }

    [Fact]
    public async Task A_fixture_CasaDoDenis_semeia_o_Casa_2_1_com_os_4_equipamentos_as_fichas_com_origem_e_os_3_modelos()
    {
        using var t = SoCasa();
        await t._setups.Carregada;
        await t._modelos.Carregada;

        var casa = Assert.Single(t._setups.Setups);
        Assert.Equal(Casa, casa.Id);
        Assert.Equal("Casa 2.1", casa.Nome);
        Assert.Null(casa.Ambiente);
        Assert.Equal(4, casa.Equipamentos.Count);
        Assert.Empty(t._setups.Dados.Memorias);
        Assert.Equal(["JBL 4TRMS80", "Protech Light 8\"", "SD800.4 EVO"], t._modelos.Modelos.Select(m => m.Modelo));
        // As fichas com a origem de cada dado.
        var jbl = casa.Equipamentos.Single(e => e.Id == CasaDoDenis.IdJblL);
        Assert.Equal(new DadoTecnico(3.6m, OrigemDado.Medido, "Medido em 18/10/2026"), jbl.Dados.Re);
        Assert.Equal(OrigemDado.Oficial, jbl.Dados.Fs!.Origem);
        Assert.Equal(98m, jbl.Dados.Fs.Valor);
        Assert.True(jbl.Dados.Fs.Verificado);
        Assert.Equal(OrigemDado.Estimado, jbl.Dados.VolumeCaixa!.Origem);
        Assert.False(jbl.Dados.VolumeCaixa.Verificado);
        var sub = casa.Equipamentos.Single(e => e.Id == CasaDoDenis.IdSubCasa);
        Assert.Equal("Protech Light 8\"", sub.Modelo);
        Assert.Equal(450m, sub.Dados.PotenciaRms!.Valor);
        Assert.Equal("dutada", sub.Dados.TipoCaixa!.Valor);
        Assert.Equal(51m, sub.Dados.Fb!.Valor);
        var modulo = casa.Equipamentos.Single(e => e.Id == CasaDoDenis.IdModuloCasa);
        Assert.Equal("SD800.4 EVO", modulo.Modelo);
        Assert.Equal(new[] { 132m, 23m, 400m, 40m }, new[] { modulo.Dados.PotenciaCanal!.Valor, modulo.Dados.TensaoCeifamentoCanal!.Valor,
            modulo.Dados.PotenciaPonte!.Valor, modulo.Dados.TensaoCeifamentoPonte!.Valor });
        Assert.Equal(OrigemDado.Medido, modulo.Dados.GanhoPonte!.Origem);
        // A leitura de 18/10 fica na fixture, para o simulador e a paridade.
        Assert.Equal(Catalogo.ConsultasMarco1.Count, CasaDoDenis.LeituraFilme.Count);
    }

    [Fact]
    public async Task Primeira_conexao_sem_nenhum_Setup_muta_os_4_canais_entra_na_duvida_e_bloqueia_a_IA()
    {
        using var t = SemArquivos();
        await t.Conectar();

        Assert.Equal(EstadoConferencia.Duvida, t._setups.Estado);
        Assert.True(t._setups.EmEscolha);
        Assert.Null(t._setups.Referencia);
        Assert.Empty(t._setups.Diferencas);
        Assert.True(t.Mutados(true));
        Assert.True(t._setups.Mutado);
        Assert.Null(t._setups.Marca!.IdSetup);
        Assert.Equal(GerenteSetups.MsgDuvida, t._setups.MotivoBloqueioIa);
        Assert.Null(t._setups.SetupEmUso);
        Assert.Contains(t._log.Linhas, l => l.Contains("nenhum Setup cadastrado", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Primeira_conexao_criar_o_Setup_Casa_com_ambiente_Sala_e_a_memoria_SOM_1_desmuta()
    {
        using var t = SemArquivos();
        await t.Conectar();
        var inicio = t._simulador.Escritas.Count;

        var r = await t.Rodar(t._setups.CriarSetupAsync("Casa", "SOM 1", AmbienteSetup.Sala));

        Assert.True(r.Ok, r.Mensagem);
        var casa = Assert.Single(t._setups.Setups);
        Assert.Equal("Casa", casa.Nome);
        Assert.Equal(AmbienteSetup.Sala, casa.Ambiente);
        Assert.Empty(casa.Equipamentos);
        var som = Assert.Single(t._setups.MemoriasDe(casa.Id));
        Assert.Equal("SOM 1", som.Nome);
        Assert.Equal(SomEmUso.DoCelular(som.Id), t._setups.EmUso);
        Assert.Equal(EstadoConferencia.Confere, t._setups.Estado);
        Assert.Equal(Desmutar, t.AudioDesde(inicio)); // só o desmutar vai ao aparelho
        Assert.True(t.Mutados(false));
        Assert.Null(t._setups.MotivoBloqueioIa);
        Assert.Equal("sala", Ambiente(t._guardadoSetups.Texto!, casa.Id));
    }

    [Fact]
    public async Task Primeira_conexao_criar_o_Setup_Casa_2_1_do_zero_o_nome_de_Setup_aceita_o_ponto()
    {
        using var t = SemArquivos();
        await t.Conectar();

        var r = await t.Rodar(t._setups.CriarSetupAsync("Casa 2.1", "SOM 1"));

        Assert.True(r.Ok, r.Mensagem);
        var casa = Assert.Single(t._setups.Setups);
        Assert.Equal("Casa 2.1", casa.Nome);
        Assert.NotEqual(CasaDoDenis.IdSetupCasa, casa.Id); // um Setup novo, sem nada da fixture
        Assert.Empty(casa.Equipamentos);
        Assert.Equal("Casa 2.1", GerenteSetups.DeJson(t._guardadoSetups.Texto!)!.Setups.Single().Nome);
        // A memória e o preset seguem a regra deles: o ponto não vale.
        Assert.Null(GerenteSetups.NomeValido("SOM 1.0"));
        Assert.Equal("Casa 2.1", GerenteSetups.NomeSetupValido("  Casa 2.1 "));
        Assert.Null(GerenteSetups.NomeSetupValido("Casa#2"));
    }

    [Fact]
    public async Task Renomear_um_Setup_para_um_nome_com_ponto_vale_e_a_memoria_com_ponto_nao()
    {
        await Conectar();

        var r = await _setups.RenomearSetupAsync(Casa, "Sala 2.0");

        Assert.True(r.Ok, r.Mensagem);
        Assert.Equal("Sala 2.0", _setups.Setup(Casa)!.Nome);
        Assert.Equal("Sala 2.0", GerenteSetups.DeJson(_guardadoSetups.Texto!)!.Setups.Single().Nome);
        Assert.Equal(GerenteSetups.MsgNomeSetupInvalido, (await _setups.RenomearSetupAsync(Casa, "Sala#2")).Motivo);
        Assert.Equal(GerentePresets.MsgNomeInvalido, (await _setups.RenomearMemoriaAsync(Filme, "FILME 2.0")).Motivo);
    }

    [Fact]
    public async Task Primeira_conexao_sem_Setup_com_a_chave_Ajustes_de_audio_desligada_fica_em_duvida_sem_mutar_e_diz_por_que()
    {
        using var t = SemArquivos();
        await t._chaves.Carregada;
        Assert.True((await t._chaves.DefinirAsync(Chave.AjustesAudio, false, OrigemComando.Tela)).Ok);

        await t.Conectar();

        Assert.Equal(EstadoConferencia.Duvida, t._setups.Estado);
        Assert.False(t._setups.Mutado);
        Assert.Null(t._setups.Marca);
        Assert.True(t.Mutados(false));
        Assert.Equal(GerenteSetups.MsgNaoMutou(Sessao.MotivoChaveDesligada), t._setups.MotivoMudo);
        Assert.Equal(GerenteSetups.MsgDuvida, t._setups.MotivoBloqueioIa);
    }

    [Fact]
    public async Task Primeira_conexao_criar_o_Setup_sem_ambiente_cria_com_ambiente_nao_informado()
    {
        using var t = SemArquivos();
        await t.Conectar();

        var r = await t.Rodar(t._setups.CriarSetupAsync("Casa", "SOM 1"));

        Assert.True(r.Ok, r.Mensagem);
        var casa = Assert.Single(t._setups.Setups);
        Assert.Null(casa.Ambiente);
        Assert.Null(Ambiente(t._guardadoSetups.Texto!, casa.Id)); // só é gravado com valor
    }

    [Fact]
    public async Task Primeira_conexao_seguir_sem_Setup_desmuta_trava_as_memorias_e_a_proxima_conexao_volta_a_escolha()
    {
        using var t = SemArquivos();
        await t.Conectar();

        var r = await t.Rodar(t._setups.SeguirSemSetupAsync());

        Assert.True(r.Ok, r.Mensagem);
        Assert.True(t.Mutados(false));
        Assert.Equal(EstadoConferencia.Nenhuma, t._setups.Estado);
        Assert.Null(t._setups.MotivoBloqueioIa);
        Assert.Equal(GerenteSetups.MsgSemSetup, (await t._setups.CriarMemoriaAsync("X")).Motivo);

        await t.Reconectar();

        Assert.Equal(EstadoConferencia.Duvida, t._setups.Estado);
        Assert.True(t.Mutados(true));
        Assert.Equal(GerenteSetups.MsgDuvida, t._setups.MotivoBloqueioIa);
    }

    [Fact]
    public async Task Primeira_conexao_o_Casa_2_1_sem_memoria_muta_e_fica_em_duvida_e_Escolher_desmuta_sem_escrever_nada()
    {
        using var t = SoCasa();
        await t.Conectar();

        Assert.Equal(EstadoConferencia.Duvida, t._setups.Estado);
        Assert.Equal(Casa, t._setups.Referencia!.Id);
        Assert.Empty(t._setups.Diferencas);
        Assert.Null(t._setups.MemoriaDaReferencia);
        Assert.True(t.Mutados(true));
        Assert.Equal(GerenteSetups.MsgDuvida, t._setups.MotivoBloqueioIa);
        var inicio = t._simulador.Escritas.Count;
        var leituras = t._leitura.Iniciadas;

        var r = await t.Rodar(t._setups.EscolherAsync(Casa));

        Assert.True(r.Ok, r.Mensagem);
        Assert.Equal(Desmutar, t.AudioDesde(inicio)); // nada aplicado: só o desmutar
        Assert.Equal(leituras, t._leitura.Iniciadas);
        Assert.Equal(EstadoConferencia.Confere, t._setups.Estado);
        Assert.Equal(Casa, t._setups.SetupEmUso!.Id);
        Assert.Equal(SomEmUso.Nenhum, t._setups.EmUso);
        Assert.Null(t._setups.Marca);
        Assert.True(t._link.Ligado(ParCanais.Saidas34)); // o link sai do bridge 3+4
        Assert.False(t._link.Ligado(ParCanais.Saidas12));
        Assert.Null(t._setups.MotivoBloqueioIa);
    }

    [Fact]
    public async Task Escolher_um_Setup_sem_memorias_desmuta_mesmo_com_a_chave_Aplicar_memoria_desligada()
    {
        using var t = SoCasa();
        await t.Conectar();
        Assert.True((await t._chaves.DefinirAsync(Chave.AplicarMemoriaCelular, false, OrigemComando.Tela)).Ok);

        var r = await t.Rodar(t._setups.EscolherAsync(Casa));

        Assert.True(r.Ok, r.Mensagem);
        Assert.True(t.Mutados(false));
    }

    [Fact]
    public async Task Conectar_escolher_o_Casa_e_criar_a_FILME_do_som_atual_da_a_memoria_do_cenario_de_teste()
    {
        using var t = SoCasa();
        await t.Conectar();
        Assert.True((await t.Rodar(t._setups.EscolherAsync(Casa))).Ok);

        var r = await t.Rodar(t._setups.CriarMemoriaAsync("FILME"));

        Assert.True(r.Ok, r.Mensagem);
        var filme = Assert.Single(t._setups.MemoriasDe(Casa));
        // A regulagem tem o valor final (a R com +7 e −18, como na leitura): nada de equilíbrio.
        Assert.Equal(CenarioFilme.Regulagem, filme.Regulagem);
        Assert.Equal("+7.0", Valor(filme.Regulagem, "081", 1));
        Assert.Equal("-18.0", Valor(filme.Regulagem, "071", 1));
        Assert.Equal(Camadas.Consultas(Camada.Regulagem).Count, filme.Regulagem.Count);
        Assert.Null(filme.IdPreset); // sem preset em uso nesta conexão
    }

    // ----------------------------------------------------------------- conferência (failure-modes 5)

    [Fact]
    public async Task Leitura_igual_a_memoria_ativa_Setup_confere_com_a_FILME_em_uso_e_nada_escrito()
    {
        await Conectar();

        Assert.Equal(EstadoConferencia.Confere, _setups.Estado);
        Assert.Equal(Casa, _setups.SetupEmUso!.Id);
        Assert.Equal(SomEmUso.DoCelular(Filme), _setups.EmUso);
        Assert.False(_setups.Mutado);
        Assert.Null(_setups.MotivoBloqueioIa);
        Assert.All(_simulador.Escritas, e => Assert.True(Catalogo.EhLeitura(e) || Senha.IsMatch(e), e));
        Assert.Equal(0, _guardadoSetups.Gravacoes);
        Assert.Equal(0, _guardadoMudo.Gravacoes);
        Assert.True(_link.Ligado(ParCanais.Saidas34));
    }

    [Theory]
    [InlineData("081", 0, "+4.0", "*081*0*10.0*")] // o ganho
    [InlineData("071", 2, "-3.0", null)] // o limitador
    [InlineData("091", 0, "+0.0", null)] // o EQ de saída
    [InlineData("080", null, "-5.0", null)] // o MASTER
    [InlineData("030", null, "+3.0", null)] // o EQ de entrada
    public async Task So_ganho_limitador_EQ_ou_MASTER_diferem_reaplica_sozinho_sem_mutar(string codigo, int? indice, string valor, string? quadro)
    {
        await _setups.Carregada;
        var filme = _setups.Memoria(Filme)!.Regulagem.Single(v => v.Codigo == codigo && v.Indice == indice).NoFio;
        _simulador.Definir(codigo, indice, valor);

        await Conectar();

        // História 9c (failure-modes, regra 5): a FILME e o Cinema voltam sozinhos, sem mutar.
        Assert.Equal(EstadoConferencia.Confere, _setups.Estado);
        Assert.Equal(SomEmUso.DoCelular(Filme), _setups.EmUso);
        Assert.Equal(Casa, _setups.SetupEmUso!.Id);
        Assert.Equal(Cinema, _setups.IdPresetEmUso);
        Assert.Null(_setups.Reaplicando);
        Assert.False(_setups.Mutado);
        Assert.True(Mutados(false));
        Assert.DoesNotContain(_simulador.Escritas, e => e.StartsWith("*100*", StringComparison.Ordinal));
        Assert.Null(_setups.MotivoBloqueioIa);
        Assert.True(Camadas.Iguais(new ValorCopiado(codigo, indice, filme), _estado.Copiar()));
        var audio = _simulador.Escritas.Where(e => Catalogo.EscritaDeAudio(e) is not null).ToList();
        Assert.Single(audio); // só o que difere
        if (quadro is not null)
            Assert.Equal(quadro, audio[0]);
        Assert.Null(CopiaAntes.DeJson(_guardadoCopia.Texto!)!.Operacao); // terminou: sem marca de pela metade
        Assert.Null(_copia.PelaMetade);
    }

    [Fact]
    public async Task Diferenca_leve_avisa_reaplicando_com_a_memoria_e_o_preset_e_some_ao_terminar()
    {
        _simulador.Definir("081", 0, "+4.0");
        var vistos = new List<Reaplicacao>();
        _setups.Mudou += () =>
        {
            if (_setups.Reaplicando is { } r)
            {
                lock (vistos)
                    vistos.Add(r);
            }
        };

        await Conectar();

        lock (vistos)
            Assert.Contains(new Reaplicacao("FILME", "Cinema"), vistos);
        Assert.Null(_setups.Reaplicando);
        Assert.Equal(EstadoConferencia.Confere, _setups.Estado);
        Assert.Equal("+10.0", _simulador.Valor("081", 0));
    }

    [Fact]
    public async Task Diferenca_leve_com_a_chave_Aplicar_memoria_desligada_nada_escrito_e_fica_o_som_diferente_com_Reaplicar_recusado()
    {
        await _chaves.Carregada;
        Assert.True((await _chaves.DefinirAsync(Chave.AplicarMemoriaCelular, false, OrigemComando.Tela)).Ok);
        _simulador.Definir("081", 0, "+4.0");

        await Conectar();

        Assert.Equal(EstadoConferencia.SomDiferente, _setups.Estado);
        Assert.Equal(SomEmUso.SomDoAparelho, _setups.EmUso);
        Assert.Equal(Casa, _setups.SetupEmUso!.Id);
        Assert.Null(_setups.Reaplicando);
        Assert.True(Mutados(false));
        Assert.DoesNotContain(_simulador.Escritas, e => Catalogo.EscritaDeAudio(e) is not null);
        Assert.Equal("+4.0", _simulador.Valor("081", 0));

        var r = await _setups.ReaplicarAsync();
        Assert.False(r.Ok);
        Assert.Equal(Sessao.MotivoChaveDesligada, r.Motivo);
        Assert.DoesNotContain(_simulador.Escritas, e => Catalogo.EscritaDeAudio(e) is not null);
    }

    [Fact]
    public async Task Diferenca_leve_com_uma_operacao_pela_metade_pendente_nada_escrito_e_a_folha_da_metade_manda()
    {
        await Conectar();
        // Uma operação de vários parâmetros guardou a marca e parou (a conexão caiu no meio dela).
        Assert.True((await _copia.IniciarAsync(new OperacaoPendente("teste", "x", "Operação de teste"))).Ok);
        _copia.Interromper();
        await _sessao.DesconectarAsync();
        _simulador.Definir("081", 0, "+4.0");
        var inicio = _simulador.Escritas.Count;

        await Conectar();
        await AndarAte(() => _copia.PelaMetade is not null);

        Assert.Equal(EstadoConferencia.SomDiferente, _setups.Estado);
        Assert.Null(_setups.Reaplicando);
        Assert.Empty(AudioDesde(inicio));
        Assert.Equal("+4.0", _simulador.Valor("081", 0));
        Assert.True(Mutados(false));
    }

    [Fact]
    public async Task Diferenca_leve_com_a_conexao_caindo_no_meio_da_reaplicacao_fica_aplicado_pela_metade_na_volta()
    {
        // Quantas escritas (a senha e as leituras) uma conexão sem diferenças faz.
        var antes = _simulador.Escritas.Count;
        await Conectar();
        var porConexao = _simulador.Escritas.Count - antes;
        await _sessao.DesconectarAsync();
        _simulador.Definir("081", 0, "+4.0");
        _simulador.Definir("081", 1, "+0.0"); // duas diferenças: a queda vem depois da primeira escrita
        _simulador.CairDepoisDaEscrita = _simulador.Escritas.Count + porConexao + 1;

        await _sessao.ConectarAsync();
        await AndarAte(() => _sessao.Estado == EstadoSessao.Desconectado && _setups.Reaplicando is null && !_setups.Ocupado);

        Assert.Null(_setups.Reaplicando); // a tela REAPLICANDO fecha
        Assert.Equal(GerenteSetups.TipoAplicarMemoria, CopiaAntes.DeJson(_guardadoCopia.Texto!)!.Operacao!.Tipo);

        _simulador.CairDepoisDaEscrita = null;
        var inicio = _simulador.Escritas.Count;
        await Conectar();
        await AndarAte(() => _copia.PelaMetade is not null);

        Assert.Equal(GerenteSetups.TipoAplicarMemoria, _copia.PelaMetade!.Tipo);
        Assert.Equal(EstadoConferencia.SomDiferente, _setups.Estado); // pela metade: não reaplica de novo
        Assert.Empty(AudioDesde(inicio));
        Assert.True(Mutados(false));
    }

    [Fact]
    public async Task So_o_ganho_difere_com_a_chave_desligada_Reaplicar_depois_de_ligar_escreve_so_o_que_difere()
    {
        _simulador.Definir("081", 0, "+4.0");
        await ConectarSemReaplicar();
        Assert.Equal(EstadoConferencia.SomDiferente, _setups.Estado);
        Assert.Equal("Som diferente da FILME", GerenteSetups.MsgSomDiferente(_setups.Ativa(Casa)!.Nome));
        var inicio = _simulador.Escritas.Count;

        var r = await Rodar(_setups.ReaplicarAsync());

        Assert.True(r.Ok, r.Mensagem);
        Assert.Equal(["*081*0*10.0*"], AudioDesde(inicio));
        Assert.Equal(EstadoConferencia.Confere, _setups.Estado);
        Assert.Equal(SomEmUso.DoCelular(Filme), _setups.EmUso);
        Assert.Null(CopiaAntes.DeJson(_guardadoCopia.Texto!)!.Operacao);
    }

    [Fact]
    public async Task O_corte_difere_muta_e_fica_em_duvida_com_uma_linha_do_passa_alta_da_JBL_L()
    {
        _simulador.Definir("040", 0, "200");
        await Conectar();

        Assert.Equal(EstadoConferencia.Duvida, _setups.Estado);
        Assert.True(Mutados(true));
        var d = Assert.Single(_setups.Diferencas);
        Assert.Equal("Passa-alta · JBL L", d.Rotulo);
        Assert.Equal("150 Hz", d.NaMemoria);
        Assert.Equal("200 Hz", d.NoAparelho);
        Assert.Equal(0, d.Canal);
        Assert.Equal("Passa-alta · JBL L: 150 Hz na FILME, 200 Hz no aparelho", d.Linha(_setups.MemoriaDaReferencia!.Nome));
    }

    [Theory]
    [InlineData("010", 0, "a")] // a rota
    [InlineData("060", 2, "0")] // a fase
    [InlineData("043", 2, "0")] // o tipo do passa-baixa
    public async Task Rota_fase_ou_tipo_do_corte_diferentes_tambem_mutam(string codigo, int indice, string valor)
    {
        _simulador.Definir(codigo, indice, valor);
        await Conectar();

        Assert.Equal(EstadoConferencia.Duvida, _setups.Estado);
        Assert.True(Mutados(true));
        Assert.Single(_setups.Diferencas);
    }

    [Fact]
    public async Task O_atraso_difere_muta_os_4_canais_guarda_a_marca_e_lista_so_o_que_muta()
    {
        MexerNoAtraso();
        _simulador.Definir("081", 0, "+4.0"); // o ganho diferente não entra na lista
        await Conectar();

        Assert.Equal(EstadoConferencia.Duvida, _setups.Estado);
        Assert.Null(_setups.SetupEmUso);
        Assert.Equal(Casa, _setups.Referencia!.Id);
        Assert.True(Mutados(true));
        Assert.True(_setups.Mutado);
        var d = Assert.Single(_setups.Diferencas);
        Assert.Equal("Atraso · Sub: 80 cm na FILME, 0 cm no aparelho", d.Linha("FILME"));
        Assert.Equal(2, d.Canal);
        Assert.Equal(GerenteSetups.MsgDuvida, _setups.MotivoBloqueioIa);
        var marca = GerenteSetups.MarcaDeJson(_guardadoMudo.Texto!)!;
        Assert.Equal(Casa, marca.IdSetup);
        Assert.Equal(["0", "0", "0", "0"], marca.MuteAnterior);
        Assert.False(_link.Ligado(ParCanais.Saidas34)); // sem Setup em uso, os links ficam desligados
    }

    [Fact]
    public async Task So_as_15_bandas_diferentes_do_preset_da_memoria_ativa_a_conferencia_confere()
    {
        _simulador.Definir("020", 4, "+3.0");
        _simulador.Definir("020", 13, "-4.0");

        await Conectar();

        Assert.Equal(EstadoConferencia.Confere, _setups.Estado);
        Assert.Equal(SomEmUso.DoCelular(Filme), _setups.EmUso);
        Assert.True(Mutados(false));
    }

    [Fact]
    public async Task Mutar_recusado_pela_chave_a_duvida_abre_mesmo_assim_com_o_motivo()
    {
        await _chaves.Carregada;
        Assert.True((await _chaves.DefinirAsync(Chave.AjustesAudio, false, OrigemComando.Tela)).Ok);
        MexerNoAtraso();

        await Conectar();

        Assert.Equal(EstadoConferencia.Duvida, _setups.Estado);
        Assert.False(_setups.Mutado);
        Assert.True(Mutados(false));
        Assert.Equal(GerenteSetups.MsgNaoMutou(Sessao.MotivoChaveDesligada), _setups.MotivoMudo);
        Assert.Equal(0, _guardadoMudo.Gravacoes);
        Assert.Equal(GerenteSetups.MsgDuvida, _setups.MotivoBloqueioIa);
    }

    [Fact]
    public async Task Duvida_interrompida_volta_a_duvida_sem_mutar_de_novo()
    {
        await EntrarNaDuvida();
        var marca = _setups.Marca;

        await Reconectar();
        var inicio = _simulador.Escritas.Count;
        await AndarAte(() => _setups.Conferido);

        Assert.Equal(EstadoConferencia.Duvida, _setups.Estado);
        Assert.Equal(marca, _setups.Marca);
        Assert.Single(_setups.Diferencas);
        Assert.Empty(AudioDesde(inicio));
        Assert.True(Mutados(true));
    }

    [Fact]
    public async Task Marca_guardada_com_o_aparelho_desmutado_por_fora_apaga_a_marca_e_confere_normal()
    {
        await EntrarNaDuvida();
        await _sessao.DesconectarAsync();
        for (var c = 0; c < 4; c++)
            _simulador.Definir("100", c, "0");
        _simulador.Definir("050", 2, "80.0");

        await Conectar();

        Assert.Equal(EstadoConferencia.Confere, _setups.Estado);
        Assert.Null(_setups.Marca);
        Assert.Null(GerenteSetups.MarcaDeJson(_guardadoMudo.Texto!));
        Assert.True(Mutados(false));
    }

    // ----------------------------------------------------------------- escolher, criar, seguir, trocar

    [Fact]
    public async Task Escolher_o_Casa_2_1_na_duvida_aplica_a_FILME_relê_desmuta_e_confere()
    {
        await EntrarNaDuvida();
        var inicio = _simulador.Escritas.Count;
        var leituras = _leitura.Iniciadas;

        var r = await Rodar(_setups.EscolherAsync(Casa));

        Assert.True(r.Ok, r.Mensagem);
        Assert.Equal(["*050*2*80.0*", .. Desmutar], AudioDesde(inicio));
        Assert.Equal(leituras + 1, _leitura.Iniciadas); // a releitura
        Assert.Equal(EstadoConferencia.Confere, _setups.Estado);
        Assert.Equal(Casa, _setups.SetupEmUso!.Id);
        Assert.Equal(SomEmUso.DoCelular(Filme), _setups.EmUso);
        Assert.Null(_setups.Marca);
        Assert.True(Mutados(false));
        Assert.True(_link.Ligado(ParCanais.Saidas34));
        Assert.Null(CopiaAntes.DeJson(_guardadoCopia.Texto!)!.Operacao);
        Assert.Contains(CopiaAntes.DeJson(_guardadoCopia.Texto!)!.Valores, v => v is { Codigo: "100", Indice: 0, NoFio: "1" }); // a cópia, mutada
    }

    [Fact]
    public async Task Escolher_com_a_chave_Aplicar_memoria_do_celular_desligada_recusa_antes_do_BLE()
    {
        await EntrarNaDuvida();
        Assert.True((await _chaves.DefinirAsync(Chave.AplicarMemoriaCelular, false, OrigemComando.Tela)).Ok);
        var inicio = _simulador.Escritas.Count;

        var r = await Rodar(_setups.EscolherAsync(Casa));

        Assert.False(r.Ok);
        Assert.Equal(Sessao.MotivoChaveDesligada, r.Motivo);
        Assert.Equal(inicio, _simulador.Escritas.Count);
        Assert.Equal(EstadoConferencia.Duvida, _setups.Estado);
    }

    [Fact]
    public async Task Queda_no_meio_do_escolher_fica_pela_metade_mutado_e_Terminar_refaz_e_desmuta()
    {
        _simulador.Definir("040", 2, "45"); // duas diferenças: o passa-alta e o atraso da saída 3
        await EntrarNaDuvida();
        _simulador.CairDepoisDaEscrita = _simulador.Escritas.Count + 1;

        var r = await Rodar(_setups.EscolherAsync(Casa));

        Assert.False(r.Ok);
        Assert.Equal(EstadoSessao.Desconectado, _sessao.Estado);
        Assert.True(Mutados(true));
        Assert.Equal(GerenteSetups.TipoAplicarSetup, CopiaAntes.DeJson(_guardadoCopia.Texto!)!.Operacao!.Tipo);

        await Conectar();
        await AndarAte(() => _copia.PelaMetade is not null);
        Assert.Equal(EstadoConferencia.Duvida, _setups.Estado);
        Assert.Equal(GerenteSetups.TipoAplicarSetup, _copia.PelaMetade!.Tipo);

        var terminar = await Rodar(_copia.TerminarAsync());

        Assert.True(terminar.Ok, terminar.Motivo);
        Assert.Equal(EstadoConferencia.Confere, _setups.Estado);
        Assert.True(Mutados(false));
        Assert.Null(_setups.Marca);
        Assert.Null(_copia.PelaMetade);
        Assert.Equal("80.0", _simulador.Valor("050", 2));
    }

    [Fact]
    public async Task Criar_Setup_novo_Carro_com_a_memoria_ESTRADA_do_som_lido_em_uso_e_desmuta()
    {
        await EntrarNaDuvida();
        var inicio = _simulador.Escritas.Count;

        var r = await Rodar(_setups.CriarSetupAsync("Carro", "ESTRADA"));

        Assert.True(r.Ok, r.Mensagem);
        Assert.Equal("Setup Carro criado, com a memória ESTRADA.", r.Mensagem);
        var carro = _setups.SetupEmUso!;
        Assert.Equal("Carro", carro.Nome);
        Assert.Empty(carro.Equipamentos);
        var estrada = Assert.Single(_setups.MemoriasDe(carro.Id));
        Assert.Equal("ESTRADA", estrada.Nome);
        Assert.Equal("0.0", Valor(estrada.Regulagem, "050", 2)); // a regulagem lida
        Assert.Equal(Cinema, estrada.IdPreset); // as bandas lidas são as do Cinema
        Assert.Equal(SomEmUso.DoCelular(estrada.Id), _setups.EmUso);
        Assert.Equal(estrada.Id, _setups.Ativa(carro.Id)!.Id);
        Assert.Equal(carro.Id, _setups.UltimoSetup);
        Assert.Equal(Desmutar, AudioDesde(inicio)); // só o desmutar
        Assert.Null(_setups.Marca);
        Assert.Equal(EstadoConferencia.Confere, _setups.Estado);
        Assert.False(_link.Ligado(ParCanais.Saidas34)); // sem equipamentos, sem bridge
        Assert.Equal(["CH1", "CH2", "CH3", "CH4"], _setups.SaidasEmUso.Select(s => s.Nome));

        // A próxima conexão confere com a ESTRADA, sem dúvida.
        await Reconectar();
        Assert.Equal(EstadoConferencia.Confere, _setups.Estado);
        Assert.Equal(carro.Id, _setups.SetupEmUso!.Id);
        Assert.Equal(SomEmUso.DoCelular(estrada.Id), _setups.EmUso);
    }

    [Fact]
    public async Task Criar_Setup_sem_o_nome_da_memoria_a_memoria_leva_o_nome_do_Setup()
    {
        await EntrarNaDuvida();

        var r = await Rodar(_setups.CriarSetupAsync("Carro", "  "));

        Assert.True(r.Ok, r.Mensagem);
        Assert.Equal("Carro", Assert.Single(_setups.MemoriasDe(_setups.SetupEmUso!.Id)).Nome);
    }

    [Theory]
    [InlineData("casa#2.1", null)]
    [InlineData("", null)]
    [InlineData("nome#ruim", null)]
    [InlineData("vinte e cinco caracteres!", null)]
    [InlineData("Carro", "nome#ruim")] // o nome da memória
    public async Task Criar_Setup_com_nome_invalido_recusa_e_nada_muda(string nome, string? memoria)
    {
        await EntrarNaDuvida();
        var inicio = _simulador.Escritas.Count;
        var gravacoes = _guardadoSetups.Gravacoes;

        var r = await _setups.CriarSetupAsync(nome, memoria);

        Assert.False(r.Ok);
        // História 10c: o nome da memória tem a recusa própria, para a tela pôr o erro embaixo do campo dela.
        Assert.Equal(memoria is null ? GerenteSetups.MsgNomeSetupInvalido : GerenteSetups.MsgNomeMemoria(GerentePresets.MsgNomeInvalido), r.Motivo);
        Assert.Equal(memoria is not null, r.ErroNomeMemoria);
        Assert.Equal(memoria is null, r.ErroNomeSetup);
        Assert.Single(_setups.Setups);
        Assert.Equal(gravacoes, _guardadoSetups.Gravacoes);
        Assert.Equal(EstadoConferencia.Duvida, _setups.Estado);
        Assert.Equal(inicio, _simulador.Escritas.Count);
        Assert.True(_setups.Mutado);
    }

    [Fact]
    public async Task Criar_Setup_com_nome_repetido_sem_diferenca_de_caixa_recusa_sem_guardar()
    {
        await EntrarNaDuvida();
        Assert.True((await Rodar(_setups.CriarSetupAsync("Carro"))).Ok);
        Assert.True((await Rodar(_setups.TrocarAsync())).Ok);
        var gravacoes = _guardadoSetups.Gravacoes;

        var r = await _setups.CriarSetupAsync("CARRO", "OUTRA");

        Assert.False(r.Ok);
        Assert.Equal(GerenteSetups.MsgNomeRepetidoSetup, r.Motivo);
        Assert.True(r.ErroNomeSetup);
        Assert.False(r.ErroNomeMemoria);
        Assert.Equal(2, _setups.Setups.Count);
        Assert.Equal(gravacoes, _guardadoSetups.Gravacoes);
        Assert.Equal(EstadoConferencia.Trocando, _setups.Estado);
        Assert.True((await Rodar(_setups.DesistirAsync())).Ok);
    }

    [Fact]
    public async Task Seguir_sem_Setup_desmuta_sem_aplicar_e_as_memorias_do_celular_ficam_travadas()
    {
        await EntrarNaDuvida();
        var inicio = _simulador.Escritas.Count;

        var r = await Rodar(_setups.SeguirSemSetupAsync());

        Assert.True(r.Ok, r.Mensagem);
        Assert.Equal(Desmutar, AudioDesde(inicio));
        Assert.Null(_setups.SetupEmUso);
        Assert.Equal(SomEmUso.SomDoAparelho, _setups.EmUso);
        Assert.Null(_setups.MotivoBloqueioIa);
        Assert.Equal("0.0", _simulador.Valor("050", 2)); // nada aplicado
        Assert.False(_link.Ligado(ParCanais.Saidas34));
        Assert.Equal(GerenteSetups.MsgSemSetup, (await _setups.AplicarAsync(Filme)).Motivo);
        Assert.Equal(GerenteSetups.MsgSemSetup, (await _setups.CriarMemoriaAsync("NOVA")).Motivo);
        Assert.Equal(GerenteSetups.MsgSemSetupSalvar, _setups.PrepararSalvar().Trava);
    }

    [Fact]
    public async Task Trocar_de_Setup_muta_e_desistir_desmuta_e_volta_ao_estado_e_ao_link_de_antes()
    {
        await Conectar();
        var inicio = _simulador.Escritas.Count;

        var trocar = await Rodar(_setups.TrocarAsync());

        Assert.True(trocar.Ok, trocar.Mensagem);
        Assert.Equal(EstadoConferencia.Trocando, _setups.Estado);
        Assert.True(Mutados(true));
        Assert.Empty(_setups.Diferencas); // "O aparelho está no Casa 2.1."
        Assert.Equal(GerenteSetups.MsgDuvida, _setups.MotivoBloqueioIa);
        Assert.False(_link.Ligado(ParCanais.Saidas34));

        var desistir = await Rodar(_setups.DesistirAsync());

        Assert.True(desistir.Ok, desistir.Mensagem);
        Assert.Equal(EstadoConferencia.Confere, _setups.Estado);
        Assert.Equal(Casa, _setups.SetupEmUso!.Id);
        Assert.Equal(SomEmUso.DoCelular(Filme), _setups.EmUso);
        Assert.True(Mutados(false));
        Assert.Null(_setups.Marca);
        Assert.True(_link.Ligado(ParCanais.Saidas34));
        Assert.Equal(["*100*0*1*", "*100*1*1*", "*100*2*1*", "*100*3*1*", .. Desmutar], AudioDesde(inicio));
    }

    [Fact]
    public async Task O_desmutar_volta_ao_mute_anterior_de_cada_canal()
    {
        _simulador.Definir("100", 3, "1"); // a saída 4 já estava mutada
        await EntrarNaDuvida();
        Assert.Equal(["0", "0", "0", "1"], _setups.Marca!.MuteAnterior);

        await Rodar(_setups.SeguirSemSetupAsync());

        Assert.Equal("0", _simulador.Valor("100", 0));
        Assert.Equal("1", _simulador.Valor("100", 3));
    }

    // ----------------------------------------------------------------- o link tirado das ligações (D1)

    [Fact]
    public void O_link_sai_das_ligacoes_CH3_e_CH4_no_bridge_3_4_ligam_o_Link_3_4()
    {
        var casa = CasaDoDenis.EquipamentosCasa;

        Assert.Equal((false, true), Ligacoes.Links(casa));
        var saidas = Ligacoes.Saidas(casa);
        Assert.Equal(["JBL L", "JBL R", "Sub"], saidas.Select(s => s.Nome));
        Assert.Equal([2, 3], saidas[2].Canais);
        Assert.All(saidas, s => Assert.Null(s.Aviso));

        // Tirar a ligação do CH4: o bridge fica com sinal num canal só, e o link desliga.
        var modulo = casa[0];
        var semCh4 = casa.Select(e => e.Id == modulo.Id
            ? e with { Entradas = e.Entradas.Select(x => x.Numero == 4 ? x with { Fonte = null } : x).ToArray() }
            : e).ToArray();
        Assert.Equal((false, false), Ligacoes.Links(semCh4));

        // O CH3 nos dois canais do bridge (Y): não é um par, sem link.
        var y = casa.Select(e => e.Id == modulo.Id
            ? e with { Entradas = e.Entradas.Select(x => x.Numero == 4 ? x with { Fonte = FonteSinal.DoCanal(2) } : x).ToArray() }
            : e).ToArray();
        Assert.Equal((false, false), Ligacoes.Links(y));

        // CH1 e CH2 num bridge 1+2 ligam o Link 1–2.
        var ponte12 = new[]
        {
            new Equipamento("m", GrupoEquipamento.Amplificador, "módulo", "M")
            {
                Entradas = [new Entrada(1, FonteSinal.DoCanal(0)), new Entrada(2, FonteSinal.DoCanal(1))],
                Bridges = [new Bridge(1, 2)],
            },
        };
        Assert.Equal((true, false), Ligacoes.Links(ponte12));
    }

    [Fact]
    public async Task Tirar_a_ligacao_do_CH4_no_Setup_em_uso_desliga_o_link_e_religar_liga_de_novo()
    {
        await Conectar();
        Assert.True(_link.Ligado(ParCanais.Saidas34));
        Assert.Equal("Sub", _setups.SaidaDe(3)!.Nome);
        var modulo = _setups.Equipamento(Casa, CasaDoDenis.IdModuloCasa)!;
        var inicio = _simulador.Escritas.Count;

        var r = await _setups.EditarEquipamentoAsync(Casa,
            modulo with { Entradas = modulo.Entradas.Select(x => x.Numero == 4 ? x with { Fonte = null } : x).ToArray() });

        Assert.True(r.Ok, r.Mensagem);
        Assert.False(_link.Ligado(ParCanais.Saidas34));
        Assert.Equal(4, _setups.SaidasEmUso.Count);
        Assert.Equal(inicio, _simulador.Escritas.Count); // mexer nos equipamentos não manda nada

        Assert.True((await _setups.EditarEquipamentoAsync(Casa, modulo)).Ok);
        Assert.True(_link.Ligado(ParCanais.Saidas34));
        Assert.Equal(3, _setups.SaidasEmUso.Count);
    }

    [Fact]
    public async Task Religar_o_bridge_com_o_CH3_diferente_do_CH4_avisa_na_hora_que_as_saidas_estao_diferentes()
    {
        await Conectar();
        var modulo = _setups.Equipamento(Casa, CasaDoDenis.IdModuloCasa)!;
        Assert.True((await _setups.EditarEquipamentoAsync(Casa,
            modulo with { Entradas = modulo.Entradas.Select(x => x.Numero == 4 ? x with { Fonte = null } : x).ToArray() })).Ok);
        Assert.True((await Rodar(_escritor.EscreverAsync(Ganho, 3, -2.0m))).Ok); // sem link: só o CH4
        Assert.False(_link.Diferentes(ParCanais.Saidas34));

        Assert.True((await _setups.EditarEquipamentoAsync(Casa, modulo)).Ok);

        Assert.True(_link.Ligado(ParCanais.Saidas34));
        Assert.True(_link.Diferentes(ParCanais.Saidas34)); // sem esperar a próxima leitura
        Assert.Equal("Link 3–4: as saídas estão diferentes", _link.AvisoDiferenca(ParCanais.Saidas34));
    }

    [Fact]
    public async Task Mexer_nos_equipamentos_de_um_Setup_que_nao_esta_em_uso_nao_mexe_no_link()
    {
        await EntrarNaDuvida();
        Assert.False(_link.Ligado(ParCanais.Saidas34));
        var modulo = _setups.Equipamento(Casa, CasaDoDenis.IdModuloCasa)!;

        Assert.True((await _setups.EditarEquipamentoAsync(Casa, modulo with { Nome = "Módulo" })).Ok);

        Assert.False(_link.Ligado(ParCanais.Saidas34)); // na dúvida, sem Setup em uso: desligado
    }

    [Fact]
    public async Task Com_o_Link_3_4_mexer_no_CH3_espelha_no_CH4()
    {
        await Conectar();
        var inicio = _simulador.Escritas.Count;

        Assert.True((await Rodar(_escritor.EscreverAsync(Atraso, 2, 70.0m))).Ok);

        Assert.Equal(["*050*2*70.0*", "*050*3*70.0*"], AudioDesde(inicio));
    }

    // ----------------------------------------------------------------- memórias do celular

    [Fact]
    public async Task Criar_memoria_do_som_atual_guarda_a_regulagem_com_o_valor_final_e_vira_a_em_uso()
    {
        await Conectar();
        Assert.True((await Rodar(_escritor.EscreverAsync(Ganho, 0, 4.0m))).Ok);
        var inicio = _simulador.Escritas.Count;

        var r = await Rodar(_setups.CriarMemoriaAsync("MUSICA ORIG"));

        Assert.True(r.Ok, r.Mensagem);
        Assert.Equal("Memória MUSICA ORIG criada no Casa 2.1.", r.Mensagem);
        var nova = _setups.MemoriasDe(Casa).Single(m => m.Nome == "MUSICA ORIG");
        Assert.Equal("+7.0", Valor(nova.Regulagem, "081", 1));
        Assert.Equal("-18.0", Valor(nova.Regulagem, "071", 1));
        Assert.Equal("+4.0", Valor(nova.Regulagem, "081", 0));
        Assert.Equal("80.0", Valor(nova.Regulagem, "050", 2)); // o atraso também é da memória
        Assert.Equal(Cinema, nova.IdPreset); // lembra o preset em uso
        Assert.Equal(SomEmUso.DoCelular(nova.Id), _setups.EmUso);
        Assert.Equal(nova.Id, _setups.Ativa(Casa)!.Id);
        Assert.Equal([Filme, nova.Id, null], _setups.Botoes(Casa).Select(m => m?.Id));
        Assert.Equal(inicio, _simulador.Escritas.Count); // criar não manda nada
    }

    [Theory]
    [InlineData("filme", "Já existe uma memória com esse nome no Casa 2.1.")]
    [InlineData("  ", "Use de 1 a 24 letras, números, espaço, _ ou -")]
    public async Task Criar_memoria_com_nome_repetido_no_Setup_ou_invalido_recusa(string nome, string motivo)
    {
        await Conectar();

        var r = await _setups.CriarMemoriaAsync(nome);

        Assert.False(r.Ok);
        Assert.Equal(motivo, r.Motivo);
        Assert.Single(_setups.MemoriasDe(Casa));
    }

    [Fact]
    public async Task Criar_memoria_com_o_som_do_aparelho_em_uso_pede_a_folha_do_Salvar()
    {
        _simulador.Definir("081", 0, "+4.0"); // só o ganho difere da FILME: som diferente, o som do aparelho
        await ConectarSemReaplicar();
        Assert.Equal(SomEmUso.SomDoAparelho, _setups.EmUso);

        var r = await _setups.CriarMemoriaAsync("NOVA");

        Assert.False(r.Ok);
        Assert.True(r.PedeFolhaSalvar);
        Assert.Single(_setups.MemoriasDe(Casa));
    }

    [Fact]
    public async Task Aplicar_a_FILME_escreve_o_valor_final_guardado_sem_soma_nenhuma()
    {
        _simulador.Definir("081", 1, "+0.0");
        _simulador.Definir("071", 1, "-3.0");
        await ConectarSemReaplicar();
        var inicio = _simulador.Escritas.Count;

        var r = await Rodar(_setups.AplicarAsync(Filme));

        Assert.True(r.Ok, r.Mensagem);
        Assert.Equal(["*081*1*7.0*", "*071*1*-18.0*"], AudioDesde(inicio).Order().Reverse().ToArray());
    }

    [Fact]
    public async Task Aplicar_memoria_com_limiter_OFF_escreve_0_5_volta_as_bandas_do_preset_e_o_mute_nao_muda()
    {
        await Conectar();
        Assert.True((await Rodar(_escritor.EscreverAsync(Limiter, 1, 0.5m))).Ok);
        Assert.True((await Rodar(_setups.CriarMemoriaAsync("SEM LIMITE"))).Ok);
        var off = Valor(_setups.MemoriasDe(Casa).Single(m => m.Nome == "SEM LIMITE").Regulagem, "071", 1)!;
        Assert.Equal("OFF", Limiter.Decodificar(off).Valor!.Rotulo);
        Assert.True((await Rodar(_setups.AplicarAsync(Filme))).Ok);
        Assert.Equal("-18.0", _simulador.Valor("071", 1));
        Assert.True((await Rodar(_escritor.EscreverAsync(Banda, 3, -4.0m))).Ok);
        _simulador.Definir("100", 3, "1");
        var inicio = _simulador.Escritas.Count;

        var r = await Rodar(_setups.AplicarAsync(_setups.MemoriasDe(Casa).Single(m => m.Nome == "SEM LIMITE").Id));

        Assert.True(r.Ok, r.Mensagem);
        // A memória nasceu com a FILME em uso: lembra o Cinema, e a banda volta junto.
        Assert.Equal(["*071*1*0.5*", Banda.CodificarEscrita(3, 0.0m)], AudioDesde(inicio));
        var copia = CopiaAntes.DeJson(_guardadoCopia.Texto!)!;
        Assert.Null(copia.Operacao);
        Assert.Contains(copia.Valores, v => v is { Codigo: "071", Indice: 1, NoFio: "-18.0" });
        var espelho = _estado.Copiar();
        var memoria = _setups.Memoria(_setups.EmUso.IdMemoria)!;
        Assert.Empty(Camadas.Diferentes(memoria.Regulagem, espelho));
        Assert.Empty(Camadas.Diferentes(CenarioFilme.Bandas, espelho));
        Assert.Equal(1m, _estado.Valor("100", 3)!.Numero);
        Assert.Equal("SEM LIMITE", memoria.Nome);
    }

    [Fact]
    public async Task Aplicar_uma_memoria_sem_preset_lembrado_nao_mexe_nas_bandas()
    {
        using var t = SoCasa();
        await t.Conectar();
        Assert.True((await t.Rodar(t._setups.EscolherAsync(Casa))).Ok);
        Assert.True((await t.Rodar(t._escritor.EscreverAsync(Ganho, 0, 2.0m))).Ok);
        Assert.True((await t.Rodar(t._setups.CriarMemoriaAsync("SEM PRESET"))).Ok);
        var semPreset = Assert.Single(t._setups.MemoriasDe(Casa));
        Assert.Null(semPreset.IdPreset); // nenhum preset em uso
        Assert.True((await t.Rodar(t._escritor.EscreverAsync(Ganho, 0, 0.0m))).Ok);
        Assert.True((await t.Rodar(t._escritor.EscreverAsync(Banda, 4, -2.0m))).Ok);
        var inicio = t._simulador.Escritas.Count;

        var r = await t.Rodar(t._setups.AplicarAsync(semPreset.Id));

        Assert.True(r.Ok, r.Mensagem);
        Assert.Equal(["*081*0*2.0*"], t.AudioDesde(inicio));
        Assert.Equal("-2.0", t._simulador.Valor("020", 4));
    }

    [Fact]
    public async Task Aplicar_memoria_com_a_chave_desligada_recusa_antes_do_BLE()
    {
        await Conectar();
        Assert.True((await _chaves.DefinirAsync(Chave.AplicarMemoriaCelular, false, OrigemComando.Tela)).Ok);
        _simulador.Definir("081", 0, "+1.0");
        var inicio = _simulador.Escritas.Count;

        var r = await _setups.AplicarAsync(Filme);

        Assert.False(r.Ok);
        Assert.Equal(Sessao.MotivoChaveDesligada, r.Motivo);
        Assert.Equal(inicio, _simulador.Escritas.Count);
        Assert.Equal(0, _guardadoCopia.Gravacoes);
    }

    [Fact]
    public async Task Aplicar_uma_memoria_na_duvida_e_recusado()
    {
        await EntrarNaDuvida();

        var r = await _setups.AplicarAsync(Filme);

        Assert.False(r.Ok);
        Assert.Equal(GerenteSetups.MsgDuvida, r.Motivo);
    }

    [Fact]
    public void Botoes_fixos_a_memoria_nova_toma_o_lugar_da_usada_ha_mais_tempo()
    {
        var uso = new UsoDoSetup(["b", "a", "c"], ["a", "b", "c"]);

        var depois = GerenteSetups.Usar(uso, "d");

        Assert.Equal(["a", "b", "d"], depois.Botoes);
        Assert.Equal(["d", "b", "a", "c"], depois.Recentes);
        Assert.Equal(["a", "b", "d"], GerenteSetups.Usar(depois, "a").Botoes);
        Assert.Equal(["x", "y", null], GerenteSetups.Usar(new UsoDoSetup(["x"], ["x", null, null]), "y").Botoes);
    }

    [Fact]
    public void Apagar_a_memoria_de_um_botao_a_vaga_fica_com_a_usada_por_ultimo_fora_dos_botoes_ou_vazia()
    {
        var uso = new UsoDoSetup(["d", "x", "a", "b"], ["a", "b", "d"]);

        var depois = GerenteSetups.Remover(uso, "b");

        Assert.Equal(["a", "x", "d"], depois.Botoes);
        Assert.Equal(["a", null, "d"], GerenteSetups.Remover(depois, "x").Botoes);
    }

    [Fact]
    public async Task Apagar_a_memoria_em_uso_e_o_Setup_em_uso_e_recusado()
    {
        await Conectar();

        Assert.Equal(GerenteSetups.MsgMemoriaEmUso, (await _setups.ApagarMemoriaAsync(Filme)).Motivo);
        Assert.Equal(GerenteSetups.MsgSetupEmUso, (await _setups.ApagarSetupAsync(Casa)).Motivo);
        Assert.Single(_setups.MemoriasDe(Casa));
        Assert.Single(_setups.Setups);
    }

    [Fact]
    public async Task Apagar_e_renomear_memoria_nao_manda_nada_e_o_botao_vazio_fica_vazio()
    {
        await Conectar();
        Assert.True((await Rodar(_setups.CriarMemoriaAsync("NOITE"))).Ok);
        var noite = _setups.Ativa(Casa)!;
        Assert.True((await Rodar(_setups.AplicarAsync(Filme))).Ok);
        var inicio = _simulador.Escritas.Count;

        Assert.Equal(GerenteSetups.MsgNomeRepetidoMemoria("Casa 2.1"), (await _setups.RenomearMemoriaAsync(noite.Id, "FILME")).Motivo);
        Assert.True((await _setups.RenomearMemoriaAsync(noite.Id, "NOITE 2")).Ok);
        Assert.True((await _setups.ApagarMemoriaAsync(noite.Id)).Ok);

        Assert.Equal(inicio, _simulador.Escritas.Count);
        Assert.Equal([Filme, null, null], _setups.Botoes(Casa).Select(m => m?.Id));
        Assert.Equal([Filme], _setups.Dados.UsoDe(Casa).Recentes);
    }

    [Fact]
    public async Task Apagar_um_Setup_que_nao_esta_em_uso_leva_as_memorias_dele()
    {
        await EntrarNaDuvida();
        Assert.True((await Rodar(_setups.CriarSetupAsync("Carro", "ESTRADA"))).Ok);
        var carro = _setups.SetupEmUso!.Id;
        Assert.True((await Rodar(_setups.TrocarAsync())).Ok);
        Assert.True((await Rodar(_setups.EscolherAsync(Casa))).Ok);

        var r = await _setups.ApagarSetupAsync(carro);

        Assert.True(r.Ok, r.Mensagem);
        Assert.Single(_setups.Setups);
        Assert.Empty(_setups.MemoriasDe(carro));
        Assert.DoesNotContain(_setups.Dados.Memorias, m => m.Nome == "ESTRADA");
    }

    // ----------------------------------------------------------------- operação pela metade

    [Fact]
    public async Task O_mudo_da_duvida_passa_mesmo_com_uma_operacao_pela_metade()
    {
        await Conectar();
        // Uma operação de vários parâmetros guardou a marca e parou (a conexão caiu no meio dela).
        Assert.True((await _copia.IniciarAsync(new OperacaoPendente("teste", "x", "Operação de teste"))).Ok);
        _copia.Interromper();
        await _sessao.DesconectarAsync();
        _simulador.Definir("050", 2, "60.0"); // o atraso do Sub mudou fora do app: dúvida

        await Conectar();
        await AndarAte(() => _copia.PelaMetade is not null);

        Assert.Equal(EstadoConferencia.Duvida, _setups.Estado);
        Assert.True(Mutados(true));
        Assert.True(_setups.Mutado);
        Assert.NotNull(_copia.PelaMetade);
    }

    // ----------------------------------------------------------------- Salvar por camadas

    [Fact]
    public async Task Salvar_com_a_memoria_do_celular_em_uso_leva_toda_a_regulagem_a_memoria_sem_mandar_nada()
    {
        await Conectar();
        Assert.Equal(GerenteSetups.MsgNadaParaSalvar, _setups.PrepararSalvar().Trava);
        Assert.True((await Rodar(_escritor.EscreverAsync(Atraso, 2, 85.0m))).Ok);
        Assert.True((await Rodar(_escritor.EscreverAsync(Ganho, 0, 8.0m))).Ok);

        var folha = _setups.PrepararSalvar();

        Assert.Null(folha.Trava);
        Assert.False(folha.PedeNome);
        var linha = Assert.Single(folha.Linhas);
        Assert.Equal(Camada.Regulagem, linha.Camada);
        Assert.Equal("Regulagem → memória FILME", linha.Titulo);
        // O Link 3–4 repete o atraso no CH4: o par muda igual, uma linha só, na cor do CH3.
        Assert.Equal(["Atraso · Sub: 80 cm → 85 cm", "Ganho · JBL L: +10,0 dB → +8,0 dB"], linha.Mudancas.Select(m => m.Linha).Order());
        Assert.Equal(2, linha.Mudancas.Single(m => m.Rotulo.StartsWith("Atraso", StringComparison.Ordinal)).Canal);
        var inicio = _simulador.Escritas.Count;

        var r = await _setups.SalvarAsync(null);

        Assert.True(r.Ok, r.Mensagem);
        Assert.Equal("Salvo: regulagem na FILME.", r.Mensagem);
        Assert.Equal("85.0", Valor(_setups.Memoria(Filme)!.Regulagem, "050", 2));
        Assert.Equal("85.0", Valor(_setups.Memoria(Filme)!.Regulagem, "050", 3));
        Assert.Equal("+8.0", Valor(_setups.Memoria(Filme)!.Regulagem, "081", 0));
        Assert.Equal("+7.0", Valor(_setups.Memoria(Filme)!.Regulagem, "081", 1));
        Assert.Equal(inicio, _simulador.Escritas.Count); // salvar não manda nada
        Assert.Equal(GerenteSetups.MsgNadaParaSalvar, _setups.PrepararSalvar().Trava);
    }

    [Fact]
    public async Task Salvar_com_o_som_do_aparelho_pede_o_nome_e_guarda_a_regulagem_lida_numa_memoria_nova()
    {
        _simulador.Definir("081", 0, "-5.0"); // só o ganho difere da FILME: o som do aparelho
        await ConectarSemReaplicar();
        Assert.Equal(SomEmUso.SomDoAparelho, _setups.EmUso);
        var inicio = _simulador.Escritas.Count;

        var folha = _setups.PrepararSalvar();

        Assert.Null(folha.Trava); // com o som do aparelho, o Salvar nunca trava por "nada mudou"
        Assert.True(folha.PedeNome);
        Assert.Equal(["Regulagem → memória nova", "15 bandas → preset Cinema"], folha.Linhas.Select(l => l.Titulo));
        Assert.False(folha.PedeNomePreset); // as bandas são as do Cinema: lembra ele, sem pergunta

        var r = await _setups.SalvarAsync("MUSICA ORIG");

        Assert.True(r.Ok, r.Mensagem);
        Assert.Equal("Salvo: regulagem na MUSICA ORIG.", r.Mensagem);
        var nova = _setups.MemoriasDe(Casa).Single(m => m.Nome == "MUSICA ORIG");
        Assert.Equal("-5.0", Valor(nova.Regulagem, "081", 0));
        Assert.Equal(Cinema, nova.IdPreset);
        Assert.Equal("+10.0", Valor(_setups.Memoria(Filme)!.Regulagem, "081", 0)); // a FILME não mudou
        Assert.Equal(SomEmUso.DoCelular(nova.Id), _setups.EmUso);
        Assert.Equal(EstadoConferencia.Confere, _setups.Estado);
        Assert.Equal(inicio, _simulador.Escritas.Count); // o Salvar não manda nada pelo BLE
    }

    [Fact]
    public async Task Salvar_no_som_diferente_vai_para_uma_memoria_nova()
    {
        _simulador.Definir("081", 0, "+4.0");
        await ConectarSemReaplicar();
        Assert.Equal(EstadoConferencia.SomDiferente, _setups.Estado);

        var folha = _setups.PrepararSalvar();

        Assert.True(folha.PedeNome);
        var r = await _setups.SalvarAsync("NOVA");
        Assert.True(r.Ok, r.Mensagem);
        Assert.Equal(EstadoConferencia.Confere, _setups.Estado);
        Assert.Equal("+4.0", Valor(_setups.MemoriasDe(Casa).Single(m => m.Nome == "NOVA").Regulagem, "081", 0));
    }

    [Fact]
    public async Task Salvar_num_Setup_sem_memorias_cria_a_memoria_com_a_regulagem()
    {
        using var t = SoCasa();
        await t.Conectar();
        Assert.True((await t.Rodar(t._setups.EscolherAsync(Casa))).Ok);
        Assert.True((await t.Rodar(t._escritor.EscreverAsync(Atraso, 2, 30.0m))).Ok);

        var folha = t._setups.PrepararSalvar();

        Assert.Null(folha.Trava);
        Assert.True(folha.PedeNome);
        Assert.Equal(["Regulagem → memória nova", "15 bandas → preset novo"], folha.Linhas.Select(l => l.Titulo));
        Assert.True(folha.PedeNomePreset); // sem nenhum preset no celular

        var r = await t._setups.SalvarAsync("PRIMEIRA", EscolhaPreset.Nenhuma, "CINEMA");

        Assert.True(r.Ok, r.Mensagem);
        var primeira = Assert.Single(t._setups.MemoriasDe(Casa));
        Assert.Equal("PRIMEIRA", primeira.Nome);
        Assert.Equal("30.0", Valor(primeira.Regulagem, "050", 2));
        var preset = Assert.Single(t._presets.Presets);
        Assert.Equal(preset.Id, primeira.IdPreset);
        Assert.Equal(SomEmUso.DoCelular(primeira.Id), t._setups.EmUso);
    }

    // ----------------------------------------------------------------- guardado (formato 3)

    [Fact]
    public async Task Reabrir_o_app_traz_Setups_equipamentos_memorias_ultimo_Setup_botoes_e_a_marca_iguais()
    {
        await EntrarNaDuvida();
        Assert.True((await Rodar(_setups.CriarSetupAsync("Carro", "ESTRADA"))).Ok);
        Assert.True((await Rodar(_setups.TrocarAsync())).Ok);
        var dados = _setups.Dados;
        var marca = _setups.Marca;
        Assert.NotNull(marca);
        Assert.Equal(GerenteSetups.Formato, GerenteSetups.FormatoDe(_guardadoSetups.Texto!));

        using var reaberto = new SetupsTestes(_guardadoSetups.Texto, _guardadoMudo.Texto);
        await reaberto._setups.Carregada;

        var outro = reaberto._setups.Dados;
        Assert.Equal(dados.UltimoSetup, outro.UltimoSetup);
        Assert.Equal(dados.Setups.Select(s => (s.Id, s.Nome, s.Ambiente)), outro.Setups.Select(s => (s.Id, s.Nome, s.Ambiente)));
        Assert.Equal(dados.Setups.SelectMany(s => s.Equipamentos), outro.Setups.SelectMany(s => s.Equipamentos), new EquipamentoIgual());
        Assert.Equal(dados.Memorias.Select(m => (m.Id, m.IdSetup, m.Nome, m.IdPreset)), outro.Memorias.Select(m => (m.Id, m.IdSetup, m.Nome, m.IdPreset)));
        Assert.Equal(dados.Memorias.SelectMany(m => m.Regulagem), outro.Memorias.SelectMany(m => m.Regulagem));
        foreach (var s in dados.Setups)
        {
            Assert.Equal(dados.UsoDe(s.Id).Recentes, outro.UsoDe(s.Id).Recentes);
            Assert.Equal(dados.UsoDe(s.Id).Botoes, outro.UsoDe(s.Id).Botoes);
        }
        Assert.Equal(marca!.IdSetup, reaberto._setups.Marca!.IdSetup);
        Assert.Equal(marca.MuteAnterior, reaberto._setups.Marca.MuteAnterior);
    }

    [Fact]
    public void As_fichas_com_origem_e_fonte_sobrevivem_ao_JSON()
    {
        var dados = CasaDoDenis.Dados;

        var relido = GerenteSetups.DeJson(GerenteSetups.ParaJson(dados))!;

        Assert.Equal(dados.Setups[0].Equipamentos, relido.Setups[0].Equipamentos, new EquipamentoIgual());
        Assert.Equal(CasaDoDenis.EquipamentosCasa, relido.Setups[0].Equipamentos, new EquipamentoIgual());
    }

    [Theory]
    [InlineData("{ isto não é json")]
    [InlineData("{\"formato\": 4, \"setups\": [{\"id\":\"x\",\"nome\":\"X\"}]}")]
    [InlineData("{\"formato\": 3, \"setups\": [{\"id\":\"\",\"nome\":\"X\"}]}")]
    [InlineData("{\"formato\": \"um\"}")]
    [InlineData("[]")]
    public async Task Um_setups_json_estragado_volta_ao_vazio_sem_erro_e_registra_no_log(string json)
    {
        using var reaberto = new SetupsTestes(json, "lixo");
        await reaberto._setups.Carregada;

        var dados = reaberto._setups.Dados;
        Assert.Empty(dados.Setups);
        Assert.Empty(dados.Memorias);
        Assert.Null(dados.UltimoSetup);
        Assert.Null(reaberto._setups.Marca);
        Assert.Contains(reaberto._log.Linhas, l => l.Contains("setups.json ilegível: voltei ao vazio", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("{\"formato\": 3, \"setups\": []}")]
    [InlineData("{\"formato\": 3, \"ultimoSetup\": null, \"setups\": [], \"memorias\": []}")]
    public async Task Um_setups_json_com_zero_Setups_vale_e_o_app_fica_vazio_sem_log_de_erro(string json)
    {
        using var reaberto = new SetupsTestes(json, null);
        await reaberto._setups.Carregada;

        Assert.Empty(reaberto._setups.Setups);
        Assert.Null(reaberto._setups.UltimoSetup);
        Assert.DoesNotContain(reaberto._log.Linhas, l => l.Contains("ilegível", StringComparison.Ordinal));
        Assert.NotNull(GerenteSetups.DeJson(json));
    }

    [Fact]
    public async Task Um_setups_json_com_o_Casa_2_1_carrega_como_Setup_comum_e_pode_ser_renomeado_e_apagado()
    {
        var comCarro = CasaDoDenis.Dados with
        {
            Setups = [.. CasaDoDenis.Dados.Setups, new Setup("carro", "Carro", DateTimeOffset.UnixEpoch)],
            UltimoSetup = "carro",
        };
        using var t = new SetupsTestes(GerenteSetups.ParaJson(comCarro), null);
        await t._setups.Carregada;

        var casa = t._setups.Setup(Casa)!;
        Assert.Equal("Casa 2.1", casa.Nome);
        Assert.Equal(4, casa.Equipamentos.Count);
        Assert.Equal(0, t._guardadoSetups.Gravacoes); // nada regravado só por ter o Casa

        Assert.True((await t._setups.RenomearSetupAsync(Casa, "Sala de casa")).Ok);
        Assert.Equal("Sala de casa", t._setups.Setup(Casa)!.Nome);
        var r = await t._setups.ApagarSetupAsync(Casa);

        Assert.True(r.Ok, r.Mensagem);
        Assert.Equal(["carro"], t._setups.Setups.Select(s => s.Id));
    }

    [Theory]
    [InlineData("carro", AmbienteSetup.Carro)]
    [InlineData("sala", AmbienteSetup.Sala)]
    [InlineData("ar-livre", AmbienteSetup.ArLivre)]
    [InlineData("outro", AmbienteSetup.Outro)]
    [InlineData("lua", null)]
    [InlineData("Carro", null)]
    [InlineData(null, null)]
    public void O_ambiente_no_JSON_le_o_nome_e_um_texto_que_nao_vale_fica_null(string? nome, AmbienteSetup? esperado)
    {
        var ambiente = nome is null ? "" : $"\"ambiente\": \"{nome}\", ";
        var json = $$"""{"formato": 3, "ultimoSetup": "s", "setups": [{"id": "s", "nome": "Casa", {{ambiente}}"quando": "2026-10-31T00:00:00-03:00"}]}""";

        var dados = GerenteSetups.DeJson(json)!;

        Assert.Equal(esperado, dados.Setups.Single().Ambiente);
    }

    [Fact]
    public void O_ambiente_so_e_gravado_com_valor_e_sobrevive_ao_JSON_sem_subir_o_formato()
    {
        var dados = new DadosSetups(
            [new Setup("a", "Carro", DateTimeOffset.UnixEpoch, AmbienteSetup.ArLivre), new Setup("b", "Casa", DateTimeOffset.UnixEpoch)],
            [], null, new Dictionary<string, UsoDoSetup>());

        var json = GerenteSetups.ParaJson(dados);

        Assert.Equal(3, GerenteSetups.FormatoDe(json));
        Assert.Equal(3, GerenteSetups.Formato);
        Assert.Equal("ar-livre", Ambiente(json, "a"));
        Assert.Null(Ambiente(json, "b"));
        Assert.Equal([AmbienteSetup.ArLivre, (AmbienteSetup?)null], GerenteSetups.DeJson(json)!.Setups.Select(s => s.Ambiente));
    }

    [Fact]
    public async Task Definir_o_ambiente_grava_e_rele_e_nao_manda_nada_ao_aparelho()
    {
        await Conectar();
        var inicio = _simulador.Escritas.Count;

        var r = await _setups.DefinirAmbienteAsync(Casa, AmbienteSetup.ArLivre);

        Assert.True(r.Ok, r.Mensagem);
        Assert.Equal(AmbienteSetup.ArLivre, _setups.Setup(Casa)!.Ambiente);
        Assert.Equal(inicio, _simulador.Escritas.Count);
        Assert.Equal("ar-livre", Ambiente(_guardadoSetups.Texto!, Casa));
        using var reaberto = new SetupsTestes(_guardadoSetups.Texto, null);
        await reaberto._setups.Carregada;
        Assert.Equal(AmbienteSetup.ArLivre, reaberto._setups.Setup(Casa)!.Ambiente);

        Assert.True((await _setups.DefinirAmbienteAsync(Casa, null)).Ok);
        Assert.Null(_setups.Setup(Casa)!.Ambiente);
        Assert.Null(Ambiente(_guardadoSetups.Texto!, Casa));
    }

    [Fact]
    public async Task Um_ambiente_fora_do_enum_e_recusado_ao_criar_e_ao_definir_e_os_rotulos_nao_o_tratam_como_Outro()
    {
        var invalido = (AmbienteSetup)99;
        await EntrarNaDuvida();
        var gravacoes = _guardadoSetups.Gravacoes;

        Assert.Equal(GerenteSetups.MsgAmbienteInvalido, (await _setups.CriarSetupAsync("Carro", null, invalido)).Motivo);
        Assert.Equal(GerenteSetups.MsgAmbienteInvalido, (await _setups.DefinirAmbienteAsync(Casa, invalido)).Motivo);

        Assert.Equal(gravacoes, _guardadoSetups.Gravacoes);
        Assert.Single(_setups.Setups);
        Assert.Null(_setups.Setup(Casa)!.Ambiente);
        Assert.Throws<ArgumentOutOfRangeException>(() => Ambientes.Rotulo(invalido));
        Assert.Throws<ArgumentOutOfRangeException>(() => Ambientes.Nome(invalido));
        Assert.Equal("Outro", Ambientes.Rotulo(AmbienteSetup.Outro));
        Assert.Equal("outro", Ambientes.Nome(AmbienteSetup.Outro));
    }

    [Fact]
    public async Task Definir_o_ambiente_que_nao_grava_avisa_e_o_valor_nao_muda()
    {
        await Conectar();
        _guardadoSetups.FalharAoGravar = true;

        var r = await _setups.DefinirAmbienteAsync(Casa, AmbienteSetup.Carro);

        Assert.False(r.Ok);
        Assert.Equal(GerenteSetups.MsgNaoGuardou, r.Motivo);
        Assert.Null(_setups.Setup(Casa)!.Ambiente);
    }

    /// <summary>O <c>ambiente</c> do Setup no <c>setups.json</c>, ou null se não foi gravado.</summary>
    private static string? Ambiente(string json, string idSetup)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var setup = doc.RootElement.GetProperty("setups").EnumerateArray().Single(s => s.GetProperty("id").GetString() == idSetup);
        return setup.TryGetProperty("ambiente", out var a) ? a.GetString() : null;
    }

    // ----------------------------------------------------------------- migração dos formatos 1 e 2

    /// <summary>Um <c>setups.json</c> da 8e (formato 2): o Casa com a calibração, o equilíbrio, o link e a FILME; um Carro com um equipamento legível e um ilegível.</summary>
    private const string SetupsFormato2 = """
        {
          "formato": 2,
          "ultimoSetup": "setup-carro",
          "setups": [
            {
              "id": "herdado-setup-casa-2-1", "nome": "Casa 2.1", "quando": "2026-10-18T21:45:00-03:00",
              "equilibrio": { "ganho": [0, -3, 0, 0], "limiter": [0, -3, 0, 0] },
              "link12": false, "link34": true,
              "calibracao": [ { "codigo": "050", "indice": 2, "noFio": "80.0" } ],
              "equipamentos": [
                { "id": "herdado-modulo", "grupo": "amplificador", "categoria": "módulo", "nome": "Módulo genérico",
                  "dados": { "ganho": 19.6, "canais": 4 },
                  "canais": [ { "n": 1, "fonte": "CH1" }, { "n": 2, "fonte": "CH2" }, { "n": 3, "fonte": "CH3" }, { "n": 4, "fonte": "CH4" } ],
                  "bridges": [ [3, 4] ] },
                { "id": "herdado-jbl-l", "grupo": "alto-falante", "categoria": "médio-grave", "nome": "JBL L", "modelo": "JBL 4TRMS80",
                  "nota": "Trocada em 2025.",
                  "dados": { "impedancia": 3.6 }, "ligado": { "equipamento": "herdado-modulo", "canal": 1 } },
                { "id": "herdado-jbl-r", "grupo": "alto-falante", "categoria": "médio-grave", "nome": "JBL R", "modelo": "JBL 4TRMS80",
                  "nota": "JBL 4TRMS80, 3,6 Ω, com passa-alta mínimo de 100 Hz a 24 dB/oit (ficha do fabricante).",
                  "dados": { "impedancia": 3.6 }, "ligado": { "equipamento": "herdado-modulo", "canal": 2 } }
              ],
              "recentes": [ "herdado-memoria-filme" ], "botoes": [ "herdado-memoria-filme", null, null ]
            },
            {
              "id": "setup-carro", "nome": "Carro", "quando": "2026-10-25T10:00:00-03:00",
              "equilibrio": { "ganho": [1, 0, 0, 0], "limiter": [0, 0, 0, 0] },
              "link12": true, "link34": false,
              "calibracao": [],
              "equipamentos": [
                { "id": "eq-1", "grupo": "ativo", "categoria": "caixa ativa 2 vias", "nome": "Caixa",
                  "dados": { "impedancia": 8, "potenciaRms": 100 }, "entradas": [ { "n": 1, "fonte": "CH1" } ] },
                { "id": "eq-2", "categoria": "sem grupo", "nome": "Ilegível" }
              ],
              "recentes": [ "memoria-estrada" ], "botoes": [ "memoria-estrada", null, null ]
            }
          ],
          "memorias": [
            { "id": "herdado-memoria-filme", "setup": "herdado-setup-casa-2-1", "nome": "FILME", "preset": "herdado-cinema-2026-10-18",
              "carater": [ { "codigo": "081", "indice": 0, "noFio": "+10.0" } ] },
            { "id": "memoria-estrada", "setup": "setup-carro", "nome": "ESTRADA", "carater": [] }
          ]
        }
        """;

    private const string PresetsFormato2 = """
        {"formato":2,"presets":[{"id":"preset-rock","nome":"Rock","quando":"2026-10-20T10:00:00-03:00",
          "valores":[{"codigo":"020","indice":0,"noFio":"+3.0"}]}]}
        """;

    private const string PlaylistsDasMemoriasQueSumiram = """
        {"formato":2,"playlists":[
          {"memoria":"herdado-memoria-filme","faixas":[{"id":"a","uri":"content://a"}]},
          {"memoria":"memoria-estrada","faixas":[{"id":"b","uri":"content://b"}]}],
         "pendentes":[{"nome":"MUSICA ORIG","faixas":[{"id":"c","uri":"content://c"}]}]}
        """;

    [Fact]
    public async Task Migracao_do_formato_2_ficam_os_Setups_e_os_equipamentos_e_somem_memorias_presets_e_playlists()
    {
        using var t = new SetupsTestes(SetupsFormato2, null, PlaylistsDasMemoriasQueSumiram, PresetsFormato2);
        await t._setups.Carregada;
        await t._presets.Carregada;
        await t._playlists.Carregada;

        var dados = t._setups.Dados;
        Assert.Equal([Casa, "setup-carro"], dados.Setups.Select(s => s.Id));
        Assert.Equal("setup-carro", dados.UltimoSetup);
        Assert.Empty(dados.Memorias);
        Assert.All(dados.Setups, s => Assert.Empty(dados.UsoDe(s.Id).Recentes));
        Assert.All(dados.Setups, s => Assert.Equal([null, null, null], dados.UsoDe(s.Id).Botoes));
        // O Carro: o equipamento legível fica, com os dados digitados; o ilegível é descartado.
        var caixa = Assert.Single(dados.Setups[1].Equipamentos);
        Assert.Equal(new DadoTecnico(8m, OrigemDado.Digitado), caixa.Dados.Impedancia);
        Assert.Equal(new DadoTecnico(100m, OrigemDado.Digitado), caixa.Dados.PotenciaRms);
        // O Casa 2.1 é um Setup comum: os equipamentos dele ficam como estavam, com os dados digitados;
        // nada vem de ficha herdada (o app nasce zerado, 8g). As ligações ficam.
        var casa = dados.Setups[0];
        Assert.Equal("Casa 2.1", casa.Nome);
        var modulo = casa.Equipamentos.Single(e => e.Id == CasaDoDenis.IdModuloCasa);
        Assert.Equal("Módulo genérico", modulo.Nome);
        Assert.Null(modulo.Modelo);
        Assert.Equal(new DadoTecnico(19.6m, OrigemDado.Digitado), modulo.Dados.Ganho);
        Assert.Equal([new Bridge(3, 4)], modulo.Bridges);
        var jbl = casa.Equipamentos.Single(e => e.Id == CasaDoDenis.IdJblL);
        Assert.Null(jbl.Dados.Re);
        Assert.Equal(new DadoTecnico(3.6m, OrigemDado.Digitado), jbl.Dados.Impedancia);
        Assert.Equal(LigacaoAltoFalante.NoCanal(CasaDoDenis.IdModuloCasa, 1), jbl.Ligado);
        Assert.Equal("Trocada em 2025.", jbl.Nota); // a nota do usuário fica
        Assert.Equal("JBL 4TRMS80, 3,6 Ω, com passa-alta mínimo de 100 Hz a 24 dB/oit (ficha do fabricante).",
            casa.Equipamentos.Single(e => e.Id == CasaDoDenis.IdJblR).Nota); // a nota guardada fica como estava
        // Os arquivos já saem no formato 3; os presets e as playlists das memórias que sumiram, também.
        Assert.Equal(GerenteSetups.Formato, GerenteSetups.FormatoDe(t._guardadoSetups.Texto!));
        Assert.Equal(GerenteSetups.Formato, GerenteSetups.FormatoDe(GerenteSetups.ParaJson(dados)));
        Assert.Empty(t._presets.Presets);
        Assert.Equal(GerentePresets.Formato, GerentePresets.FormatoDe(t._guardadoPresets.Texto!));
        Assert.Empty(t._playlists.Playlist("herdado-memoria-filme"));
        Assert.Empty(t._playlists.Playlist("memoria-estrada"));
        Assert.Empty(GerentePlaylists.DeJson(t._guardadoPlaylists.Texto!).PorMemoria);
        // As pendentes antigas eram das memórias de antes: somem também.
        Assert.Empty(t._playlists.Pendentes);
        Assert.Empty(GerentePlaylists.DeJson(t._guardadoPlaylists.Texto!).Pendentes);
    }

    [Fact]
    public async Task Terminar_a_escolha_de_um_Setup_que_ficou_sem_memorias_na_migracao_desmuta_e_confere()
    {
        // O app fechou no meio de "Escolher o Setup Casa 2.1": a marca da cópia e a do mudo ficaram, e o
        // setups.json era do formato 2 (as memórias somem na migração).
        var copia = CopiaAntes.ParaJson(new Copia(DateTimeOffset.UnixEpoch,
            Enumerable.Range(0, 4).Select(c => new ValorCopiado("100", c, "1")).ToArray(),
            new OperacaoPendente(GerenteSetups.TipoAplicarSetup, Casa, "Escolher o Setup Casa 2.1", "Casa 2.1")));
        var mudo = GerenteSetups.MarcaParaJson(new MarcaMudo(Casa, ["0", "0", "0", "0"], DateTimeOffset.UnixEpoch));
        using var t = new SetupsTestes(SetupsFormato2, mudo, copiaGuardada: copia);
        for (var c = 0; c < 4; c++)
            t._simulador.Definir("100", c, "1");
        await t.Conectar();
        await t.AndarAte(() => t._copia.PelaMetade is not null);
        Assert.Equal(EstadoConferencia.Duvida, t._setups.Estado);
        Assert.Empty(t._setups.MemoriasDe(Casa));

        var r = await t.Rodar(t._copia.TerminarAsync());

        Assert.True(r.Ok, r.Motivo);
        Assert.Null(t._copia.PelaMetade);
        Assert.True(t.Mutados(false));
        Assert.Null(t._setups.Marca);
        Assert.Equal(EstadoConferencia.Confere, t._setups.Estado);
        Assert.Equal(Casa, t._setups.SetupEmUso!.Id);
    }

    [Fact]
    public async Task Migracao_do_formato_1_o_Casa_2_1_fica_sem_equipamentos_e_as_memorias_somem()
    {
        const string formato1 = """
            {"formato":1,"ultimoSetup":"herdado-setup-casa-2-1","setups":[
              {"id":"herdado-setup-casa-2-1","nome":"Casa 2.1","equilibrio":{"ganho1":0,"limiter1":0,"ganho2":-3,"limiter2":-3},
               "link12":false,"link34":false,"calibracao":[],"recentes":["herdado-memoria-filme"],"botoes":["herdado-memoria-filme",null,null]}],
             "memorias":[{"id":"herdado-memoria-filme","setup":"herdado-setup-casa-2-1","nome":"FILME","carater":[]}]}
            """;
        using var t = new SetupsTestes(formato1, null);
        await t._setups.Carregada;

        var casa = Assert.Single(t._setups.Setups);
        Assert.Equal("Casa 2.1", casa.Nome);
        Assert.Empty(casa.Equipamentos); // nada de equipamentos herdados
        Assert.Empty(t._setups.Dados.Memorias);
        Assert.Equal(GerenteSetups.Formato, GerenteSetups.FormatoDe(t._guardadoSetups.Texto!));
    }

    [Fact]
    public async Task Depois_da_migracao_a_primeira_conexao_fica_em_duvida_sem_nenhum_240_nem_23N()
    {
        using var t = new SetupsTestes(SetupsFormato2, null, null, PresetsFormato2);
        await t.Conectar();

        Assert.Equal(EstadoConferencia.Duvida, t._setups.Estado); // o Carro não tem mais memória
        Assert.Equal("setup-carro", t._setups.Referencia!.Id);
        Assert.True((await t.Rodar(t._setups.EscolherAsync(Casa))).Ok);
        Assert.Equal(EstadoConferencia.Confere, t._setups.Estado);
    }

    // ----------------------------------------------------------------- a base de modelos

    [Fact]
    public async Task Base_de_modelos_guarda_o_modelo_confirmado_uma_vez_so_com_a_ficha_mais_nova_e_sem_o_medido()
    {
        await _setups.Carregada;
        await _modelos.Carregada;
        var jbl = new Equipamento("rascunho", GrupoEquipamento.AltoFalante, "médio-grave", "JBL Centro", "  jbl 4trms80 ")
        {
            Dados = new DadosTecnicos
            {
                Fs = new DadoTecnico(99m, OrigemDado.Oficial, "https://jbl.example/ficha"),
                Re = new DadoTecnico(3.5m, OrigemDado.Medido),
                Vas = new DadoTecnico(2.1m, OrigemDado.OutraFonte, "Fórum de áudio"),
                Sd = new DadoTecnico(60m, OrigemDado.Digitado),
            },
        };

        var r = await _setups.AdicionarEquipamentoAsync(Casa, jbl);

        Assert.True(r.Ok, r.Mensagem);
        Assert.Equal(3, _modelos.Modelos.Count); // o modelo uma vez só
        var ficha = _modelos.Ficha("JBL 4TRMS80")!;
        Assert.Equal("JBL 4TRMS80", ficha.Modelo); // o nome com que o modelo entrou na base fica
        Assert.Equal(new DadoTecnico(99m, OrigemDado.Oficial, "https://jbl.example/ficha"), ficha.Dados.Fs);
        Assert.Equal(OrigemDado.OutraFonte, ficha.Dados.Vas!.Origem);
        Assert.Null(ficha.Dados.Re); // o medido é da instalação
        Assert.Null(ficha.Dados.Sd); // o digitado não é confirmado
        Assert.Null(ficha.Dados.Xmax); // a ficha mais nova substitui a de antes
        Assert.Equal(GrupoEquipamento.AltoFalante, ficha.Grupo);
        Assert.Equal(["JBL 4TRMS80"], _modelos.Sugerir("jbl"));
        Assert.Equal(["Protech Light 8\""], _modelos.Sugerir(" PRO"));
        Assert.Empty(_modelos.Sugerir(""));
        // Guardada no modelos.json e lida de volta.
        var lidos = BaseModelos.DeJson(_guardadoModelos.Texto!)!;
        Assert.Equal(3, lidos.Count);
        Assert.Equal(ficha.Dados.Fs, lidos.Single(m => BaseModelos.Chave(m.Modelo) == "JBL 4TRMS80").Dados.Fs);

        // Confirmar o modelo de novo só com dados digitados não apaga a ficha guardada.
        var soDigitado = jbl with { Nome = "JBL Fundo", Dados = new DadosTecnicos { Fs = DadoTecnico.Digitado(120m) } };
        Assert.True((await _setups.AdicionarEquipamentoAsync(Casa, soDigitado)).Ok);
        Assert.Same(ficha, _modelos.Ficha("jbl 4trms80"));

        // Uma base nova lida do modelos.json guardado tem o modelo com as origens.
        var reaberta = new BaseModelos(new TextoMemoria(_guardadoModelos.Texto), _log, _relogio);
        await reaberta.Carregada;
        var relida = reaberta.Ficha("JBL 4TRMS80")!;
        Assert.Equal(ficha.Dados, relida.Dados);
        Assert.Equal(OrigemDado.OutraFonte, relida.Dados.Vas!.Origem);
        Assert.Equal("Fórum de áudio", relida.Dados.Vas.Fonte);
    }

    [Fact]
    public async Task Base_de_modelos_com_o_modelos_json_estragado_fica_vazia_sem_erro_e_registra_no_log()
    {
        var log = new LogBle();
        var estragada = new BaseModelos(new TextoMemoria("{ lixo"), log, _relogio);

        await estragada.Carregada;

        Assert.Empty(estragada.Modelos);
        Assert.Contains(log.Linhas, l => l.Contains("modelos.json ilegível", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Base_de_modelos_sem_arquivo_nasce_vazia()
    {
        var vazia = new BaseModelos(new TextoMemoria(), _log, _relogio);

        await vazia.Carregada;

        Assert.Empty(vazia.Modelos);
        Assert.Empty(vazia.Sugerir("JBL"));
        Assert.Null(vazia.Ficha("JBL 4TRMS80"));
    }

    [Fact]
    public async Task Base_de_modelos_nao_guarda_os_dados_da_alimentacao_do_modulo()
    {
        await _setups.Carregada;
        await _modelos.Carregada;
        var modulo = _setups.Equipamento(Casa, CasaDoDenis.IdModuloCasa)!;
        Assert.Equal(OrigemDado.Oficial, modulo.Dados.CorrenteAlimentacao!.Origem); // a BOB da casa

        Assert.True((await _setups.EditarEquipamentoAsync(Casa, modulo with { Nome = "Módulo" })).Ok);

        var ficha = _modelos.Ficha("SD800.4 EVO")!;
        Assert.Null(ficha.Dados.CorrenteAlimentacao);
        Assert.Null(ficha.Dados.TensaoAlimentacao);
        Assert.Equal(132m, ficha.Dados.PotenciaCanal!.Valor);
        Assert.Null(ficha.Dados.Ganho); // o ganho medido é da instalação
    }

    [Fact]
    public async Task Sem_conseguir_gravar_a_base_o_equipamento_fica_e_a_frase_avisa()
    {
        await _setups.Carregada;
        await _modelos.Carregada;
        _guardadoModelos.FalharAoGravar = true;
        var tweeter = new Equipamento("r", GrupoEquipamento.AltoFalante, "tweeter", "Tweeter", "ST200")
        {
            Dados = new DadosTecnicos { PotenciaRms = new DadoTecnico(100m, OrigemDado.Oficial, "Manual") },
        };

        var r = await _setups.AdicionarEquipamentoAsync(Casa, tweeter);

        Assert.True(r.Ok, r.Mensagem);
        Assert.EndsWith(GerenteSetups.MsgModeloNaoGuardado, r.Mensagem);
        Assert.Contains(_setups.Setup(Casa)!.Equipamentos, e => e.Nome == "Tweeter");
        Assert.Null(_modelos.Ficha("ST200"));
    }

    [Fact]
    public async Task Um_tipo_de_caixa_com_mais_de_32_caracteres_e_recusado_e_nada_e_guardado()
    {
        await _setups.Carregada;
        var gravacoes = _guardadoSetups.Gravacoes;
        var e = new Equipamento("r", GrupoEquipamento.AltoFalante, "woofer", "Woofer")
        {
            Dados = new DadosTecnicos { TipoCaixa = new DadoTexto(new string('d', 33), OrigemDado.Digitado) },
        };

        var r = await _setups.AdicionarEquipamentoAsync(Casa, e);

        Assert.False(r.Ok);
        Assert.Equal(Ligacoes.MsgTipoCaixaInvalido, r.Motivo);
        Assert.Equal(gravacoes, _guardadoSetups.Gravacoes);
        Assert.DoesNotContain(_setups.Setup(Casa)!.Equipamentos, x => x.Nome == "Woofer");
    }

    [Fact]
    public async Task Base_de_modelos_um_modelo_novo_entra_e_um_equipamento_sem_modelo_nao()
    {
        await _setups.Carregada;
        await _modelos.Carregada;
        var tweeter = new Equipamento("r", GrupoEquipamento.AltoFalante, "tweeter", "Tweeter", "ST200")
        {
            Dados = new DadosTecnicos { PotenciaRms = new DadoTecnico(100m, OrigemDado.Oficial, "Manual") },
        };
        var generico = new Equipamento("r2", GrupoEquipamento.AltoFalante, "tweeter", "Genérico")
        {
            Dados = new DadosTecnicos { PotenciaRms = new DadoTecnico(50m, OrigemDado.Oficial, "Manual") },
        };

        Assert.True((await _setups.AdicionarEquipamentoAsync(Casa, tweeter)).Ok);
        Assert.True((await _setups.AdicionarEquipamentoAsync(Casa, generico)).Ok);

        Assert.Equal(4, _modelos.Modelos.Count);
        Assert.Equal(100m, _modelos.Ficha("st200")!.Dados.PotenciaRms!.Valor);
    }

    [Fact]
    public async Task Uma_ficha_com_a_fonte_longa_demais_e_recusada()
    {
        await _setups.Carregada;
        var e = new Equipamento("r", GrupoEquipamento.AltoFalante, "tweeter", "Tweeter")
        {
            Dados = new DadosTecnicos { Fs = new DadoTecnico(2000m, OrigemDado.Oficial, new string('x', 121)) },
        };

        var r = await _setups.AdicionarEquipamentoAsync(Casa, e);

        Assert.False(r.Ok);
        Assert.Equal(Ligacoes.MsgFonteDadoInvalida, r.Motivo);
    }

    // ----------------------------------------------------------------- revisões

    [Fact]
    public async Task Ao_cair_a_conexao_o_Setup_em_uso_sai_ate_a_conferencia_nova()
    {
        await Conectar();
        Assert.NotNull(_setups.SetupEmUso);

        await _sessao.DesconectarAsync();

        Assert.Null(_setups.SetupEmUso);
        Assert.Equal(EstadoConferencia.Nenhuma, _setups.Estado);
        Assert.Equal(GerenteSetups.MsgSemSetup, (await _setups.CriarMemoriaAsync("NOVA")).Motivo);
        await Conectar();
        Assert.Equal(Casa, _setups.SetupEmUso!.Id);
    }

    [Fact]
    public void A_marca_nao_guardada_le_uma_vez_so_dentro_do_motivo_do_mudo() =>
        Assert.Equal("O som não foi mutado: não consegui guardar a marca do mudo no celular",
            GerenteSetups.MsgNaoMutou(GerenteSetups.MsgNaoGuardouMarca));

    [Fact]
    public void A_funcao_sugerida_usa_o_corte_da_memoria_ativa_e_sem_memoria_so_a_categoria()
    {
        var casa = CasaDoDenis.SetupCasa;
        var caixa = new Equipamento("cx", GrupoEquipamento.AltoFalante, "caixa", "Caixa") { Ligado = LigacaoAltoFalante.NoBridge(CasaDoDenis.IdModuloCasa, new Bridge(3, 4)) };

        Assert.Equal(FuncaoAcustica.Sub, GerenteSetups.FuncaoSugerida(casa, CenarioFilme.Memoria, caixa)); // passa-baixa 150 Hz no CH3
        Assert.Equal(FuncaoAcustica.FullRange, GerenteSetups.FuncaoSugerida(casa, null, caixa));
        Assert.Equal(FuncaoAcustica.Sub, GerenteSetups.FuncaoSugerida(casa, null, caixa with { Categoria = "subwoofer" }));
    }

    // ----------------------------------------------------------------- o cenário inteiro

    [Fact]
    public async Task Conectar_duvida_escolher_criar_aplicar_e_salvar_sem_nenhum_240_241_nem_23N()
    {
        await EntrarNaDuvida();
        Assert.True((await Rodar(_setups.EscolherAsync(Casa))).Ok);
        Assert.True((await Rodar(_escritor.EscreverAsync(Ganho, 0, 5.0m))).Ok);
        Assert.True((await Rodar(_setups.CriarMemoriaAsync("MUSICA ORIG"))).Ok);
        Assert.True((await Rodar(_setups.AplicarAsync(Filme))).Ok);
        Assert.True((await Rodar(_escritor.EscreverAsync(Atraso, 0, 1.0m))).Ok);
        Assert.True((await _setups.SalvarAsync(null)).Ok);
        Assert.True((await Rodar(_setups.TrocarAsync())).Ok);
        Assert.True((await Rodar(_setups.DesistirAsync())).Ok);

        Assert.DoesNotContain(_simulador.Escritas, e => e.StartsWith("*24", StringComparison.Ordinal) || e.StartsWith("*23", StringComparison.Ordinal));
        Assert.Equal(2, _setups.MemoriasDe(Casa).Count);
    }

    // ----------------------------------------------------------------- 8d: o preset lembrado e as 15 bandas

    /// <summary>O "Rock": o Cinema com 160 Hz em +4 e 10 kHz em −3.</summary>
    private async Task<Preset> CriarRock()
    {
        var bandas = CenarioFilme.Bandas
            .Select(v => v.Indice switch { 4 => v with { NoFio = "+4.0" }, 13 => v with { NoFio = "-3.0" }, _ => v })
            .ToArray();
        var r = await _presets.CriarAsync("Rock", bandas);
        Assert.True(r.Ok, r.Mensagem);
        return r.Preset!;
    }

    private static bool EhBanda(string quadro) => Catalogo.EscritaDeAudio(quadro)?.Parametro.Codigo == "020";

    [Fact]
    public async Task Aplicar_um_preset_com_a_FILME_em_uso_muda_so_as_bandas_e_a_FILME_passa_a_lembrar_ele()
    {
        await Conectar();
        Assert.Equal(SomEmUso.DoCelular(Filme), _setups.EmUso);
        Assert.Equal(Cinema, _setups.IdPresetEmUso);
        var rock = await CriarRock();
        var antes = _simulador.EstadoMarco1();
        var inicio = _simulador.Escritas.Count;

        var r = await Rodar(_presets.AplicarAsync(rock.Id));

        Assert.True(r.Ok, r.Mensagem);
        Assert.Equal([Banda.CodificarEscrita(4, 4.0m), Banda.CodificarEscrita(13, -3.0m)], AudioDesde(inicio));
        var depois = _simulador.EstadoMarco1();
        Assert.All(depois.Where(kv => kv.Key.Codigo != "020"), kv => Assert.Equal(antes[kv.Key], kv.Value));
        Assert.Empty(Camadas.Diferentes(rock.Valores, _estado.Copiar()));
        Assert.Equal(rock.Id, _setups.Memoria(Filme)!.IdPreset);
        Assert.Equal(rock.Id, _setups.IdPresetEmUso);
        Assert.Same(rock, _setups.PresetEmUso);
        Assert.Equal(rock.Id, GerenteSetups.DeJson(_guardadoSetups.Texto!)!.Memorias.Single(m => m.Id == Filme).IdPreset);
        Assert.Equal(EstadoConferencia.Confere, _setups.Estado);
    }

    [Fact]
    public async Task Aplicar_a_FILME_com_as_bandas_planas_traz_o_Cinema_e_o_espelho_fica_igual_a_leitura_de_18_10()
    {
        foreach (var b in GerentePresets.BandasPlanas)
            _simulador.Definir("020", b.Indice, b.NoFio);
        _simulador.Definir("081", 0, "+1.0");
        await ConectarSemReaplicar();
        Assert.Equal(EstadoConferencia.SomDiferente, _setups.Estado); // as bandas não entram na conferência
        var inicio = _simulador.Escritas.Count;

        var r = await Rodar(_setups.AplicarAsync(Filme));

        Assert.True(r.Ok, r.Mensagem);
        Assert.Equal("Memória FILME aplicada.", r.Mensagem);
        var audio = AudioDesde(inicio);
        Assert.Contains("*081*0*10.0*", audio);
        Assert.Equal(6, audio.Count(EhBanda));
        var espelho = _estado.Copiar();
        foreach (var v in CasaDoDenis.LeituraFilme.Where(v => Camadas.De(v.Codigo) != Camada.Fora && v.Codigo != "220"))
            Assert.True(Camadas.Iguais(v, espelho), $"{v.Codigo} {v.Indice}");
        Assert.Equal(Cinema, _setups.IdPresetEmUso);
        Assert.Null(_copia.PelaMetade);
    }

    [Fact]
    public async Task Uma_memoria_que_lembra_um_preset_que_sumiu_aplica_sem_as_bandas_e_avisa()
    {
        var editado = CenarioFilme.Dados with { Memorias = [CenarioFilme.Memoria with { IdPreset = "preset-que-sumiu" }] };
        using var t = new SetupsTestes(GerenteSetups.ParaJson(editado), null);
        await t.Conectar();
        Assert.True((await t.Rodar(t._escritor.EscreverAsync(Banda, 4, 3.0m))).Ok);
        Assert.True((await t.Rodar(t._escritor.EscreverAsync(Ganho, 0, 4.0m))).Ok);
        var inicio = t._simulador.Escritas.Count;

        var r = await t.Rodar(t._setups.AplicarAsync(Filme));

        Assert.True(r.Ok, r.Mensagem);
        Assert.Equal($"Memória FILME aplicada. {GerenteSetups.MsgPresetSumiu}", r.Mensagem);
        Assert.Equal(["*081*0*10.0*"], t.AudioDesde(inicio));
        Assert.Equal("+3.0", t._simulador.Valor("020", 4));
        Assert.Null(t._setups.PresetEmUso);
    }

    [Fact]
    public async Task Apagar_um_preset_lembrado_por_duas_memorias_e_recusado()
    {
        await Conectar();
        var rock = await CriarRock();
        Assert.True((await Rodar(_presets.AplicarAsync(rock.Id))).Ok);
        Assert.True((await Rodar(_setups.CriarMemoriaAsync("SHOW"))).Ok);
        Assert.Equal(rock.Id, _setups.MemoriasDe(Casa).Single(m => m.Nome == "SHOW").IdPreset);

        var r = await _presets.ApagarAsync(rock.Id);

        Assert.False(r.Ok);
        Assert.Equal("Lembrado por 2 memórias.", r.Motivo);
        Assert.Contains(_presets.Proprios, p => p.Id == rock.Id);

        Assert.True((await Rodar(_presets.AplicarAsync(Cinema))).Ok); // a SHOW passa a lembrar o Cinema
        Assert.Equal("Lembrado por 1 memória.", (await _presets.ApagarAsync(rock.Id)).Motivo);
    }

    [Fact]
    public async Task Salvar_as_bandas_mexidas_na_FILME_pergunta_atualizar_o_Cinema_ou_criar_um_novo()
    {
        await Conectar();
        Assert.True((await Rodar(_escritor.EscreverAsync(Banda, 4, 3.0m))).Ok); // 160 Hz

        var folha = _setups.PrepararSalvar();

        Assert.Null(folha.Trava);
        var linha = Assert.Single(folha.Linhas);
        Assert.Equal(Camada.Bandas, linha.Camada);
        Assert.Equal("15 bandas → preset Cinema", linha.Titulo);
        Assert.Equal("Banda 160 Hz: +1,0 dB → +3,0 dB", Assert.Single(linha.Mudancas).Linha);
        Assert.True(folha.PerguntaPreset);
        Assert.Equal("Cinema", folha.NomePreset);
        Assert.Null(folha.MotivoAtualizarTravado); // não há mais preset herdado
        Assert.Equal(EscolhaPreset.Nenhuma, folha.EscolhaPresetInicial);
        Assert.False(folha.PedeNome);
        Assert.Equal(GerenteSetups.MsgEscolhaPreset, (await _setups.SalvarAsync(null)).Motivo);
        Assert.Equal("Nome do preset: Já existe um preset com esse nome.",
            (await _setups.SalvarAsync(null, EscolhaPreset.Criar, "cinema")).Motivo);
        var inicio = _simulador.Escritas.Count;

        var r = await _setups.SalvarAsync(null, EscolhaPreset.Criar, "CINEMA 2");

        Assert.True(r.Ok, r.Mensagem);
        Assert.Equal("Salvo: 15 bandas no preset CINEMA 2.", r.Mensagem);
        var novo = _presets.Proprios.Single(p => p.Nome == "CINEMA 2");
        Assert.Equal("+3.0", novo.Valores.Single(v => v.Indice == 4).NoFio);
        Assert.Equal(novo.Id, _setups.Memoria(Filme)!.IdPreset);
        Assert.Equal("+1.0", _presets.Obter(Cinema)!.Valores.Single(v => v.Indice == 4).NoFio);
        Assert.Equal(inicio, _simulador.Escritas.Count);
        Assert.Equal(GerenteSetups.MsgNadaParaSalvar, _setups.PrepararSalvar().Trava);
    }

    [Fact]
    public async Task Salvar_as_bandas_e_a_regulagem_com_Atualizar_o_preset_lembrado()
    {
        await Conectar();
        var rock = await CriarRock();
        Assert.True((await Rodar(_presets.AplicarAsync(rock.Id))).Ok);
        Assert.True((await Rodar(_escritor.EscreverAsync(Banda, 0, 5.0m))).Ok);
        Assert.True((await Rodar(_escritor.EscreverAsync(Ganho, 0, 8.0m))).Ok);

        var folha = _setups.PrepararSalvar();

        Assert.Equal(["Regulagem → memória FILME", "15 bandas → preset Rock"], folha.Linhas.Select(l => l.Titulo));
        Assert.True(folha.PerguntaPreset);
        Assert.Null(folha.MotivoAtualizarTravado);

        var r = await _setups.SalvarAsync(null, EscolhaPreset.Atualizar);

        Assert.True(r.Ok, r.Mensagem);
        Assert.Equal("Salvo: regulagem na FILME, 15 bandas no preset Rock.", r.Mensagem);
        var atualizado = _presets.Proprios.Single(p => p.Id == rock.Id);
        Assert.Equal("+5.0", atualizado.Valores.Single(v => v.Indice == 0).NoFio);
        Assert.Equal(rock.Id, _setups.Memoria(Filme)!.IdPreset);
        Assert.Equal("+8.0", Valor(_setups.Memoria(Filme)!.Regulagem, "081", 0));
        Assert.Equal(GerenteSetups.MsgNadaParaSalvar, _setups.PrepararSalvar().Trava);
    }

    [Fact]
    public async Task Salvar_uma_memoria_nova_com_bandas_diferentes_de_todos_pede_o_nome_do_preset_novo()
    {
        _simulador.Definir("020", 0, "+6.0");
        _simulador.Definir("081", 0, "+4.0"); // o ganho também difere da FILME: o som do aparelho
        await ConectarSemReaplicar();
        Assert.Equal(SomEmUso.SomDoAparelho, _setups.EmUso);

        var folha = _setups.PrepararSalvar();

        Assert.Contains(folha.Linhas, l => l.Titulo == "15 bandas → preset novo");
        Assert.True(folha.PedeNomePreset);
        Assert.Equal(GerenteSetups.MsgNomePreset(GerentePresets.MsgNomeInvalido), (await _setups.SalvarAsync("MUSICA ORIG")).Motivo);
        Assert.DoesNotContain(_setups.MemoriasDe(Casa), m => m.Nome == "MUSICA ORIG");

        var r = await _setups.SalvarAsync("MUSICA ORIG", EscolhaPreset.Nenhuma, "ORIG EQ");

        Assert.True(r.Ok, r.Mensagem);
        Assert.Equal("Salvo: regulagem na MUSICA ORIG, 15 bandas no preset ORIG EQ.", r.Mensagem);
        var preset = _presets.Proprios.Single(p => p.Nome == "ORIG EQ");
        Assert.Equal("+6.0", preset.Valores.Single(v => v.Indice == 0).NoFio);
        var nova = _setups.MemoriasDe(Casa).Single(m => m.Nome == "MUSICA ORIG");
        Assert.Equal(preset.Id, nova.IdPreset);
        Assert.Equal(SomEmUso.DoCelular(nova.Id), _setups.EmUso);
    }

    [Fact]
    public async Task O_preset_em_uso_e_o_ultimo_aplicado_sem_memoria_do_celular_e_nenhum_depois_de_cair()
    {
        using var t = SoCasa();
        await t.Conectar();
        Assert.True((await t.Rodar(t._setups.EscolherAsync(Casa))).Ok);
        var rock = await t.CriarRock();
        Assert.True((await t.Rodar(t._presets.AplicarAsync(rock.Id))).Ok); // sem memória do celular em uso
        Assert.Equal(rock.Id, t._setups.IdPresetEmUso);

        Assert.True((await t.Rodar(t._setups.CriarMemoriaAsync("ESTRADA"))).Ok);

        Assert.Equal(rock.Id, t._setups.MemoriasDe(Casa).Single().IdPreset);
        Assert.Equal(rock.Id, t._setups.IdPresetEmUso);
        await t._sessao.DesconectarAsync();
        Assert.Null(t._setups.IdPresetEmUso);
    }

    [Fact]
    public async Task Escolher_o_Setup_com_as_bandas_diferentes_do_preset_da_memoria_ativa_escreve_as_bandas_de_volta()
    {
        _simulador.Definir("020", 4, "+3.0");
        await EntrarNaDuvida();
        var inicio = _simulador.Escritas.Count;

        var r = await Rodar(_setups.EscolherAsync(Casa));

        Assert.True(r.Ok, r.Mensagem);
        Assert.Contains(Banda.CodificarEscrita(4, 1.0m), AudioDesde(inicio));
        Assert.Empty(Camadas.Diferentes(CenarioFilme.Bandas, _estado.Copiar()));
    }

    [Fact]
    public async Task Escolher_o_Setup_com_o_preset_lembrado_sumido_avisa_no_fim_da_frase()
    {
        var editado = CenarioFilme.Dados with { Memorias = [CenarioFilme.Memoria with { IdPreset = "preset-que-sumiu" }] };
        using var t = new SetupsTestes(GerenteSetups.ParaJson(editado), null);
        t.MexerNoAtraso();
        await t.Conectar();
        Assert.Equal(EstadoConferencia.Duvida, t._setups.Estado);

        var r = await t.Rodar(t._setups.EscolherAsync(Casa));

        Assert.True(r.Ok, r.Mensagem);
        Assert.EndsWith(GerenteSetups.MsgPresetSumiu, r.Mensagem);
    }

    // ----------------------------------------------------------------- 8d: playlists por memória

    private const string PlaylistsAntigas = """
        {"formato":1,"playlists":[
          [],
          [{"id":"o1","uri":"content://o1"},{"id":"o2","uri":"content://o2"}],
          [{"id":"f1","uri":"content://f1"},{"id":"f2","uri":"content://f2"},{"id":"f3","uri":"content://f3"}]]}
        """;

    private static SetupsTestes ComFilme(string? playlists) => new(CenarioFilme.SetupsJson, null, playlists, CenarioFilme.PresetsJson);

    [Fact]
    public async Task O_playlists_json_antigo_vira_as_pendentes_Memoria_2_e_Memoria_3_e_a_FILME_nao_adota_nada()
    {
        using var t = ComFilme(PlaylistsAntigas);

        await t._setups.Carregada;
        await t._playlists.Carregada;

        Assert.Empty(t._playlists.Playlist(Filme)); // nenhuma nota herdada liga a posição 3 à FILME
        Assert.Equal(["Memória 2", "Memória 3"], t._playlists.Pendentes.Select(p => p.Nome));
    }

    [Fact]
    public async Task Sem_setups_json_as_playlists_antigas_esperam_e_criar_o_Setup_com_a_memoria_Memoria_2_adota()
    {
        // Sem setups.json não há migração: as pendentes do playlists.json antigo esperam a dona.
        using var t = SemArquivos(PlaylistsAntigas);
        await t._setups.Carregada;
        await t._playlists.Carregada;
        Assert.Equal(2, t._playlists.Pendentes.Count);
        t.MexerNoAtraso();
        await t.Conectar();
        Assert.Equal(EstadoConferencia.Duvida, t._setups.Estado);

        Assert.True((await t.Rodar(t._setups.CriarSetupAsync("Carro", "Memória 2"))).Ok);

        var nova = Assert.Single(t._setups.MemoriasDe(t._setups.SetupEmUso!.Id));
        Assert.Equal(["content://o1", "content://o2"], t._playlists.Playlist(nova.Id).Select(f => f.Uri));
        Assert.Equal("Memória 3", Assert.Single(t._playlists.Pendentes).Nome);
    }

    [Fact]
    public async Task Adocao_tardia_criar_a_memoria_Memoria_2_traz_as_2_faixas_e_a_pendente_sai()
    {
        using var t = ComFilme(PlaylistsAntigas);
        await t.Conectar();

        Assert.True((await t.Rodar(t._setups.CriarMemoriaAsync("Memória 2"))).Ok);

        var nova = t._setups.MemoriasDe(Casa).Single(m => m.Nome == "Memória 2");
        Assert.Equal(["content://o1", "content://o2"], t._playlists.Playlist(nova.Id).Select(f => f.Uri));
        Assert.Equal("Memória 3", Assert.Single(t._playlists.Pendentes).Nome);
    }

    [Fact]
    public async Task Renomear_uma_memoria_para_o_nome_da_pendente_tambem_adota()
    {
        using var t = ComFilme(PlaylistsAntigas);
        await t.Conectar();
        Assert.True((await t.Rodar(t._setups.CriarMemoriaAsync("NOVA"))).Ok);
        var nova = t._setups.MemoriasDe(Casa).Single(m => m.Nome == "NOVA");
        Assert.Empty(t._playlists.Playlist(nova.Id));

        Assert.True((await t._setups.RenomearMemoriaAsync(nova.Id, "memória 2")).Ok);

        Assert.Equal(2, t._playlists.Playlist(nova.Id).Count);
        Assert.Equal("Memória 3", Assert.Single(t._playlists.Pendentes).Nome);
    }

    [Fact]
    public async Task Adocao_pelo_Salvar_a_memoria_nova_Memoria_2_recebe_as_2_faixas_e_a_pendente_sai()
    {
        using var t = ComFilme(PlaylistsAntigas);
        t._simulador.Definir("081", 0, "+4.0"); // só o ganho difere da FILME: o som do aparelho vai para uma memória nova
        await t.ConectarSemReaplicar();
        Assert.Equal(SomEmUso.SomDoAparelho, t._setups.EmUso);

        var r = await t._setups.SalvarAsync("Memória 2");

        Assert.True(r.Ok, r.Mensagem);
        var nova = t._setups.MemoriasDe(Casa).Single(m => m.Nome == "Memória 2");
        Assert.Equal(["content://o1", "content://o2"], t._playlists.Playlist(nova.Id).Select(f => f.Uri));
        Assert.Equal("Memória 3", Assert.Single(t._playlists.Pendentes).Nome);
    }

    [Fact]
    public void A_ordem_de_adocao_e_a_dos_Setups_e_dentro_de_cada_um_a_das_memorias_sem_privilegio_para_o_Casa_2_1()
    {
        var casa = CasaDoDenis.SetupCasa;
        var carro = casa with { Id = "carro", Nome = "Carro" };
        var sitio = casa with { Id = "sitio", Nome = "Sitio" };
        MemoriaCelular M(string id, string setup) => new(id, setup, id, DateTimeOffset.UnixEpoch, []);
        var dados = new DadosSetups([carro, casa, sitio],
            [M("carro-1", "carro"), M("casa-1", casa.Id), M("sitio-1", "sitio"), M("casa-2", casa.Id), M("carro-2", "carro")],
            casa.Id, new Dictionary<string, UsoDoSetup>());

        var ordem = GerenteSetups.OrdemDeAdocao(dados);

        Assert.Equal(["carro-1", "carro-2", "casa-1", "casa-2", "sitio-1"], ordem.Select(m => m.Id));
    }

    [Fact]
    public async Task Apagar_uma_memoria_apaga_a_playlist_dela()
    {
        await Conectar();
        Assert.True((await Rodar(_setups.CriarMemoriaAsync("SHOW"))).Ok);
        var show = _setups.MemoriasDe(Casa).Single(m => m.Nome == "SHOW");
        Assert.True((await _playlists.AdicionarAsync(show.Id, [new ArquivoAudio("content://a", "A", "M", TimeSpan.Zero)])).Ok);
        Assert.True((await _playlists.AdicionarAsync(Filme, [new ArquivoAudio("content://b", "B", "M", TimeSpan.Zero)])).Ok);
        Assert.True((await Rodar(_setups.AplicarAsync(Filme))).Ok);

        Assert.True((await _setups.ApagarMemoriaAsync(show.Id)).Ok);

        Assert.Empty(_playlists.Playlist(show.Id));
        Assert.Single(_playlists.Playlist(Filme));
        Assert.False(GerentePlaylists.DeJson(_guardadoPlaylists.Texto!).PorMemoria.ContainsKey(show.Id));
    }

    [Fact]
    public async Task Apagar_um_Setup_apaga_as_playlists_das_memorias_dele()
    {
        await EntrarNaDuvida();
        Assert.True((await Rodar(_setups.CriarSetupAsync("Carro", "ESTRADA"))).Ok);
        var carro = _setups.SetupEmUso!.Id;
        var estrada = _setups.MemoriasDe(carro).Single();
        Assert.True((await _playlists.AdicionarAsync(estrada.Id, [new ArquivoAudio("content://a", "A", "M", TimeSpan.Zero)])).Ok);
        Assert.True((await _playlists.AdicionarAsync(Filme, [new ArquivoAudio("content://b", "B", "M", TimeSpan.Zero)])).Ok);
        Assert.True((await Rodar(_setups.TrocarAsync())).Ok);
        Assert.True((await Rodar(_setups.EscolherAsync(Casa))).Ok);

        Assert.True((await _setups.ApagarSetupAsync(carro)).Ok);

        Assert.Empty(_playlists.Playlist(estrada.Id));
        Assert.Single(_playlists.Playlist(Filme));
    }

    [Fact]
    public async Task Um_setups_json_ilegivel_nao_apaga_nenhuma_playlist()
    {
        const string playlists = """
            {"formato":2,"playlists":[{"memoria":"memoria-de-antes","faixas":[{"id":"a","uri":"content://a"}]}],"pendentes":[]}
            """;
        using var t = new SetupsTestes("{ isto não é json", null, playlists);

        await t._setups.Carregada;

        Assert.Empty(t._setups.Dados.Memorias); // voltou ao vazio
        Assert.Single(t._playlists.Playlist("memoria-de-antes"));
        Assert.Equal(0, t._guardadoPlaylists.Gravacoes);
    }
}

/// <summary>Dois equipamentos iguais campo a campo (as listas pelo conteúdo).</summary>
internal sealed class EquipamentoIgual : IEqualityComparer<Equipamento>
{
    public bool Equals(Equipamento? a, Equipamento? b) =>
        a is not null && b is not null
        && a with { Entradas = [], Bridges = [], Dados = DadosTecnicos.Nenhum } == b with { Entradas = [], Bridges = [], Dados = DadosTecnicos.Nenhum }
        && a.Entradas.SequenceEqual(b.Entradas) && a.Bridges.SequenceEqual(b.Bridges)
        && a.Dados with { Vias = [] } == b.Dados with { Vias = [] } && a.Dados.Vias.SequenceEqual(b.Dados.Vias);

    public int GetHashCode(Equipamento e) => e.Id.GetHashCode(StringComparison.Ordinal);
}
