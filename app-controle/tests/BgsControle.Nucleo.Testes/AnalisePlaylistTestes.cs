using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using BgsControle.Nucleo.Aparelho;
using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Gerador;
using BgsControle.Nucleo.Leitura;
using BgsControle.Nucleo.Player;
using BgsControle.Nucleo.Porta;
using BgsControle.Nucleo.Presets;
using BgsControle.Nucleo.Registro;
using BgsControle.Nucleo.Rendimento;
using BgsControle.Nucleo.Seguranca;
using BgsControle.Nucleo.Setups;
using BgsControle.Nucleo.Uso;
using BgsControle.Simulador;

namespace BgsControle.Nucleo.Testes;

/// <summary>
/// História 13d, a análise da playlist e o teto do Extra contra o processador simulado e o dublê do decodificador: o
/// Casa 2.1 da fixture calibrado, com o Bluetooth em −13 dB, e a memória PLANA em uso (o ganho +15 nas JBL, LR24 em
/// 200 Hz, lembrando o preset Plano; há também o Mil, com +6 dB em 1 kHz). Uma linha da matriz por teste.
/// </summary>
public sealed class AnalisePlaylistTestes : IDisposable
{
    private static readonly TemposSessao Tempos = new(
        Scan: TimeSpan.FromSeconds(1),
        TentativaConexao: TimeSpan.FromSeconds(1),
        PausaEntreTentativas: TimeSpan.FromMilliseconds(1),
        RespostaSenha: TimeSpan.FromSeconds(2),
        PrazoSenha: TimeSpan.FromSeconds(60));

    private static readonly TemposLeitura LeituraRapida = new(TimeSpan.Zero, TimeSpan.FromMilliseconds(100));
    private static readonly Regex Senha = new(@"^\*000\*[0-9A-Z]{4}\*$");
    private static readonly IPAddress Casa = IPAddress.Parse("192.168.0.10");

    private const string IdPlana = "memoria-plana";
    private const string IdPlano = "preset-plano";
    private const string IdMil = "preset-mil";
    private const decimal Bluetooth = -13m;
    private const decimal GanhoJbl = 15m;

    private readonly RelogioFalso _relogio = new();
    private readonly ProcessadorSimulado _simulador;
    private readonly CofreFalso _cofre = new() { Senha = "AB12" };
    private readonly Chaves _chaves;
    private readonly LogBle _log = new();
    private readonly Historico _historico;
    private readonly UsoCompartilhado _uso;
    private readonly Sessao _sessao;
    private readonly LeituraAutomatica _leitura;
    private readonly EstadoAparelho _estado;
    private readonly Escritor _escritor;
    private readonly CopiaAntes _copia;
    private readonly LinkCanais _link;
    private readonly GerentePresets _presets;
    private readonly GerentePlaylists _playlists;
    private readonly GerenteSetups _setups;
    private readonly DecodificadorSintetico _decodificador = new();
    private readonly TextoMemoria _arquivoAnalises = new();
    private readonly ReprodutorTeste _reprodutor = new();
    private readonly Player.Player _player;
    private AnalisePlaylist _analise;

    public AnalisePlaylistTestes() : this(CasaDoDenis.SetupCasaComBluetooth)
    {
    }

    private AnalisePlaylistTestes(Setup setup)
    {
        var regulagem = PiorCasoTestes.Plana(GanhoJbl).Where(v => Camadas.De(v.Codigo) == Camada.Regulagem).ToArray();
        var plano = new Preset(IdPlano, "Plano", CasaDoDenis.QuandoFilme, GerentePresets.BandasPlanas);
        // A banda 8 é a de 1 kHz.
        var mil = new Preset(IdMil, "Mil", CasaDoDenis.QuandoFilme,
            GerentePresets.BandasPlanas.Select(v => v.Indice == 8 ? v with { NoFio = "+6.0" } : v).ToArray());
        var memoria = new MemoriaCelular(IdPlana, CasaDoDenis.IdSetupCasa, "PLANA", CasaDoDenis.QuandoFilme, regulagem, IdPlano);
        var dados = new DadosSetups([setup], [memoria], CasaDoDenis.IdSetupCasa,
            new Dictionary<string, UsoDoSetup> { [CasaDoDenis.IdSetupCasa] = new([IdPlana], [IdPlana, null, null]) });

        _simulador = new ProcessadorSimulado(_relogio) { AtrasoSenha = TimeSpan.Zero, AtrasoLeitura = TimeSpan.Zero };
        foreach (var v in regulagem.Concat(plano.Valores))
            _simulador.Definir(v.Codigo, v.Indice, v.NoFio);
        _chaves = new Chaves(new RepositorioMemoria());
        _historico = new Historico(new TextoMemoria(), _relogio, _log);
        _uso = new UsoCompartilhado(_relogio, _log);
        _sessao = new Sessao(_simulador, _cofre, _log, Tempos, _chaves, _historico, _uso);
        _leitura = new LeituraAutomatica(_sessao, new LeitorCompleto(_sessao, _log, LeituraRapida));
        _estado = new EstadoAparelho(_sessao, _leitura);
        _escritor = new Escritor(_sessao, _estado, _log, relogio: _relogio);
        _copia = new CopiaAntes(_sessao, _estado, _escritor, _leitura, new TextoMemoria(), _log, _relogio);
        _link = new LinkCanais(_sessao, _estado, _escritor, new TextoMemoria(), _log);
        _presets = new GerentePresets(_sessao, _estado, _escritor, _leitura, _copia, new TextoMemoria(GerentePresets.ParaJson([plano, mil])),
            _log, _relogio);
        _playlists = new GerentePlaylists(new TextoMemoria(), _log);
        _setups = new GerenteSetups(_sessao, _estado, _escritor, _leitura, _copia, _link, _presets, _playlists,
            _chaves, new TextoMemoria(GerenteSetups.ParaJson(dados)), new TextoMemoria(), _log, _relogio);
        _analise = NovaAnalise();
        _player = new Player.Player(_playlists, _setups, _reprodutor, _log) { EmAnalise = id => _analise.EmAnalise(id) };
    }

    /// <summary>Critério de aceite: a análise não muda o som; nada além do catálogo chega ao simulador; nenhum 240, 241 ou 23N.</summary>
    public void Dispose()
    {
        _player.Dispose();
        _analise.Dispose();
        Assert.DoesNotContain(_simulador.Escritas, e => e.StartsWith("*24", StringComparison.Ordinal) || e.StartsWith("*23", StringComparison.Ordinal));
        Assert.All(_simulador.Escritas, e => Assert.True(Senha.IsMatch(e) || Catalogo.EhLeitura(e) || Catalogo.EscritaDeAudio(e) is not null, e));
        Assert.DoesNotContain(_simulador.Escritas, e => e.StartsWith("*080", StringComparison.Ordinal));
    }

    // ----------------------------------------------------------------- apoio

    private AnalisePlaylist NovaAnalise() => new(_setups, _presets, _playlists, _decodificador, _arquivoAnalises, _log);

    private async Task Conectar()
    {
        await _chaves.Carregada;
        await _copia.Carregada;
        await _link.Carregada;
        await _presets.Carregada;
        await _playlists.Carregada;
        await _setups.Carregada;
        await _analise.Carregada;
        await _sessao.ConectarAsync();
        Assert.Equal(EstadoSessao.Conectado, _sessao.Estado);
        await AndarAte(() => _estado.Pronto && !_estado.Lendo && !_leitura.Lendo && !_copia.EmAndamento && _setups.Conferido && !_setups.Ocupado);
        Assert.Equal(CasaDoDenis.IdSetupCasa, _setups.SetupEmUso?.Id);
        Assert.Equal(IdPlana, _setups.MemoriaEmUso?.Id);
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

    private async Task Rodar(Task tarefa)
    {
        await AndarAte(() => tarefa.IsCompleted);
        await tarefa;
    }

    /// <summary>A análise parou (nenhuma faixa pendente e a fila parada).</summary>
    private Task Pronta() => AndarAte(() => !_analise.Ocupada && !_analise.Analisando(IdPlana));

    /// <summary>Senos de 1 kHz com os picos (dBFS), cada um numa faixa "menos N" com a URI dada.</summary>
    private async Task Adicionar(params (string Uri, double Pico)[] faixas)
    {
        foreach (var (uri, pico) in faixas)
            _decodificador.Seno(uri, pico);
        var r = await _playlists.AdicionarAsync(IdPlana,
            faixas.Select(f => new ArquivoAudio(f.Uri, Nome(f.Pico), "Testes", TimeSpan.FromSeconds(2))));
        Assert.True(r.Ok, r.Mensagem);
    }

    private static string Nome(double pico) => $"menos {Math.Abs(pico):0}";

    private Faixa Faixa(string uri) => _playlists.Playlist(IdPlana).Single(f => f.Uri == uri);

    private async Task Otimizar()
    {
        var r = await Rodar(_analise.OtimizarAsync(IdPlana));
        Assert.True(r.Ok, r.Motivo);
    }

    /// <summary>O teto de min(X, C) da JBL L (canal 0) na cadeia do aparelho agora (a PLANA), em dB do limitador.</summary>
    private decimal TetoJbl() =>
        LimiteFisico.Calcular(_setups.SetupEmUso!.Equipamentos, _setups.SaidasEmUso, _estado.Valor, semTermico: true)
            .SelectMany(l => l.PorCanal).Single(c => c.Canal == 0).TetoDb;

    /// <summary>O master esperado para uma faixa de seno com o pico (dBFS) mandando: teto − 1 − (pico + Bluetooth + ganho).</summary>
    private double Esperado(double pico) => (double)TetoJbl() - 1 - (pico + (double)Bluetooth + (double)GanhoJbl);

    private static Func<Endereco, Valor?> Leitor(IEnumerable<ValorCopiado> valores, params (string Codigo, int? Indice, string NoFio)[] mudancas)
    {
        var d = new Dictionary<Endereco, Valor>();
        foreach (var v in valores)
            d[new Endereco(v.Codigo, v.Indice)] = Catalogo.ParametroDe(v.Codigo)!.Decodificar(v.NoFio).Valor!;
        foreach (var (codigo, indice, noFio) in mudancas)
            d[new Endereco(codigo, indice)] = Catalogo.ParametroDe(codigo)!.Decodificar(noFio).Valor!;
        return e => d.GetValueOrDefault(e);
    }

    private FaixaExtra Linha(TetoExtra teto, string uri) => teto.Faixas.Single(f => f.Faixa.Uri == uri);

    // ----------------------------------------------------------------- a matriz

    [Fact]
    public async Task Tres_faixas_sinteticas_o_teto_fica_na_mais_pesada_menos_1_dB_e_ela_pesa_3_dB()
    {
        await Conectar();
        await Adicionar(("seno-3", -3), ("seno-6", -6), ("seno-10", -10));

        await Otimizar();
        await Pronta();

        var teto = _analise.Teto(IdPlana, IdPlano);
        Assert.Equal(EstadoExtra.Pronta, teto.Estado);
        var master = (double)teto.Master!.Value;
        Assert.InRange(master - Esperado(-3), -0.2, 0.2);
        // A matriz diz ≈ −4,9 dB pelo curso do cone de 22,2 V; na fixture ele dá 22,9 V (−4,7 dB): a conta acima é a que vale.
        Assert.InRange(master, -5.1, -4.65);
        Assert.Equal(3.0m, Linha(teto, "seno-3").Peso);
        Assert.Equal(0m, Linha(teto, "seno-6").Peso);
        Assert.Equal(0m, Linha(teto, "seno-10").Peso);
        Assert.Equal("−3,0 dB no teto", Linha(teto, "seno-3").Texto);
        Assert.Null(Linha(teto, "seno-6").Texto);
        Assert.Equal($"Teto do Extra: {LimiteFisico.Db(teto.Master.Value)} dB. Falta o teste a seco.", teto.Resultado);
        Assert.Equal("Falta o teste a seco.", teto.TextoNivel);
        Assert.Null(teto.Aviso);
        // O teto em uso é o do preset em uso, e a memória fica otimizada no setups.json (sem subir o formato).
        Assert.Equal(teto.Master, _analise.TetoEmUso().Master);
        Assert.True(_setups.MemoriaEmUso!.Otimizada);
        var json = GerenteSetups.ParaJson(_setups.Dados);
        Assert.Contains("\"otimizada\": true", json);
        Assert.Contains("\"formato\": 4", json);
        Assert.True(GerenteSetups.DeJson(json)!.Memorias.Single().Otimizada);
        Assert.Equal(3, _decodificador.Lidas.Count);
    }

    [Fact]
    public async Task Os_trechos_fortes_contem_as_rajadas_tem_10_s_e_nao_se_sobrepoem()
    {
        _decodificador.Rajadas("rajadas", 60, [12, 40]);
        var (medidor, falta) = MedidorFaixa.Montar(Leitor(PiorCasoTestes.Plana(GanhoJbl)), Bluetooth);
        Assert.Null(falta);

        await _decodificador.LerAsync("rajadas", medidor!.Processar, CancellationToken.None);
        var analise = medidor.Resultado();

        Assert.Equal(60, analise.Duracao, 3);
        Assert.Equal(2, analise.Trechos.Count);
        Assert.All(analise.Trechos, t => Assert.Equal(10, t.Fim - t.Inicio, 6));
        Assert.Contains(analise.Trechos, t => t.Inicio <= 12 && t.Fim >= 12.05);
        Assert.Contains(analise.Trechos, t => t.Inicio <= 40 && t.Fim >= 40.05);
        Assert.True(analise.Trechos[0].Fim <= analise.Trechos[1].Inicio);

        // Uma faixa abaixo de 10 s: um trecho com a faixa toda.
        _decodificador.Rajadas("curta", 6, [3]);
        var (curto, _) = MedidorFaixa.Montar(Leitor(PiorCasoTestes.Plana(GanhoJbl)), Bluetooth);
        await _decodificador.LerAsync("curta", curto!.Processar, CancellationToken.None);
        var trecho = Assert.Single(curto.Resultado().Trechos);
        Assert.Equal(new Trecho(0, 6), trecho);
    }

    [Fact]
    public async Task Musica_nova_fica_em_analise_o_Player_pula_ela_e_ao_fim_o_teto_desce_e_o_peso_aponta_ela()
    {
        await Conectar();
        await Adicionar(("seno-3", -3), ("seno-6", -6), ("seno-10", -10));
        await Otimizar();
        await Pronta();
        var antes = _analise.Teto(IdPlana, IdPlano).Master!.Value;

        _decodificador.Segurar("seno-1");
        await Adicionar(("seno-1", -1));
        var nova = Faixa("seno-1");

        Assert.True(_analise.EmAnalise(nova.Id));
        Assert.False(_analise.EmAnalise(Faixa("seno-3").Id));
        var emCurso = _analise.Teto(IdPlana, IdPlano);
        Assert.Equal(EstadoExtra.Analisando, emCurso.Estado);
        Assert.Equal("Em análise", Linha(emCurso, "seno-1").Texto);
        Assert.Equal("Analisando a playlist.", emCurso.TextoNivel);
        Assert.Equal(antes, emCurso.Master);

        // O Player toca a terceira; no fim dela, pula a nova (em análise) e para, sem pedir a nova ao reprodutor.
        await Rodar(_player.TocarFaixaAsync(Faixa("seno-10").Id));
        Assert.Equal("seno-10", _reprodutor.Tocando);
        _reprodutor.Terminar();
        await AndarAte(() => _player.Estado == EstadoPlayer.Parado);
        Assert.DoesNotContain("seno-1", _reprodutor.Pedidas);

        _decodificador.Soltar("seno-1");
        await Pronta();

        var depois = _analise.Teto(IdPlana, IdPlano);
        Assert.Equal(EstadoExtra.Pronta, depois.Estado);
        Assert.InRange((double)depois.Master!.Value - Esperado(-1), -0.2, 0.2);
        Assert.True(depois.Master < antes);
        Assert.Equal(antes - depois.Master.Value, Linha(depois, "seno-1").Peso);
        Assert.InRange(Linha(depois, "seno-1").Peso, 1.9m, 2.1m);
        Assert.Equal(0m, Linha(depois, "seno-3").Peso);
        Assert.False(_analise.EmAnalise(nova.Id));
    }

    [Fact]
    public async Task Tocar_direto_uma_faixa_em_analise_avisa_e_nao_toca()
    {
        await Conectar();
        await Adicionar(("seno-3", -3));
        await Otimizar();
        await Pronta();
        _decodificador.Segurar("seno-1");
        await Adicionar(("seno-1", -1));

        await Rodar(_player.TocarFaixaAsync(Faixa("seno-1").Id));

        Assert.Equal("Esta faixa ainda está em análise.", _player.Aviso);
        Assert.Null(_reprodutor.Tocando);
        Assert.DoesNotContain("seno-1", _reprodutor.Pedidas);
        Assert.Equal(EstadoPlayer.Parado, _player.Estado);
        _decodificador.Soltar("seno-1");
        await Pronta();
    }

    [Fact]
    public async Task Arquivo_sumido_fica_de_fora_com_o_motivo_e_o_teto_vem_das_outras()
    {
        await Conectar();
        _decodificador.Ausente("sumida");
        await Adicionar(("seno-6", -6), ("seno-10", -10));
        Assert.True((await _playlists.AdicionarAsync(IdPlana, [new ArquivoAudio("sumida", "sumida", "Testes", TimeSpan.FromSeconds(3))])).Ok);

        await Otimizar();
        await Pronta();

        var teto = _analise.Teto(IdPlana, IdPlano);
        Assert.Equal(EstadoExtra.Pronta, teto.Estado);
        var sumida = Linha(teto, "sumida");
        Assert.Equal(SituacaoFaixa.Falhou, sumida.Situacao);
        Assert.Equal("Não deu para analisar: o arquivo não foi encontrado", sumida.Texto);
        Assert.InRange((double)teto.Master!.Value - Esperado(-6), -0.2, 0.2);
        Assert.False(_analise.EmAnalise(Faixa("sumida").Id));
    }

    [Fact]
    public async Task Outro_preset_com_6_dB_em_1_kHz_tem_o_teto_6_dB_abaixo()
    {
        await Conectar();
        await Adicionar(("seno-3", -3), ("seno-6", -6));
        await Otimizar();
        await Pronta();

        var plano = _analise.Teto(IdPlana, IdPlano).Master!.Value;
        var mil = _analise.Teto(IdPlana, IdMil).Master!.Value;

        Assert.InRange(plano - 6m - mil, -0.2m, 0.2m);
        // O áudio foi lido uma vez para os dois presets.
        Assert.Equal(2, _decodificador.Lidas.Count);
    }

    [Fact]
    public async Task Sem_calibracao_o_Otimizar_da_o_motivo_e_nada_e_analisado()
    {
        using var t = new AnalisePlaylistTestes(CasaDoDenis.SetupCasaComBluetooth with { Equipamentos = CasaDoDenis.EquipamentosSemCalibracao });
        await t.Conectar();
        await t.Adicionar(("seno-3", -3));

        var r = await t.Rodar(t._analise.OtimizarAsync(IdPlana));

        Assert.False(r.Ok);
        Assert.StartsWith("Calibre a saída ", r.Motivo);
        await t.AndarAte(() => !t._analise.Ocupada);
        Assert.Empty(t._decodificador.Lidas);
        Assert.False(t._setups.MemoriaEmUso!.Otimizada);
        Assert.Equal(EstadoExtra.Falta, t._analise.Teto(IdPlana, IdPlano).Estado);
        Assert.Equal("Falta analisar a playlist.", t._analise.TetoEmUso().TextoNivel);
    }

    [Fact]
    public async Task Reabrir_o_app_sem_mudanca_nao_decodifica_de_novo_e_a_ficha_mudada_analisa_de_novo()
    {
        await Conectar();
        await Adicionar(("seno-3", -3), ("seno-6", -6));
        await Otimizar();
        await Pronta();
        var master = _analise.Teto(IdPlana, IdPlano).Master;
        Assert.Equal(2, _decodificador.Lidas.Count);
        Assert.Contains("\"impressao\"", _arquivoAnalises.Texto);

        // Fechar e reabrir o app: as análises voltam do analises.json.
        _analise.Dispose();
        _analise = NovaAnalise();
        await _analise.Carregada;
        await Pronta();
        Assert.Equal(2, _decodificador.Lidas.Count);
        Assert.Equal(EstadoExtra.Pronta, _analise.Teto(IdPlana, IdPlano).Estado);
        Assert.Equal(master, _analise.Teto(IdPlana, IdPlano).Master);

        // A ficha da JBL L muda: a impressão muda e a playlist é analisada de novo.
        var jbl = _setups.SetupEmUso!.Equipamentos.Single(e => e.Id == CasaDoDenis.IdJblL);
        var editada = jbl with { Dados = jbl.Dados with { PotenciaRms = new DadoTecnico(81m, OrigemDado.Oficial, "teste") } };
        Assert.True((await Rodar(_setups.EditarEquipamentoAsync(CasaDoDenis.IdSetupCasa, editada))).Ok);
        await AndarAte(() => _decodificador.Lidas.Count >= 4);
        await Pronta();
        Assert.Equal(4, _decodificador.Lidas.Count);
        Assert.Equal(EstadoExtra.Pronta, _analise.Teto(IdPlana, IdPlano).Estado);
    }

    [Fact]
    public async Task Apagar_a_faixa_ou_desotimizar_cancela_a_analise_em_curso_e_nada_e_gravado()
    {
        await Conectar();
        _decodificador.Segurar("seno-3");
        await Adicionar(("seno-3", -3));
        await Otimizar();
        await AndarAte(() => _decodificador.Lidas.Count == 1);

        Assert.True((await _playlists.RemoverAsync(IdPlana, Faixa("seno-3").Id)).Ok);
        await AndarAte(() => !_analise.Ocupada);
        Assert.Null(_arquivoAnalises.Texto);

        _decodificador.Segurar("seno-6");
        await Adicionar(("seno-6", -6));
        await AndarAte(() => _decodificador.Lidas.Count == 2);
        Assert.True((await Rodar(_analise.DesotimizarAsync(IdPlana))).Ok);
        await AndarAte(() => !_analise.Ocupada);
        Assert.Null(_arquivoAnalises.Texto);
        Assert.Equal(EstadoExtra.Falta, _analise.Teto(IdPlana, IdPlano).Estado);
        Assert.False(_analise.EmAnalise(Faixa("seno-6").Id));
    }

    [Fact]
    public async Task Rota_a_mais_b_com_L_e_R_iguais_soma_6_dB_no_pico_da_saida()
    {
        _decodificador.Seno("igual-6", -6, segundos: 10);
        var (medidor, _) = MedidorFaixa.Montar(Leitor(PiorCasoTestes.Plana(GanhoJbl), ("010", 1, "a+b")), Bluetooth);

        await _decodificador.LerAsync("igual-6", medidor!.Processar, CancellationToken.None);
        var analise = medidor.Resultado();

        // O canal 1 soma L e R: −6 + 6 dB antes do resto (o Bluetooth e o ganho; o LR24 em 200 Hz quase nada em 1 kHz).
        Assert.Equal(6.0, analise.Pico(1)!.Value - analise.Pico(0)!.Value, 1);
        Assert.InRange(analise.Pico(1)!.Value - (-6 + 6 + (double)Bluetooth + (double)GanhoJbl), -0.1, 0.0);
        // A RMS verdadeira de um seno fica 3,01 dB abaixo do pico (a subida e a descida de 0,1 s a puxam um pouco mais).
        Assert.InRange(analise.Canais[0].Rms - (analise.Pico(0)!.Value - 20 * Math.Log10(Math.Sqrt(2))), -0.1, 0.0);
    }

    /// <summary>Uma porta com a análise, conectada, aberta e pareada: a porta (para descartar) e a chave.</summary>
    private async Task<(PortaComandos Porta, string Chave)> PortaPareada()
    {
        var gerador = new GeradorSinal(_sessao, _estado, _leitura, _copia, _log, relogio: _relogio);
        var monitor = new MonitorAparelho(_sessao, _leitura, _relogio);
        var comandos = new ComandosAparelho(_sessao, _estado, _leitura, _copia, _cofre, _log, gerador, relogio: _relogio);
        var pareamento = new Pareamento(new TextoMemoria(), _relogio, _log);
        var porta = new PortaComandos(_sessao, _chaves, _uso, _historico, pareamento, new PedidosConfirmacao(_relogio, _log),
            new RedeFalsa(), _estado, _escritor, _setups, gerador, comandos, monitor, _log, _relogio, 0, analise: _analise);
        await _historico.Carregado;
        await pareamento.Carregado;
        await Conectar();
        Assert.True((await _chaves.DefinirAsync(Chave.ControlePelaRede, true, OrigemComando.Tela)).Ok);
        await porta.AtualizarAsync();
        var parear = await porta.AtenderAsync(PedidoPorta.De("POST", "/parear", Casa,
            $$"""{"codigo":"{{pareamento.Codigo}}","nome":"Claude"}"""));
        return (porta, JsonDocument.Parse(parear.Json).RootElement.GetProperty("chave").GetString()!);
    }

    private static async Task<JsonElement> Extra(PortaComandos porta, string chave) =>
        JsonDocument.Parse((await porta.AtenderAsync(PedidoPorta.De("GET", "/limites", Casa, bearer: chave))).Json).RootElement.GetProperty("extra");

    [Fact]
    public async Task A_porta_traz_o_extra_com_estado_master_pesando_e_trechos()
    {
        var (porta, chave) = await PortaPareada();
        using var _ = porta;

        var antes = JsonDocument.Parse((await porta.AtenderAsync(PedidoPorta.De("GET", "/limites", Casa, bearer: chave))).Json).RootElement;
        Assert.Equal("falta", antes.GetProperty("extra").GetProperty("estado").GetString());
        Assert.Equal(JsonValueKind.Null, antes.GetProperty("extra").GetProperty("master").ValueKind);

        await Adicionar(("seno-3", -3), ("seno-6", -6));
        await Otimizar();
        await Pronta();
        var r = await porta.AtenderAsync(PedidoPorta.De("GET", "/limites", Casa, bearer: chave));

        Assert.Equal(200, r.Status);
        var extra = JsonDocument.Parse(r.Json).RootElement.GetProperty("extra");
        Assert.Equal("pronta", extra.GetProperty("estado").GetString());
        Assert.Equal(_analise.TetoEmUso().Master, extra.GetProperty("master").GetDecimal());
        var pesando = extra.GetProperty("pesando").EnumerateArray().ToArray();
        Assert.Equal(["menos 3", "menos 6"], pesando.Select(p => p.GetProperty("faixa").GetString()));
        Assert.Equal(Faixa("seno-3").Id, pesando[0].GetProperty("id").GetString());
        Assert.Equal(3.0m, pesando[0].GetProperty("db").GetDecimal());
        Assert.Equal(0m, pesando[1].GetProperty("db").GetDecimal());
        var trechos = extra.GetProperty("trechos").EnumerateArray().ToArray();
        Assert.Equal(2, trechos.Length);
        var trecho = Assert.Single(trechos[0].GetProperty("trechos").EnumerateArray());
        Assert.Equal(0, trecho.GetProperty("inicio").GetDouble());
        Assert.Equal(2, trecho.GetProperty("fim").GetDouble());
        var ajuda = await porta.AtenderAsync(PedidoPorta.De("GET", "/ajuda", Casa, bearer: chave));
        Assert.Contains(Textos(JsonDocument.Parse(ajuda.Json).RootElement), t => t.Contains("Em \"extra\", o teto do Extra", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_porta_mostra_a_faixa_em_analise_e_a_que_falhou_fora_do_pesando()
    {
        var (porta, chave) = await PortaPareada();
        using var _ = porta;
        _decodificador.Ausente("sumida");
        _decodificador.Segurar("seno-6");
        await Adicionar(("seno-3", -3));
        Assert.True((await _playlists.AdicionarAsync(IdPlana, [new ArquivoAudio("sumida", "sumida", "Testes", TimeSpan.FromSeconds(3))])).Ok);
        await Adicionar(("seno-6", -6));

        await Otimizar();
        await AndarAte(() => _decodificador.Lidas.Contains("seno-6"));
        var extra = await Extra(porta, chave);

        Assert.Equal("analisando", extra.GetProperty("estado").GetString());
        var trechos = extra.GetProperty("trechos").EnumerateArray().ToDictionary(t => t.GetProperty("faixa").GetString()!);
        Assert.Equal("em-analise", trechos["menos 6"].GetProperty("situacao").GetString());
        Assert.Equal(Faixa("seno-6").Id, trechos["menos 6"].GetProperty("id").GetString());
        Assert.Equal("falhou", trechos["sumida"].GetProperty("situacao").GetString());
        Assert.Equal("o arquivo não foi encontrado", trechos["sumida"].GetProperty("motivo").GetString());
        Assert.Equal(["menos 3"], extra.GetProperty("pesando").EnumerateArray().Select(p => p.GetProperty("faixa").GetString()));

        _decodificador.Soltar("seno-6");
        await Pronta();
    }

    [Fact]
    public async Task Com_todas_as_faixas_em_analise_o_Player_avisa_que_nenhuma_esta_disponivel_e_nao_pede_nada()
    {
        await Conectar();
        await Otimizar();
        _decodificador.Segurar("seno-1");
        await Adicionar(("seno-1", -1), ("seno-2", -2));
        await AndarAte(() => _decodificador.Lidas.Contains("seno-1"));
        Assert.True(_analise.EmAnalise(Faixa("seno-1").Id));
        Assert.True(_analise.EmAnalise(Faixa("seno-2").Id));

        await Rodar(_player.TocarPausarAsync());

        Assert.Equal(Player.Player.MsgNenhumaDisponivel("PLANA"), _player.Aviso);
        Assert.Empty(_reprodutor.Pedidas);
        Assert.Equal(EstadoPlayer.Parado, _player.Estado);
        _decodificador.Soltar("seno-1");
        await Pronta();
    }

    [Fact]
    public async Task Salvar_a_memoria_com_outro_ganho_reanalisa_o_master_acompanha_e_as_faixas_seguem_tocando()
    {
        await Conectar();
        await Adicionar(("seno-3", -3), ("seno-6", -6));
        await Otimizar();
        await Pronta();
        var antes = _analise.Teto(IdPlana, IdPlano).Master!.Value;
        Assert.Equal(2, _decodificador.Lidas.Count);

        // O ganho das JBL desce de +15 para +12 (o 081 vai só até +15) e a memória é salva: a cadeia guardada muda.
        _decodificador.Segurar("seno-3");
        var ganho = Catalogo.ParametroDe("081")!;
        using (Origem.Como(OrigemComando.App))
        {
            Assert.True((await Rodar(_escritor.EscreverAsync(ganho, 0, 12m))).Ok);
            Assert.True((await Rodar(_escritor.EscreverAsync(ganho, 1, 12m))).Ok);
        }
        var salvar = await Rodar(_setups.SalvarAsync(null));
        Assert.True(salvar.Ok, salvar.Motivo);
        await AndarAte(() => _decodificador.Lidas.Count == 3);

        // Na reanálise, as faixas seguem tocando; só o Extra fica "analisando".
        Assert.False(_analise.EmAnalise(Faixa("seno-3").Id));
        Assert.False(_analise.EmAnalise(Faixa("seno-6").Id));
        Assert.Equal(EstadoExtra.Analisando, _analise.Teto(IdPlana, IdPlano).Estado);
        await Rodar(_player.TocarFaixaAsync(Faixa("seno-3").Id));
        Assert.Equal("seno-3", _reprodutor.Tocando);

        _decodificador.Soltar("seno-3");
        await Pronta();
        var depois = _analise.Teto(IdPlana, IdPlano);
        Assert.Equal(EstadoExtra.Pronta, depois.Estado);
        Assert.InRange(depois.Master!.Value - antes, 2.8m, 3.2m);
        Assert.Equal(4, _decodificador.Lidas.Count);
    }

    // ----------------------------------------------------------------- a conta e os trechos, puros

    /// <summary>Os limites do Casa 2.1 no Máximo (min(X, C)) com a PLANA de +15.</summary>
    private static IReadOnlyList<LimiteSaida> LimitesMaximo()
    {
        var equipamentos = CasaDoDenis.SetupCasaComBluetooth.Equipamentos;
        return LimiteFisico.Calcular(equipamentos, Ligacoes.Saidas(equipamentos), Leitor(PiorCasoTestes.Plana(GanhoJbl)), semTermico: true);
    }

    /// <summary>Uma análise com o pico nas JBL (canais 0 e 1) e o Sub em silêncio.</summary>
    private static AnaliseFaixa Analise(double picoJbl) =>
        new([new(0, picoJbl, picoJbl - 3), new(1, picoJbl, picoJbl - 3), new(2, MedidorFaixa.PisoDb, MedidorFaixa.PisoDb),
            new(3, MedidorFaixa.PisoDb, MedidorFaixa.PisoDb)], [], 2);

    private static decimal TetoJblMaximo() => LimitesMaximo().SelectMany(l => l.PorCanal).Single(c => c.Canal == 0).TetoDb;

    [Fact]
    public void Um_pico_muito_baixo_da_master_0_com_o_aviso_de_falta_de_ganho_e_um_muito_alto_fica_em_menos_80()
    {
        var baixo = AnalisePlaylist.Conta(LimitesMaximo(), [("baixa", Analise(-30))]);
        Assert.Equal(0m, baixo.Master);
        Assert.Equal("JBL L", baixo.FaltaGanho);
        Assert.Equal(Niveis.MsgFaltaGanhoNa("JBL L"), baixo.Aviso);

        var alto = AnalisePlaylist.Conta(LimitesMaximo(), [("alta", Analise(80))]);
        Assert.Equal(PiorCaso.MinimoMaster, alto.Master);
        Assert.Null(alto.FaltaGanho);
    }

    [Fact]
    public void Duas_faixas_empatadas_no_minimo_pesam_o_que_o_grupo_tira_do_teto()
    {
        var conta = AnalisePlaylist.Conta(LimitesMaximo(), [("a", Analise(-1)), ("b", Analise(-1)), ("c", Analise(-4))]);

        Assert.Equal(TetoJblMaximo(), conta.Master);
        Assert.Equal(3.0m, conta.Pesos["a"]);
        Assert.Equal(3.0m, conta.Pesos["b"]);
        Assert.Equal(0m, conta.Pesos["c"]);
    }

    [Fact]
    public void Uma_faixa_sem_som_fica_fora_da_conta()
    {
        var silencio = new AnaliseFaixa(Enumerable.Range(0, 4).Select(c => new MedidaCanal(c, MedidorFaixa.PisoDb, MedidorFaixa.PisoDb)).ToArray(), [], 2);

        var conta = AnalisePlaylist.Conta(LimitesMaximo(), [("muda", silencio), ("c", Analise(-1))]);

        Assert.True(silencio.Silenciosa);
        Assert.Null(conta.FaltaGanho);
        Assert.False(conta.Pesos.ContainsKey("muda"));
        Assert.Equal(AnalisePlaylist.Conta(LimitesMaximo(), [("c", Analise(-1))]).Master, conta.Master);
    }

    [Fact]
    public void Os_trechos_saem_em_ordem_de_tempo_sem_sobrepor_e_respeitam_o_comeco_da_faixa()
    {
        // O mais forte em 40 s e um mais fraco em 12 s.
        var picos = Enumerable.Repeat(0.01, 600).ToArray();
        picos[400] = 1.0;
        picos[120] = 0.5;
        var trechos = MedidorFaixa.Trechos(picos, 0.1, 60);
        Assert.Equal(2, trechos.Count);
        Assert.True(trechos[0].Fim <= trechos[1].Inicio);
        Assert.True(trechos[0].Inicio <= 12 && trechos[0].Fim >= 12.1);
        Assert.True(trechos[1].Inicio <= 40 && trechos[1].Fim >= 40.1);

        // O mais forte em 4,5 s (sem lugar à esquerda), o segundo em 2 s (dentro do primeiro) e o terceiro em 30 s.
        picos = Enumerable.Repeat(0.01, 600).ToArray();
        picos[45] = 1.0;
        picos[20] = 0.9;
        picos[300] = 0.8;
        trechos = MedidorFaixa.Trechos(picos, 0.1, 60);
        Assert.Equal(new Trecho(0, 10), trechos[0]);
        Assert.True(trechos[1].Inicio <= 30 && trechos[1].Fim >= 30.1);
        Assert.True(trechos[0].Fim <= trechos[1].Inicio);
        Assert.All(trechos, t => Assert.True(t.Fim <= 60));
    }

    [Fact]
    public async Task Dois_tons_somados_dao_o_pico_da_forma_de_onda_e_nao_o_de_cada_tom()
    {
        // 100 Hz e 1 kHz, cada um com −8 dBFS de pico, numa cadeia plana (Bluetooth 0, ganho 0, sem cortes nas JBL).
        _decodificador.Tons("dois", [(100, -8), (1000, -8)]);
        var maximo = 0.0;
        await _decodificador.LerAsync("dois", (bloco, canais, _) =>
        {
            for (var i = 0; i < bloco.Length; i += canais)
                maximo = Math.Max(maximo, Math.Abs(bloco[i]));
        }, CancellationToken.None);
        var (medidor, falta) = MedidorFaixa.Montar(Leitor(PiorCasoTestes.Plana(0m), ("042", 0, "0"), ("042", 1, "0")), 0m);
        Assert.Null(falta);

        await _decodificador.LerAsync("dois", medidor!.Processar, CancellationToken.None);
        var pico = medidor.Resultado().Pico(0)!.Value;

        Assert.InRange(pico - 20 * Math.Log10(maximo), -0.05, 0.05);
        Assert.True(pico > -8 + 5, $"o pico {pico:0.00} dB é o de um tom só");
    }

    [Fact]
    public void Uma_rota_desconhecida_e_dado_que_falta()
    {
        var ler = Leitor(PiorCasoTestes.Plana(GanhoJbl));
        var rota = new Endereco("010", 1);

        var (medidor, falta) = MedidorFaixa.Montar(e => e == rota ? new Valor { NoFio = "x", Exibicao = "x", Texto = "x" } : ler(e), Bluetooth);

        Assert.Null(medidor);
        Assert.Equal(PiorCaso.MsgFalta("010", 1), falta);
    }

    [Fact]
    public void Uma_amostra_que_nao_e_numero_recusa_a_faixa()
    {
        var (medidor, _) = MedidorFaixa.Montar(Leitor(PiorCasoTestes.Plana(GanhoJbl)), Bluetooth);

        var erro = Assert.Throws<ErroDecodificacao>(() => medidor!.Processar([0.1f, float.NaN], 2, 48000));

        Assert.Equal(ErroDecodificacao.MsgFormato, erro.Motivo);
    }

    [Fact]
    public void Uma_analise_guardada_sem_os_4_canais_ou_com_canal_repetido_sai()
    {
        static string Canais(params int[] canais) => string.Join(",", canais.Select(c => $$"""{"canal":{{c}},"pico":-1,"rms":-4}"""));
        var json = $$"""
            {"formato":1,"analises":[
              {"uri":"boa","duracao":2000,"impressao":"X","segundos":2,"canais":[{{Canais(0, 1, 2, 3)}}],"trechos":[[0,2]]},
              {"uri":"tres","duracao":2000,"impressao":"X","segundos":2,"canais":[{{Canais(0, 1, 2)}}],"trechos":[]},
              {"uri":"repetido","duracao":2000,"impressao":"X","segundos":2,"canais":[{{Canais(0, 1, 1, 3)}}],"trechos":[]},
              {"uri":"fora","duracao":2000,"impressao":"X","segundos":2,"canais":[{{Canais(0, 1, 2, 7)}}],"trechos":[]}
            ]}
            """;

        var lidas = AnalisePlaylist.LerJson(json)!;

        Assert.Equal(["boa"], lidas.Select(l => l.Uri));
    }

    /// <summary>Todos os textos de um JSON.</summary>
    private static IEnumerable<string> Textos(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.String => [e.GetString()!],
        JsonValueKind.Object => e.EnumerateObject().SelectMany(p => Textos(p.Value)),
        JsonValueKind.Array => e.EnumerateArray().SelectMany(Textos),
        _ => [],
    };

    // ----------------------------------------------------------------- o reprodutor

    /// <summary>O reprodutor do teste: toca na hora, guarda as URIs pedidas e termina a faixa quando o teste manda.</summary>
    private sealed class ReprodutorTeste : IReprodutor
    {
        private readonly object _trava = new();
        private readonly List<string> _pedidas = [];
        private string? _tocando;

        public string? Tocando
        {
            get
            {
                lock (_trava)
                    return _tocando;
            }
        }

        public IReadOnlyList<string> Pedidas
        {
            get
            {
                lock (_trava)
                    return _pedidas.ToArray();
            }
        }

        public event Action<string>? Terminou;

        public event Action<string>? Pausou;

        public event Action<string>? Falhou;

        public Task<bool> TocarAsync(string uri)
        {
            lock (_trava)
            {
                _pedidas.Add(uri);
                _tocando = uri;
            }
            return Task.FromResult(true);
        }

        public Task PausarAsync() => Task.CompletedTask;

        public Task<bool> ContinuarAsync() => Task.FromResult(true);

        public Task PararAsync()
        {
            lock (_trava)
                _tocando = null;
            return Task.CompletedTask;
        }

        public void Terminar()
        {
            if (Tocando is { } uri)
                Terminou?.Invoke(uri);
        }

        /// <summary>Nunca usados aqui (só para a interface).</summary>
        public void Nada()
        {
            Pausou?.Invoke("");
            Falhou?.Invoke("");
        }
    }
}
