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
/// O reprodutor da história 13e nos testes: toca a faixa do Player na hora e, no trecho do teste a seco, manda ao
/// simulador o pico de cada saída (o da análise da 13d, mais o que o teste puser a mais) pelo <see cref="ProcessadorSimulado.Sinal"/>.
/// Registra os trechos e pode segurar um trecho até o teste soltar (ou o reprodutor parar).
/// </summary>
internal sealed class ReprodutorSeco(ProcessadorSimulado simulador) : IReprodutor
{
    private readonly object _trava = new();
    private readonly List<(string Uri, TimeSpan Inicio, TimeSpan Duracao, int Escritas)> _trechos = [];
    private readonly Dictionary<string, (int Canal, double Db)[]> _picos = new(StringComparer.Ordinal);
    private TaskCompletionSource<bool>? _segurado;
    private string? _tocando;

    /// <summary>O pico de cada saída da faixa (dB do limitador, sem o master).</summary>
    public void Picos(string uri, params (int Canal, double Db)[] picos)
    {
        lock (_trava)
            _picos[uri] = picos;
    }

    /// <summary>O que vai a mais no Sinal do trecho n (1 = o primeiro tocado), por canal: (n, canal) → dB.</summary>
    public Func<int, int, double> AMais { get; set; } = (_, _) => 0;

    /// <summary>Chamado no começo do trecho n (1 = o primeiro tocado), antes do Sinal (para derrubar a conexão, por exemplo).</summary>
    public Action<int>? AoTocar { get; set; }

    /// <summary>O trecho n (1 = o primeiro) fica tocando até <see cref="Soltar"/> ou o <see cref="PararAsync"/>.</summary>
    public int? Segurar { get; set; }

    /// <summary>Os trechos tocados, na ordem, com quantas escritas o simulador já tinha recebido.</summary>
    public IReadOnlyList<(string Uri, TimeSpan Inicio, TimeSpan Duracao, int Escritas)> Trechos
    {
        get
        {
            lock (_trava)
                return _trechos.ToArray();
        }
    }

    public string? Tocando
    {
        get
        {
            lock (_trava)
                return _tocando;
        }
    }

    public bool Segurando
    {
        get
        {
            lock (_trava)
                return _segurado is not null;
        }
    }

    public TimeSpan? Posicao { get; set; }

    public event Action<string>? Terminou;

    public event Action<string>? Pausou;

    public event Action<string>? Falhou;

    public Task<bool> TocarAsync(string uri)
    {
        lock (_trava)
            _tocando = uri;
        return Task.FromResult(true);
    }

    public Task PausarAsync() => Task.CompletedTask;

    public Task<bool> ContinuarAsync() => Task.FromResult(true);

    public Task PararAsync()
    {
        TaskCompletionSource<bool>? segurado;
        lock (_trava)
        {
            _tocando = null;
            segurado = _segurado;
            _segurado = null;
        }
        segurado?.TrySetResult(false);
        return Task.CompletedTask;
    }

    public void Soltar()
    {
        TaskCompletionSource<bool>? segurado;
        lock (_trava)
        {
            segurado = _segurado;
            _segurado = null;
        }
        segurado?.TrySetResult(true);
    }

    public async Task<bool> TocarTrechoAsync(string uri, TimeSpan inicio, TimeSpan duracao, CancellationToken cancelar = default)
    {
        int n;
        (int Canal, double Db)[] picos;
        TaskCompletionSource<bool>? segurado = null;
        lock (_trava)
        {
            _trechos.Add((uri, inicio, duracao, simulador.Escritas.Count));
            n = _trechos.Count;
            _tocando = uri;
            picos = _picos.GetValueOrDefault(uri) ?? [];
            if (Segurar == n)
                segurado = _segurado = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        AoTocar?.Invoke(n);
        foreach (var (canal, db) in picos)
            simulador.Sinal(canal, db + AMais(n, canal));
        if (segurado is null)
            return !cancelar.IsCancellationRequested;
        using (cancelar.Register(() => segurado.TrySetResult(false)))
            return await segurado.Task;
    }

    /// <summary>Só para o compilador: os eventos existem na interface.</summary>
    public void Nada()
    {
        Terminou?.Invoke("");
        Pausou?.Invoke("");
        Falhou?.Invoke("");
    }
}

/// <summary>Outro app do celular nos testes: o teste dispara o "começou".</summary>
internal sealed class OutroPlayerFalso : IOutroPlayer
{
    public event Action? Comecou;

    public void Disparar() => Comecou?.Invoke();
}

/// <summary>O volume de mídia do celular nos testes (0 a 15).</summary>
internal sealed class VolumeFalso : IVolumeCelular
{
    private int _volume = 9;

    public int Volume => Volatile.Read(ref _volume);

    public int Maximo => 15;

    public int Definicoes { get; private set; }

    public event Action? Mudou;

    /// <summary>O teto que o Android deixa (o volume seguro): acima dele, o volume para nele.</summary>
    public int Limite { get; set; } = 15;

    public Task<bool> DefinirAsync(int volume)
    {
        Definicoes++;
        Volatile.Write(ref _volume, Math.Min(volume, Limite));
        Mudou?.Invoke();
        return Task.FromResult(Volume >= volume);
    }

    /// <summary>O usuário (ou outro app) mexe no volume.</summary>
    public void Mudar(int volume)
    {
        Volatile.Write(ref _volume, volume);
        Mudou?.Invoke();
    }
}

/// <summary>
/// A bancada da história 13e contra o processador simulado: o Casa 2.1 calibrado (Bluetooth −13 dB; da 13f, com os dados
/// térmicos de teste, <see cref="CasaDoDenis.SetupCasaComTermico"/>, ou outro Setup), a memória PLANA em
/// uso no Máximo (o ganho +15 nas JBL, LR24 em 200 Hz, os limitadores das JBL em −5,0 dB, abaixo de min(X, C)),
/// lembrando o preset Plano; a análise da playlist com o dublê do decodificador, o Player, o teste a seco e o fiscal.
/// As faixas são senos de 1 kHz de 2 s (um trecho cada): o pico nas JBL é o dBFS − 13 + 15.
/// </summary>
internal sealed class BancadaExtra : IDisposable
{
    private static readonly TemposSessao Tempos = new(
        Scan: TimeSpan.FromSeconds(1),
        TentativaConexao: TimeSpan.FromSeconds(1),
        PausaEntreTentativas: TimeSpan.FromMilliseconds(1),
        RespostaSenha: TimeSpan.FromSeconds(2),
        PrazoSenha: TimeSpan.FromSeconds(60));

    private static readonly TemposLeitura LeituraRapida = new(TimeSpan.Zero, TimeSpan.FromMilliseconds(100));
    private static readonly Regex Senha = new(@"^\*000\*[0-9A-Z]{4}\*$");
    public static readonly IPAddress Casa = IPAddress.Parse("192.168.0.10");

    public const string IdPlana = "memoria-plana";
    public const string IdPlano = "preset-plano";
    public const string IdMil = "preset-mil";
    public const decimal Bluetooth = -13m;
    public const decimal GanhoJbl = 15m;

    /// <summary>O limiar dos limitadores das JBL (−5,0 dB): o pico no teto candidato (teto − 1) passa sem clip.</summary>
    public const string LimitadorJbl = "-5.0";

    public RelogioFalso Relogio { get; } = new();
    public ProcessadorSimulado Simulador { get; }
    public CofreFalso Cofre { get; } = new() { Senha = "AB12" };
    public Chaves Chaves { get; }
    public LogBle Log { get; } = new();
    public Historico Historico { get; }
    public UsoCompartilhado Uso { get; }
    public Sessao Sessao { get; }
    public LeituraAutomatica Leitura { get; }
    public EstadoAparelho Estado { get; }
    public Escritor Escritor { get; }
    public CopiaAntes Copia { get; }
    public LinkCanais Link { get; }
    public GerentePresets Presets { get; }
    public GerentePlaylists Playlists { get; }
    public GerenteSetups Setups { get; }
    public DecodificadorSintetico Decodificador { get; } = new();
    public AnalisePlaylist Analise { get; }
    public ReprodutorSeco Reprodutor { get; }
    public Player.Player Player { get; }
    public GeradorSinal Gerador { get; }
    public MonitorAparelho Monitor { get; }
    public ComandosAparelho Comandos { get; }
    public TesteSeco Teste { get; }
    public OutroPlayerFalso Outro { get; } = new();
    public VolumeFalso Volume { get; } = new();
    public FiscalExtra Fiscal { get; }

    /// <summary>O mudo do fiscal guardado no celular (o <c>fiscal-extra.json</c>).</summary>
    public TextoMemoria ArquivoFiscal { get; } = new();

    /// <summary>O modelo térmico guardado no celular (o <c>termico.json</c>, história 13f).</summary>
    public TextoMemoria ArquivoTermico { get; } = new();

    /// <summary>Os modelos térmicos criados por <see cref="NovoTermico"/> (o último é o do app aberto).</summary>
    private readonly List<TermicoExtra> _termicos = [];

    public BancadaExtra() : this(CasaDoDenis.SetupCasaComTermico)
    {
    }

    public BancadaExtra(Setup setup)
    {
        var regulagem = PiorCasoTestes.Plana(GanhoJbl)
            .Where(v => Camadas.De(v.Codigo) == Camada.Regulagem)
            .Select(v => v.Codigo == Cerca.CodigoLimitador && v.Indice is 0 or 1 ? v with { NoFio = LimitadorJbl } : v)
            .ToArray();
        var plano = new Preset(IdPlano, "Plano", CasaDoDenis.QuandoFilme, GerentePresets.BandasPlanas);
        // A banda 8 é a de 1 kHz.
        var mil = new Preset(IdMil, "Mil", CasaDoDenis.QuandoFilme,
            GerentePresets.BandasPlanas.Select(v => v.Indice == 8 ? v with { NoFio = "+6.0" } : v).ToArray());
        var memoria = new MemoriaCelular(IdPlana, CasaDoDenis.IdSetupCasa, "PLANA", CasaDoDenis.QuandoFilme, regulagem, IdPlano)
        {
            Nivel = NivelRendimento.Maximo,
        };
        var dados = new DadosSetups([setup], [memoria], CasaDoDenis.IdSetupCasa,
            new Dictionary<string, UsoDoSetup> { [CasaDoDenis.IdSetupCasa] = new([IdPlana], [IdPlana, null, null]) });

        Simulador = new ProcessadorSimulado(Relogio) { AtrasoSenha = TimeSpan.Zero, AtrasoLeitura = TimeSpan.Zero };
        foreach (var v in regulagem.Concat(plano.Valores))
            Simulador.Definir(v.Codigo, v.Indice, v.NoFio);
        Chaves = new Chaves(new RepositorioMemoria());
        Historico = new Historico(new TextoMemoria(), Relogio, Log);
        Uso = new UsoCompartilhado(Relogio, Log);
        Sessao = new Sessao(Simulador, Cofre, Log, Tempos, Chaves, Historico, Uso);
        Leitura = new LeituraAutomatica(Sessao, new LeitorCompleto(Sessao, Log, LeituraRapida));
        Estado = new EstadoAparelho(Sessao, Leitura);
        Escritor = new Escritor(Sessao, Estado, Log, relogio: Relogio);
        Copia = new CopiaAntes(Sessao, Estado, Escritor, Leitura, new TextoMemoria(), Log, Relogio);
        Link = new LinkCanais(Sessao, Estado, Escritor, new TextoMemoria(), Log);
        Presets = new GerentePresets(Sessao, Estado, Escritor, Leitura, Copia, new TextoMemoria(GerentePresets.ParaJson([plano, mil])), Log, Relogio);
        Playlists = new GerentePlaylists(new TextoMemoria(), Log);
        Setups = new GerenteSetups(Sessao, Estado, Escritor, Leitura, Copia, Link, Presets, Playlists, Chaves,
            new TextoMemoria(GerenteSetups.ParaJson(dados)), new TextoMemoria(), Log, Relogio);
        Analise = new AnalisePlaylist(Setups, Presets, Playlists, Decodificador, new TextoMemoria(), Log);
        Reprodutor = new ReprodutorSeco(Simulador);
        Player = new Player.Player(Playlists, Setups, Reprodutor, Log) { EmAnalise = Analise.EmAnalise };
        Gerador = new GeradorSinal(Sessao, Estado, Leitura, Copia, Log, relogio: Relogio);
        Monitor = new MonitorAparelho(Sessao, Leitura, Relogio);
        Comandos = new ComandosAparelho(Sessao, Estado, Leitura, Copia, Cofre, Log, Gerador, relogio: Relogio);
        Teste = new TesteSeco(Sessao, Estado, Escritor, Copia, Setups, Analise, Comandos, Monitor, Reprodutor, Chaves, Log, Player, Gerador,
            Relogio, TimeSpan.FromMilliseconds(200), Volume);
        Fiscal = NovoFiscal();
    }

    /// <summary>
    /// Critério de aceite: só a senha, as leituras, o áudio do catálogo e o 252; nenhum 240, 241 ou 23N; e o 252 = 1 só
    /// com o master no aparelho igual ao que ficou validado.
    /// </summary>
    public void Dispose()
    {
        foreach (var t in _termicos)
            t.Dispose();
        Fiscal.Dispose();
        Teste.Dispose();
        Player.Dispose();
        Analise.Dispose();
        Assert.DoesNotContain(Simulador.Escritas, e => e.StartsWith("*24", StringComparison.Ordinal) || e.StartsWith("*23", StringComparison.Ordinal));
        Assert.All(Simulador.Escritas, e =>
            Assert.True(Senha.IsMatch(e) || Catalogo.EhLeitura(e) || Catalogo.EscritaDeAudio(e) is not null
                        || e is "*252*0*" or "*252*1*" or "*251*0*" or "*251*1*", e));
    }

    // ----------------------------------------------------------------- apoio

    /// <summary>Um fiscal novo (o app reaberto), com o mudo guardado em <see cref="ArquivoFiscal"/>.</summary>
    public FiscalExtra NovoFiscal() =>
        new(Sessao, Estado, Escritor, Copia, Setups, Analise, Player, Reprodutor, Monitor, Outro, Volume, Log, Relogio, Teste, ArquivoFiscal);

    /// <summary>Um modelo térmico novo (o app aberto de novo, história 13f), com o <see cref="ArquivoTermico"/>; o descanso impede o teste a seco.</summary>
    public TermicoExtra NovoTermico()
    {
        var t = new TermicoExtra(Sessao, Estado, Escritor, Setups, Analise, Player, Comandos, Log, Relogio, ArquivoTermico);
        Teste.Impedimento = () => t.TextoDescanso;
        _termicos.Add(t);
        return t;
    }

    public async Task Conectar()
    {
        await Chaves.Carregada;
        await Copia.Carregada;
        await Link.Carregada;
        await Presets.Carregada;
        await Playlists.Carregada;
        await Setups.Carregada;
        await Analise.Carregada;
        await Sessao.ConectarAsync();
        Assert.Equal(EstadoSessao.Conectado, Sessao.Estado);
        await AndarAte(() => Estado.Pronto && !Estado.Lendo && !Leitura.Lendo && !Copia.EmAndamento && Setups.Conferido && !Setups.Ocupado);
        Assert.Equal(CasaDoDenis.IdSetupCasa, Setups.SetupEmUso?.Id);
        Assert.Equal(IdPlana, Setups.MemoriaEmUso?.Id);
    }

    public async Task AndarAte(Func<bool> condicao, int passos = 3000, int ms = 10)
    {
        for (var i = 0; i < passos && !condicao(); i++)
        {
            Relogio.Avancar(TimeSpan.FromMilliseconds(ms));
            await Task.Delay(1);
        }
        Assert.True(condicao(), "a condição não ficou verdadeira");
    }

    /// <summary>Anda o relógio até a tarefa terminar (com <paramref name="ms"/> por passo).</summary>
    public async Task<T> Rodar<T>(Task<T> tarefa, int ms = 10)
    {
        await AndarAte(() => tarefa.IsCompleted, ms: ms);
        return await tarefa;
    }

    public async Task Rodar(Task tarefa)
    {
        await AndarAte(() => tarefa.IsCompleted);
        await tarefa;
    }

    /// <summary>Senos de 1 kHz de 2 s com os picos (dBFS), na playlist da PLANA ("menos 3"), com o pico das JBL no reprodutor.</summary>
    public async Task Adicionar(params (string Uri, double Pico)[] faixas)
    {
        foreach (var (uri, pico) in faixas)
        {
            Decodificador.Seno(uri, pico);
            var jbl = pico + (double)Bluetooth + (double)GanhoJbl;
            Reprodutor.Picos(uri, (0, jbl), (1, jbl));
        }
        var r = await Playlists.AdicionarAsync(IdPlana,
            faixas.Select(f => new ArquivoAudio(f.Uri, $"menos {Math.Abs(f.Pico):0}", "Testes", TimeSpan.FromSeconds(2))));
        Assert.True(r.Ok, r.Mensagem);
    }

    public Faixa Faixa(string uri) => Playlists.Playlist(IdPlana).Single(f => f.Uri == uri);

    public async Task Otimizar()
    {
        var r = await Rodar(Analise.OtimizarAsync(IdPlana));
        Assert.True(r.Ok, r.Motivo);
    }

    /// <summary>A análise parou (nenhuma faixa pendente e a fila parada).</summary>
    public Task Pronta() => AndarAte(() => !Analise.Ocupada && !Analise.Analisando(IdPlana));

    /// <summary>Conecta, põe as 3 faixas (−3, −6 e −10 dBFS) e otimiza: a análise pronta, com o candidato da de −3.</summary>
    public async Task Preparar()
    {
        await Conectar();
        await Adicionar(("seno-3", -3), ("seno-6", -6), ("seno-10", -10));
        await Otimizar();
        await Pronta();
        Assert.Equal(EstadoExtra.Pronta, Analise.TetoEmUso().Estado);
    }

    /// <summary>O teste a seco inteiro (abrir e confirmar), que tem de passar.</summary>
    public async Task<ResultadoTesteSeco> Validar()
    {
        var abrir = Teste.Abrir();
        Assert.True(abrir.Ok, abrir.Motivo);
        var r = await Rodar(Teste.ConfirmarAsync());
        Assert.True(r.Ok, r.Motivo);
        await AndarAte(() => !Copia.EmAndamento && !Comandos.Ocupado);
        return r;
    }

    /// <summary>A memória PLANA, como está agora.</summary>
    public MemoriaCelular Plana => Setups.Memoria(IdPlana)!;

    /// <summary>O master no simulador.</summary>
    public decimal MasterNoAparelho => decimal.Parse(Simulador.Valor("080")!, System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>O 252 no simulador ("0" ou "1").</summary>
    public string? Modulo => Simulador.Valor("252");

    /// <summary>A porta conectada, aberta e pareada (sem conectar de novo): a porta (para descartar) e a chave.</summary>
    public async Task<(PortaComandos Porta, string Chave)> PortaPareada(TermicoExtra? termico = null)
    {
        var pareamento = new Pareamento(new TextoMemoria(), Relogio, Log);
        var porta = new PortaComandos(Sessao, Chaves, Uso, Historico, pareamento, new PedidosConfirmacao(Relogio, Log),
            new RedeFalsa(), Estado, Escritor, Setups, Gerador, Comandos, Monitor, Log, Relogio, 0, analise: Analise, termico: termico);
        await Historico.Carregado;
        await pareamento.Carregado;
        Assert.True((await Chaves.DefinirAsync(Chave.ControlePelaRede, true, OrigemComando.Tela)).Ok);
        await porta.AtualizarAsync();
        var parear = await porta.AtenderAsync(PedidoPorta.De("POST", "/parear", Casa,
            $$"""{"codigo":"{{pareamento.Codigo}}","nome":"Claude"}"""));
        return (porta, JsonDocument.Parse(parear.Json).RootElement.GetProperty("chave").GetString()!);
    }
}
