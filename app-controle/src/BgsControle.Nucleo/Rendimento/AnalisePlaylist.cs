using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Player;
using BgsControle.Nucleo.Presets;
using BgsControle.Nucleo.Registro;
using BgsControle.Nucleo.Setups;

namespace BgsControle.Nucleo.Rendimento;

/// <summary>O que uma faixa faz numa saída (um canal do processador), sem o master, em dB da escala do limitador.</summary>
/// <param name="Canal">O canal do processador (0 a 3).</param>
/// <param name="Pico">O pico de amostra: 20·log10(|y| máximo), com 0 dB = um tom de 0 dBFS no nível da cadeia.</param>
/// <param name="Rms">A média RMS de toda a faixa, na mesma escala: √(Σy²/N) em dB (um seno de 0 dBFS dá o pico − 3 dB).</param>
public sealed record MedidaCanal(int Canal, double Pico, double Rms);

/// <summary>Um trecho forte de uma faixa (para o teste a seco da 13e), em segundos.</summary>
public sealed record Trecho(double Inicio, double Fim);

/// <summary>A análise de uma faixa numa cadeia (memória + preset): por canal, o pico e a RMS; os trechos fortes; a duração lida.</summary>
public sealed record AnaliseFaixa(IReadOnlyList<MedidaCanal> Canais, IReadOnlyList<Trecho> Trechos, double Duracao)
{
    /// <summary>O pico do canal, ou null se ele não entrou.</summary>
    public double? Pico(int canal) => Canais.FirstOrDefault(c => c.Canal == canal)?.Pico;

    /// <summary>Nenhum canal tem som (todos no piso): a faixa fica fora da conta.</summary>
    public bool Silenciosa => Canais.All(c => c.Pico <= MedidorFaixa.PisoDb);
}

/// <summary>Em que pé está o Extra de uma memória com um preset (a porta: <c>falta</c>, <c>analisando</c>, <c>pronta</c>, <c>indisponivel</c>).</summary>
public enum EstadoExtra
{
    /// <summary>A playlist não foi analisada (a memória não está otimizada, ou nenhuma faixa deu resultado).</summary>
    Falta,

    /// <summary>Alguma faixa ainda está em análise.</summary>
    Analisando,

    /// <summary>Todas as faixas foram analisadas (ou falharam, com o motivo): o teto está pronto; falta o teste a seco.</summary>
    Pronta,

    /// <summary>Faltam as condições dos níveis (Setup, calibração, Bluetooth), a memória ou o preset.</summary>
    Indisponivel,
}

/// <summary>Uma faixa no "quem está pesando".</summary>
public enum SituacaoFaixa
{
    Pronta,
    EmAnalise,
    Falhou,

    /// <summary>Analisada, mas sem som em nenhuma saída: fica fora da conta ("A faixa não tem som.").</summary>
    SemSom,
}

/// <summary>
/// Uma faixa da playlist no teto do Extra: a situação, o peso (quanto o teto subiria sem ela, em dB, a 0,1), o motivo
/// da falha e os trechos fortes.
/// </summary>
public sealed record FaixaExtra(Faixa Faixa, SituacaoFaixa Situacao, decimal Peso, string? Motivo, IReadOnlyList<Trecho> Trechos)
{
    /// <summary>
    /// "−3,0 dB no teto", "Em análise", "Não deu para analisar: o arquivo não foi encontrado", "A faixa não tem som.", ou
    /// null (peso 0).
    /// </summary>
    public string? Texto => Situacao switch
    {
        SituacaoFaixa.EmAnalise => AnalisePlaylist.TextoEmAnalise,
        SituacaoFaixa.Falhou => AnalisePlaylist.MsgNaoAnalisou(Motivo ?? AnalisePlaylist.MsgNaoAbriu),
        SituacaoFaixa.SemSom => AnalisePlaylist.MsgSemSom,
        _ => Peso > 0 ? AnalisePlaylist.MsgPeso(Peso) : null,
    };
}

/// <summary>
/// O teto do Extra de uma memória com um preset (história 13d): o estado, o master candidato (−80..0, no passo de
/// 0,1), o motivo de não haver, o aviso da falta de ganho e as faixas da playlist com o peso.
/// </summary>
public sealed record TetoExtra(EstadoExtra Estado, decimal? Master, string? Motivo, string? Aviso, IReadOnlyList<FaixaExtra> Faixas)
{
    public static TetoExtra Sem(EstadoExtra estado, string motivo) => new(estado, null, motivo, null, []);

    /// <summary>"Teto do Extra: −4,9 dB. Falta o teste a seco." com a análise pronta, ou null.</summary>
    public string? Resultado => Estado == EstadoExtra.Pronta && Master is { } m ? AnalisePlaylist.MsgResultado(m) : null;

    /// <summary>A linha do Extra na folha do nível: "Analisando a playlist.", "Falta o teste a seco." ou "Falta analisar a playlist.".</summary>
    public string TextoNivel => Estado switch
    {
        EstadoExtra.Analisando => AnalisePlaylist.MsgAnalisando,
        EstadoExtra.Pronta when Master is not null => AnalisePlaylist.MsgFaltaTesteSeco,
        _ => Niveis.MsgExtra,
    };
}

/// <summary>
/// A cadeia de uma memória com um preset, no tempo (história 13d), e o que ela faz com uma faixa: por amostra, em
/// <c>double</c> e na taxa da faixa, sobre as amostras em ±1,0 (0 dBFS):
/// <list type="number">
/// <item>o nível do Bluetooth do Setup (um tom de 0 dBFS sai nele, em dB do limitador);</item>
/// <item>o EQ de entrada (<c>030</c>–<c>032</c>), sino RBJ;</item>
/// <item>as 15 bandas (<c>020</c>), sinos RBJ com Q = <see cref="PiorCaso.QBandas"/>;</item>
/// <item>a rota (<c>010</c>): a = L, b = R, a+b = L+R (mono: L = R);</item>
/// <item>o ganho (<c>081</c>) e o EQ de saída (<c>090</c>–<c>092</c>, sino RBJ);</item>
/// <item>os cortes (<c>040</c>–<c>043</c>) em biquads (<see cref="Biquad.Corte"/>);</item>
/// <item>sem o master.</item>
/// </list>
/// As operações são lineares: a ordem não muda o pico [ASSUMPTION: o processador não tem compressão antes do
/// limitador]. Por canal, o pico de amostra, a soma dos quadrados (a RMS) e o maior |y| de cada bloco de 0,1 s (de
/// qualquer canal), de onde saem os 2 trechos fortes de 10 s.
/// </summary>
public sealed class MedidorFaixa
{
    public const int Canais = 4;

    /// <summary>A duração de um trecho forte, em segundos.</summary>
    public const double DuracaoTrecho = 10;

    /// <summary>A duração de um bloco dos picos por tempo, em segundos.</summary>
    public const double DuracaoBloco = 0.1;

    /// <summary>O menor dB que sai (o silêncio).</summary>
    public const double PisoDb = -200;

    private readonly double _bluetooth;
    private readonly (double Ganho, double F0, double Q)? _entrada;
    private readonly IReadOnlyList<(double F0, double Ganho)> _bandas;
    private readonly int[] _rota = new int[Canais];
    private readonly double[] _ganho = new double[Canais];
    private readonly (double Ganho, double F0, double Q)?[] _saida = new (double, double, double)?[Canais];
    private readonly PassaAlta?[] _passaAlta = new PassaAlta?[Canais];
    private readonly PassaBaixa?[] _passaBaixa = new PassaBaixa?[Canais];

    private int _taxa;
    private Biquad[] _globalL = [];
    private Biquad[] _globalR = [];
    private readonly Biquad[][] _porCanal = new Biquad[Canais][];
    private readonly double[] _pico = new double[Canais];
    private readonly double[] _soma = new double[Canais];
    private long _quadros;
    private readonly List<double> _blocos = [];
    private double _blocoMax;
    private int _noBloco;
    private int _porBloco;

    private MedidorFaixa(double bluetooth, (double, double, double)? entrada, IReadOnlyList<(double, double)> bandas)
    {
        _bluetooth = bluetooth;
        _entrada = entrada;
        _bandas = bandas;
    }

    /// <summary>
    /// A cadeia lida (a memória e o preset, ou o espelho), ou o dado que falta, com a mesma frase do
    /// <see cref="PiorCaso"/> ("O app não sabe o valor de Ganho da saída 1.").
    /// </summary>
    public static (MedidorFaixa? Medidor, string? Falta) Montar(Func<Endereco, Valor?> ler, decimal nivelBluetooth)
    {
        var (entrada, faltaEntrada) = PiorCaso.Sinos(ler, null, "030", "031", "032");
        if (faltaEntrada is not null)
            return (null, faltaEntrada);
        var bandas = new List<(double, double)>();
        for (var b = 0; b < Catalogo.BandasHz.Count; b++)
        {
            if (ler(new Endereco("020", b))?.Numero is not { } g)
                return (null, PiorCaso.MsgFalta("020", b));
            if (g != 0)
                bandas.Add((Catalogo.BandasHz[b], (double)g));
        }
        var m = new MedidorFaixa((double)nivelBluetooth, entrada, bandas);
        for (var c = 0; c < Canais; c++)
        {
            if (ler(new Endereco("010", c)) is not { } rota)
                return (null, PiorCaso.MsgFalta("010", c));
            // Uma rota que o app não conhece é dado que falta (não vira a+b em silêncio).
            m._rota[c] = rota.Texto switch { "a" => 0, "b" => 1, "a+b" => 2, _ => -1 };
            if (m._rota[c] < 0)
                return (null, PiorCaso.MsgFalta("010", c));
            if (ler(new Endereco(Camadas.CodigoGanho, c))?.Numero is not { } ganho)
                return (null, PiorCaso.MsgFalta(Camadas.CodigoGanho, c));
            m._ganho[c] = (double)ganho;
            var (saida, faltaSaida) = PiorCaso.Sinos(ler, c, "091", "090", "092");
            if (faltaSaida is not null)
                return (null, faltaSaida);
            m._saida[c] = saida;
            m._passaAlta[c] = LimiteFisico.PassaAltaDe(ler, c);
            m._passaBaixa[c] = PassaBaixa.DoAparelho(ler(new Endereco("043", c))?.Numero, ler(new Endereco("041", c))?.Numero);
        }
        return (m, null);
    }

    /// <summary>Quantos quadros (amostras por canal) já passaram.</summary>
    public long Quadros => _quadros;

    /// <summary>
    /// Passa um bloco do decodificador: as amostras intercaladas, os canais (1 = mono, L = R; acima de 2, só os dois
    /// primeiros) e a taxa. A taxa não pode mudar no meio da faixa, e uma amostra que não é número finito (NaN, infinito)
    /// recusa a faixa.
    /// </summary>
    public void Processar(float[] bloco, int canais, int taxa)
    {
        if (canais < 1 || taxa <= 0)
            throw new ErroDecodificacao(ErroDecodificacao.MsgFormato);
        if (_taxa == 0)
            Preparar(taxa);
        else if (taxa != _taxa)
            throw new ErroDecodificacao(ErroDecodificacao.MsgTaxa);

        var gBt = Math.Pow(10, _bluetooth / 20);
        var quadros = bloco.Length / canais;
        for (var i = 0; i < quadros; i++)
        {
            var al = bloco[i * canais];
            var ar = canais > 1 ? bloco[i * canais + 1] : al;
            if (!float.IsFinite(al) || !float.IsFinite(ar))
                throw new ErroDecodificacao(ErroDecodificacao.MsgFormato);
            var l = al * gBt;
            var r = ar * gBt;
            foreach (var f in _globalL)
                l = f.Processar(l);
            foreach (var f in _globalR)
                r = f.Processar(r);
            for (var c = 0; c < Canais; c++)
            {
                var x = (_rota[c] switch { 0 => l, 1 => r, _ => l + r }) * _ganho[c];
                foreach (var f in _porCanal[c])
                    x = f.Processar(x);
                var abs = Math.Abs(x);
                if (abs > _pico[c])
                    _pico[c] = abs;
                _soma[c] += x * x;
                if (abs > _blocoMax)
                    _blocoMax = abs;
            }
            _quadros++;
            if (++_noBloco == _porBloco)
            {
                _blocos.Add(_blocoMax);
                _blocoMax = 0;
                _noBloco = 0;
            }
        }
    }

    /// <summary>O resultado do que passou: o pico e a RMS por canal, os trechos fortes e a duração.</summary>
    public AnaliseFaixa Resultado()
    {
        var blocos = new List<double>(_blocos);
        if (_noBloco > 0)
            blocos.Add(_blocoMax);
        var duracao = _taxa > 0 ? _quadros / (double)_taxa : 0;
        var canais = Enumerable.Range(0, Canais)
            .Select(c => new MedidaCanal(c, Db(_pico[c]), _quadros > 0 ? Db(Math.Sqrt(_soma[c] / _quadros)) : PisoDb))
            .ToArray();
        return new AnaliseFaixa(canais, Trechos(blocos, DuracaoBloco, duracao), duracao);
    }

    private void Preparar(int taxa)
    {
        _taxa = taxa;
        _porBloco = Math.Max(1, (int)Math.Round(taxa * DuracaoBloco));
        Biquad[] Global()
        {
            var lista = new List<Biquad>();
            if (_entrada is { } e && Biquad.Sino(taxa, e.F0, e.Ganho, e.Q) is { } sinoEntrada)
                lista.Add(sinoEntrada);
            foreach (var (f0, g) in _bandas)
            {
                if (Biquad.Sino(taxa, f0, g, PiorCaso.QBandas) is { } banda)
                    lista.Add(banda);
            }
            return lista.ToArray();
        }
        _globalL = Global();
        _globalR = Global();
        for (var c = 0; c < Canais; c++)
        {
            var lista = new List<Biquad>();
            if (_saida[c] is { } s && Biquad.Sino(taxa, s.F0, s.Ganho, s.Q) is { } sinoSaida)
                lista.Add(sinoSaida);
            lista.AddRange(Biquad.De(_passaAlta[c], taxa));
            lista.AddRange(Biquad.De(_passaBaixa[c], taxa));
            _porCanal[c] = lista.ToArray();
            _ganho[c] = Math.Pow(10, _ganho[c] / 20);
        }
    }

    private static double Db(double amplitude) =>
        amplitude > 0 ? Math.Max(PisoDb, 20 * Math.Log10(amplitude)) : PisoDb;

    /// <summary>
    /// Os 2 trechos de <see cref="DuracaoTrecho"/> s que não se sobrepõem e têm os maiores picos, em ordem de tempo: o
    /// primeiro centrado no bloco mais forte; o segundo no bloco mais forte fora dele, do lado em que cabe um trecho
    /// inteiro. Uma faixa de até 10 s dá um trecho com a faixa toda; sem lugar para o segundo, só um.
    /// </summary>
    /// <param name="picos">O maior |y| de cada bloco, na ordem.</param>
    /// <param name="duracaoBloco">A duração de um bloco, em segundos.</param>
    /// <param name="duracao">A duração da faixa, em segundos.</param>
    public static IReadOnlyList<Trecho> Trechos(IReadOnlyList<double> picos, double duracaoBloco, double duracao)
    {
        if (duracao <= 0 || picos.Count == 0)
            return [];
        if (duracao <= DuracaoTrecho)
            return [new Trecho(0, Math.Round(duracao, 1))];
        var ordem = Enumerable.Range(0, picos.Count).OrderByDescending(i => picos[i]).ThenBy(i => i).ToArray();
        double Centro(int i) => Math.Min((i + 0.5) * duracaoBloco, duracao);

        var inicio1 = Encaixar(Centro(ordem[0]) - DuracaoTrecho / 2, 0, duracao - DuracaoTrecho);
        var primeiro = new Trecho(inicio1, Math.Min(inicio1 + DuracaoTrecho, duracao));
        foreach (var i in ordem.Skip(1))
        {
            var t = Centro(i);
            if (t >= primeiro.Inicio && t <= primeiro.Fim)
                continue;
            double inicio;
            if (t < primeiro.Inicio)
            {
                if (primeiro.Inicio < DuracaoTrecho)
                    continue;
                inicio = Encaixar(t - DuracaoTrecho / 2, 0, primeiro.Inicio - DuracaoTrecho);
            }
            else
            {
                if (duracao - primeiro.Fim < DuracaoTrecho)
                    continue;
                inicio = Encaixar(t - DuracaoTrecho / 2, primeiro.Fim, duracao - DuracaoTrecho);
            }
            var segundo = new Trecho(inicio, Math.Min(inicio + DuracaoTrecho, duracao));
            return segundo.Inicio < primeiro.Inicio ? [segundo, primeiro] : [primeiro, segundo];
        }
        return [primeiro];
    }

    /// <summary>
    /// O início dentro de [min, max], no passo de 0,1 s: o arredondamento não sai da faixa (o ruído do double é tirado antes
    /// do teto e do piso, para 12,0000001 não virar 12,1).
    /// </summary>
    private static double Encaixar(double inicio, double min, double max)
    {
        var dentro = Math.Clamp(inicio, min, max);
        var redondo = Math.Round(dentro, 1);
        return redondo < min ? Math.Ceiling(Math.Round(min * 10, 6)) / 10
            : redondo > max ? Math.Floor(Math.Round(max * 10, 6)) / 10
            : redondo;
    }
}


/// <summary>
/// A análise da playlist e o teto do Extra (história 13d, spec-rendimento CAP-5). Começa pelo "Otimizar configuração na
/// playlist" da memória (<see cref="OtimizarAsync"/>), que a marca <see cref="MemoriaCelular.Otimizada"/>. Numa memória
/// otimizada, cada faixa é analisada sozinha, uma por vez e em segundo plano, em todos os presets do celular de uma
/// vez (o áudio é lido uma vez e passa por uma cadeia por preset, <see cref="MedidorFaixa"/>); uma memória sem preset
/// lembrado (ou com ele apagado) usa as 15 bandas planas, na chave <see cref="SemPreset"/> (como os masters da 13c):
/// <list type="bullet">
/// <item>uma faixa nova (sem nenhuma análise pronta) fica "em análise" e o Player não a toca até terminar;</item>
/// <item>a análise é refeita quando muda a regulagem guardada da memória (a cadeia, sem o master), as bandas de um preset,
/// ou a ficha, a calibração ou o nível do Bluetooth do Setup: a "impressão" da cadeia muda. Na reanálise as faixas
/// seguem tocando; só o Extra fica "analisando";</item>
/// <item>apagar a faixa, desotimizar a memória ou fechar o app cancela a análise em curso, e nada dela é gravado.</item>
/// </list>
/// Precisa das condições dos níveis da 13c (<see cref="Niveis.Indisponivel"/>). Um dado da cadeia que falta numa
/// combinação memória + preset deixa aquele teto indisponível, com o motivo, e não falha as faixas. O teto do Extra por
/// preset é master = min sobre as faixas e as saídas de (teto − 1 − pico), com teto = min(X, C) do canal e o corte da
/// memória (<see cref="Conta"/>). Os resultados ficam em <c>analises.json</c>, por faixa (URI + duração) e impressão. A
/// análise não muda o som: o teto do Extra é só calculado e mostrado (aplicar é da 13e e da 13f).
/// </summary>
public sealed class AnalisePlaylist : IDisposable
{
    public const int Formato = 1;

    /// <summary>Entra na impressão: uma conta nova invalida as análises guardadas.</summary>
    public const string VersaoConta = "13d.2";

    /// <summary>As análises guardadas no máximo (as mais antigas saem).</summary>
    public const int MaximoGuardadas = 5000;

    /// <summary>A margem do Extra abaixo de min(X, C), em dB.</summary>
    public const decimal Margem = 1m;

    /// <summary>A chave da memória sem preset (as 15 bandas planas), a mesma dos masters da 13c.</summary>
    public const string SemPreset = "";

    public const string TextoEmAnalise = "Em análise";
    public const string MsgAnalisando = "Analisando a playlist.";
    public const string MsgFaltaTesteSeco = "Falta o teste a seco.";
    public const string MsgSemMemoria = "Sem memória do celular em uso.";
    public const string MsgSemPreset = "A memória não lembra um preset.";
    public const string MsgNenhumaFaixa = "Nenhuma faixa da playlist foi analisada.";
    public const string MsgNaoAbriu = "o arquivo não abriu";
    public const string MsgSemAudio = "a faixa não tem áudio";
    public const string MsgSemSom = "A faixa não tem som.";
    public const string MsgInterrompida = "a leitura foi interrompida";
    public const string MsgOutroSetup = "A memória é de outro Setup. Escolha o Setup dela para otimizar.";

    /// <summary>Os códigos da regulagem guardada que entram na cadeia (o master fica de fora).</summary>
    public static IReadOnlySet<string> CodigosDaMemoria { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "030", "031", "032", "010", "081", "090", "091", "092", "040", "041", "042", "043",
    };

    private static readonly CultureInfo Fio = CultureInfo.InvariantCulture;

    /// <summary>As 15 bandas planas da memória sem preset.</summary>
    private static readonly Preset Plano = new(SemPreset, "Sem preset", default, GerentePresets.BandasPlanas);

    /// <summary>"−3,0 dB no teto".</summary>
    public static string MsgPeso(decimal peso) => $"−{peso.ToString("0.0", Fio).Replace('.', ',')} dB no teto";

    /// <summary>"Não deu para analisar: o arquivo não foi encontrado".</summary>
    public static string MsgNaoAnalisou(string motivo) => $"Não deu para analisar: {motivo}";

    /// <summary>"Teto do Extra: −4,9 dB. Falta o teste a seco."</summary>
    public static string MsgResultado(decimal master) => $"Teto do Extra: {LimiteFisico.Db(master)} dB. {MsgFaltaTesteSeco}";

    private readonly GerenteSetups _setups;
    private readonly GerentePresets _presets;
    private readonly GerentePlaylists _playlists;
    private readonly IDecodificadorAudio _decodificador;
    private readonly IRepositorioTexto _repositorio;
    private readonly LogBle _log;

    private readonly object _trava = new();
    private readonly Dictionary<string, Guardada> _guardadas = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Falha> _falhas = new(StringComparer.Ordinal);
    private long _ordem;
    private int _versao;
    private Foto? _foto;
    private Trabalho? _atual;
    private CancellationTokenSource? _cancelar;
    private bool _rodando;
    private bool _deNovo;
    private bool _descartada;

    public AnalisePlaylist(GerenteSetups setups, GerentePresets presets, GerentePlaylists playlists, IDecodificadorAudio decodificador,
        IRepositorioTexto repositorio, LogBle log)
    {
        _setups = setups;
        _presets = presets;
        _playlists = playlists;
        _decodificador = decodificador;
        _repositorio = repositorio;
        _log = log;
        // A carga existe antes de qualquer aviso: a fila sempre espera por ela.
        Carregada = Task.Run(CarregarAsync);
        _setups.Mudou += Reavaliar;
        _presets.Mudou += Reavaliar;
        _playlists.Mudou += Reavaliar;
        _ = Carregada.ContinueWith(_ => Pedir(), TaskScheduler.Default);
    }

    /// <summary>Completa quando o <c>analises.json</c> foi lido (ausente ou estragado = nenhuma análise).</summary>
    public Task Carregada { get; }

    /// <summary>Uma análise começou, terminou, falhou ou foi cancelada, ou a playlist, a memória ou o preset mudou. Qualquer thread.</summary>
    public event Action? Mudou;

    /// <summary>Uma faixa está sendo lida agora (ou esperando a vez).</summary>
    public bool Ocupada
    {
        get
        {
            lock (_trava)
                return _rodando;
        }
    }

    // ------------------------------------------------------------------ comandos

    /// <summary>
    /// "Otimizar configuração na playlist" (história 13d): com o Setup da memória em uso, todas as saídas calibradas e o
    /// Bluetooth medido (senão, o motivo da 13c), marca a memória otimizada, esquece as falhas de antes (elas são
    /// tentadas de novo) e começa a análise em segundo plano. Volta logo, sem esperar a análise.
    /// </summary>
    public async Task<ResultadoSetup> OtimizarAsync(string? idMemoria)
    {
        await Carregada;
        await _setups.Carregada;
        if (_setups.Memoria(idMemoria) is not { } memoria)
            return ResultadoSetup.Falha(idMemoria is null ? MsgSemMemoria : GerenteSetups.MsgMemoriaNaoExiste);
        var emUso = _setups.SetupEmUso;
        if (emUso is not null && emUso.Id != memoria.IdSetup)
            return ResultadoSetup.Falha(MsgOutroSetup);
        if (Niveis.Indisponivel(emUso, emUso is null ? [] : Ligacoes.Saidas(emUso.Equipamentos)) is { } motivo)
            return ResultadoSetup.Falha(motivo);
        var ids = _playlists.Playlist(memoria.Id).Select(f => f.Id).ToHashSet(StringComparer.Ordinal);
        lock (_trava)
        {
            foreach (var id in _falhas.Keys.Where(ids.Contains).ToArray())
                _falhas.Remove(id);
            _versao++;
        }
        var r = await _setups.DefinirOtimizadaAsync(memoria.Id, true);
        if (!r.Ok)
            return r;
        _log.Evento($"análise da playlist da {memoria.Nome}: começou");
        Pedir();
        Notificar();
        return ResultadoSetup.Feito(MsgAnalisando);
    }

    /// <summary>Desotimiza a memória: a análise em curso dela é cancelada e nada dela é gravado.</summary>
    public async Task<ResultadoSetup> DesotimizarAsync(string? idMemoria)
    {
        await Carregada;
        if (_setups.Memoria(idMemoria) is not { } memoria)
            return ResultadoSetup.Falha(idMemoria is null ? MsgSemMemoria : GerenteSetups.MsgMemoriaNaoExiste);
        return await _setups.DefinirOtimizadaAsync(memoria.Id, false);
    }

    /// <summary>
    /// Fechar a janela do app: a análise em curso é cancelada e nada dela é gravado. A fila continua viva (a faixa volta a
    /// ser lida do começo quando a fila andar de novo); o <see cref="Dispose"/> é só o fim do processo.
    /// </summary>
    public void CancelarEmCurso()
    {
        lock (_trava)
            _cancelar?.Cancel();
    }

    // ------------------------------------------------------------------ o que se vê

    /// <summary>
    /// A faixa (pelo id) é nova numa memória otimizada: ainda não tem nenhuma análise pronta (de nenhuma cadeia) nem falha.
    /// Ela não toca. Uma faixa que só está sendo reanalisada (a cadeia mudou) segue tocando.
    /// </summary>
    public bool EmAnalise(string idFaixa) => Fotografar().EmAnalise.Contains(idFaixa);

    /// <summary>A playlist da memória tem faixas pendentes (novas ou em reanálise).</summary>
    public bool Analisando(string? idMemoria) =>
        idMemoria is not null && Fotografar().Memorias.TryGetValue(idMemoria, out var m) && m.Pendentes.Count > 0;

    /// <summary>O teto do Extra da memória em uso com o preset em uso.</summary>
    public TetoExtra TetoEmUso() => _setups.MemoriaEmUso is { } m ? Teto(m.Id, _setups.IdPresetEmUso) : TetoExtra.Sem(EstadoExtra.Indisponivel, MsgSemMemoria);

    /// <summary>O teto do Extra da memória com o preset que vale para ela: o em uso, se ela está em uso; senão, o que ela lembra.</summary>
    public TetoExtra TetoDa(string? idMemoria) =>
        idMemoria is not null && _setups.MemoriaEmUso?.Id == idMemoria ? Teto(idMemoria, _setups.IdPresetEmUso)
        : Teto(idMemoria, _setups.Memoria(idMemoria)?.IdPreset);

    /// <summary>
    /// O teto do Extra da memória com o preset (null, ou um preset que não existe mais: as bandas planas): o estado, o
    /// master candidato (a menor folga entre as faixas analisadas e as saídas, menos 1 dB), o aviso "falta ganho" e cada
    /// faixa com o peso, "em análise", o motivo da falha ou "sem som".
    /// </summary>
    public TetoExtra Teto(string? idMemoria, string? idPreset)
    {
        if (_setups.Memoria(idMemoria) is not { } memoria)
            return TetoExtra.Sem(EstadoExtra.Indisponivel, MsgSemMemoria);
        if (!memoria.Otimizada)
            return TetoExtra.Sem(EstadoExtra.Falta, Niveis.MsgExtra);
        var foto = Fotografar();
        if (!foto.Memorias.TryGetValue(memoria.Id, out var info))
            return TetoExtra.Sem(EstadoExtra.Falta, Niveis.MsgExtra);
        if (info.Motivo is { } motivo)
            return TetoExtra.Sem(EstadoExtra.Indisponivel, motivo);
        var preset = idPreset is { } id && foto.Presets.FirstOrDefault(p => p.Id == id) is { } achado ? achado : Plano;
        if (info.Faltas.TryGetValue(preset.Id, out var falta))
            return TetoExtra.Sem(EstadoExtra.Indisponivel, falta);
        if (!info.Impressoes.TryGetValue(preset.Id, out var impressao))
            return TetoExtra.Sem(EstadoExtra.Indisponivel, MsgSemPreset);

        var faixas = foto.Playlists.PorMemoria.GetValueOrDefault(memoria.Id) ?? [];
        var prontas = new List<(Faixa Faixa, AnaliseFaixa Analise)>();
        var situacoes = new Dictionary<string, (SituacaoFaixa Situacao, string? Motivo)>(StringComparer.Ordinal);
        lock (_trava)
        {
            foreach (var f in faixas)
            {
                if (_guardadas.TryGetValue(Chave(f, impressao), out var g))
                {
                    prontas.Add((f, g.Analise));
                    situacoes[f.Id] = g.Analise.Silenciosa ? (SituacaoFaixa.SemSom, MsgSemSom) : (SituacaoFaixa.Pronta, null);
                }
                else if (_falhas.TryGetValue(f.Id, out var falha) && falha.Impressoes.Contains(impressao))
                {
                    situacoes[f.Id] = (SituacaoFaixa.Falhou, falha.Motivo);
                }
                else
                {
                    situacoes[f.Id] = (SituacaoFaixa.EmAnalise, null);
                }
            }
        }

        var limites = LimiteFisico.Calcular(info.Setup!.Equipamentos, Ligacoes.Saidas(info.Setup.Equipamentos), Leitor(memoria, preset),
            semTermico: true);
        var conta = Conta(limites, prontas.Select(p => (p.Faixa.Id, p.Analise)).ToArray());
        var lista = faixas.Select(f =>
        {
            var (situacao, motivoFaixa) = situacoes[f.Id];
            var analise = prontas.FirstOrDefault(p => p.Faixa.Id == f.Id).Analise;
            return new FaixaExtra(f, situacao, conta.Pesos.GetValueOrDefault(f.Id), motivoFaixa, analise?.Trechos ?? []);
        }).ToArray();

        var estado = situacoes.Values.Any(s => s.Situacao == SituacaoFaixa.EmAnalise) ? EstadoExtra.Analisando
            : conta.Master is not null ? EstadoExtra.Pronta
            : EstadoExtra.Falta;
        var motivoTeto = conta.Master is null ? conta.Falta ?? (estado == EstadoExtra.Analisando ? MsgAnalisando : MsgNenhumaFaixa) : null;
        return new TetoExtra(estado, conta.Master, motivoTeto, conta.Aviso, lista);
    }

    /// <summary>A conta do teto do Extra: o master candidato, a saída que pede ganho (acima de 0) e o peso de cada faixa.</summary>
    public sealed record ContaExtra(decimal? Master, string? Falta, string? FaltaGanho, IReadOnlyDictionary<string, decimal> Pesos)
    {
        /// <summary>"O nível não chega ao teto: falta ganho na memória. Saída: JBL L.", ou null.</summary>
        public string? Aviso => FaltaGanho is { } saida ? Niveis.MsgFaltaGanhoNa(saida) : null;
    }

    /// <summary>
    /// O teto do Extra (puro): master = min sobre as faixas com som e os canais calibrados e com limite de
    /// (teto − <see cref="Margem"/> − pico), com o teto de cada canal pelos <paramref name="limites"/> (min(X, C) e o
    /// corte da memória), arredondado para baixo a 0,1, dentro de −80..0 (acima de 0, a saída que pede ganho). Uma faixa
    /// sem som fica de fora. O peso das faixas que mandam (as de master igual ao mínimo, a 0,1) é o master sem o grupo
    /// todo − o master com todas; as outras pesam 0. Sem as outras faixas, o master sem o grupo é o topo (0 dB).
    /// </summary>
    public static ContaExtra Conta(IReadOnlyList<LimiteSaida> limites, IReadOnlyList<(string Id, AnaliseFaixa Analise)> faixas)
    {
        var canais = limites.Where(l => l.Calibrada && l.TetoDb is not null)
            .SelectMany(l => l.PorCanal.Select(c => (Saida: l.Nome, c.Canal, Teto: (double)c.TetoDb)))
            .ToArray();
        if (canais.Length == 0)
            return new ContaExtra(null, Niveis.MsgSemLimite, null, new Dictionary<string, decimal>());
        var comSom = faixas.Where(f => !f.Analise.Silenciosa).ToArray();
        if (comSom.Length == 0)
            return new ContaExtra(null, null, null, new Dictionary<string, decimal>());

        var porFaixa = comSom.Select(f =>
        {
            var pior = canais
                .Select(c => (c.Saida, Folga: c.Teto - (double)Margem - (f.Analise.Pico(c.Canal) ?? MedidorFaixa.PisoDb)))
                .MinBy(x => x.Folga);
            return (f.Id, pior.Saida, pior.Folga, Master: Arredondar(pior.Folga).Master);
        }).ToArray();

        var manda = porFaixa.MinBy(x => x.Folga);
        var (master, acima) = Arredondar(manda.Folga);
        var grupo = porFaixa.Where(x => x.Master == master).Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
        var outras = porFaixa.Where(x => !grupo.Contains(x.Id)).ToArray();
        var sem = outras.Length == 0 ? 0m : Arredondar(outras.Min(x => x.Folga)).Master;
        var pesos = porFaixa.ToDictionary(x => x.Id, x => grupo.Contains(x.Id) ? Math.Max(0m, sem - master) : 0m, StringComparer.Ordinal);
        return new ContaExtra(master, null, acima ? manda.Saida : null, pesos);
    }

    /// <summary>O master no passo de 0,1 para baixo, dentro de −80..0, e se a conta passou de 0 (falta ganho).</summary>
    private static (decimal Master, bool Acima) Arredondar(double folga)
    {
        // O arredondamento a 6 casas tira o ruído do double antes do piso (como no PiorCaso).
        var master = (decimal)Math.Floor(Math.Round(Math.Clamp(folga, -1000, 1000) * 10, 6)) / 10m;
        if (master > 0)
            return (0m, true);
        return (Math.Max(master, PiorCaso.MinimoMaster), false);
    }

    // ------------------------------------------------------------------ a fotografia do que falta analisar

    /// <summary>
    /// Uma memória otimizada: o Setup, o motivo de não analisar, a impressão por preset (só as cadeias que se montam), o dado
    /// que falta nas outras e as faixas pendentes.
    /// </summary>
    private sealed record InfoMemoria(MemoriaCelular Memoria, Setup? Setup, string? Motivo, IReadOnlyDictionary<string, string> Impressoes,
        IReadOnlyDictionary<string, string> Faltas, IReadOnlySet<string> Pendentes);

    private sealed record Foto(DadosSetups Setups, DadosPlaylists Playlists, IReadOnlyList<Preset> Presets, int Versao,
        IReadOnlyDictionary<string, InfoMemoria> Memorias, IReadOnlySet<string> EmAnalise);

    private sealed record Falha(string Motivo, HashSet<string> Impressoes);

    private sealed record Guardada(AnaliseFaixa Analise, long Ordem, string Uri, long DuracaoMs, string Impressao)
    {
        public string Faixa => ChaveFaixa(Uri, DuracaoMs);
    }

    /// <summary>Uma cadeia a medir: o preset, a impressão e o leitor (a memória com o preset).</summary>
    private sealed record Cadeia(string IdPreset, string Impressao, Func<Endereco, Valor?> Ler);

    private sealed record Trabalho(string IdMemoria, Faixa Faixa, decimal NivelBluetooth, IReadOnlyList<Cadeia> Cadeias);

    /// <summary>Os presets de uma memória: todos os do celular e, sem o lembrado, as bandas planas.</summary>
    private static IEnumerable<Preset> PresetsDa(MemoriaCelular m, IReadOnlyList<Preset> presets) =>
        m.IdPreset is { } id && presets.Any(p => p.Id == id) ? presets : presets.Append(Plano);

    /// <summary>O estado de agora (refeito quando os Setups, as playlists, os presets ou as análises mudam).</summary>
    private Foto Fotografar()
    {
        var setups = _setups.Dados;
        var playlists = _playlists.Dados;
        var presets = _presets.Presets;
        lock (_trava)
        {
            if (_foto is { } f && ReferenceEquals(f.Setups, setups) && ReferenceEquals(f.Playlists, playlists)
                && ReferenceEquals(f.Presets, presets) && f.Versao == _versao)
                return f;
            var memorias = new Dictionary<string, InfoMemoria>(StringComparer.Ordinal);
            var emAnalise = new HashSet<string>(StringComparer.Ordinal);
            var jaAnalisadas = _guardadas.Values.Select(g => g.Faixa).ToHashSet(StringComparer.Ordinal);
            var impressoesSetup = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var m in setups.Memorias.Where(m => m.Otimizada))
            {
                var setup = setups.Setups.FirstOrDefault(s => s.Id == m.IdSetup);
                var motivo = Niveis.Indisponivel(setup, setup is null ? [] : Ligacoes.Saidas(setup.Equipamentos));
                if (setup is null || motivo is not null)
                {
                    memorias[m.Id] = new InfoMemoria(m, setup, motivo ?? Niveis.MsgSemSetup, new Dictionary<string, string>(),
                        new Dictionary<string, string>(), new HashSet<string>());
                    continue;
                }
                if (!impressoesSetup.TryGetValue(setup.Id, out var doSetup))
                    impressoesSetup[setup.Id] = doSetup = ImpressaoDoSetup(setup);
                var impressoes = new Dictionary<string, string>(StringComparer.Ordinal);
                var faltas = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var p in PresetsDa(m, presets))
                {
                    // Um dado da cadeia que falta é da combinação memória + preset, não das faixas.
                    if (MedidorFaixa.Montar(Leitor(m, p), setup.NivelBluetooth!.Valor).Falta is { } falta)
                        faltas[p.Id] = falta;
                    else
                        impressoes[p.Id] = Impressao(m, p, doSetup);
                }
                var pendentes = new HashSet<string>(StringComparer.Ordinal);
                foreach (var faixa in playlists.PorMemoria.GetValueOrDefault(m.Id) ?? [])
                {
                    if (Faltam(faixa, impressoes.Values).Count == 0)
                        continue;
                    pendentes.Add(faixa.Id);
                    // Só a faixa nova não toca: sem nenhuma análise pronta e sem falha. A reanálise não bloqueia o Player.
                    if (!jaAnalisadas.Contains(ChaveFaixa(faixa)) && !_falhas.ContainsKey(faixa.Id))
                        emAnalise.Add(faixa.Id);
                }
                memorias[m.Id] = new InfoMemoria(m, setup, null, impressoes, faltas, pendentes);
            }
            return _foto = new Foto(setups, playlists, presets, _versao, memorias, emAnalise);
        }
    }

    /// <summary>As impressões que a faixa ainda não tem (nem guardada, nem falhada). Chamar com a trava.</summary>
    private List<string> Faltam(Faixa faixa, IEnumerable<string> impressoes)
    {
        var falha = _falhas.GetValueOrDefault(faixa.Id);
        return impressoes.Distinct(StringComparer.Ordinal)
            .Where(i => !_guardadas.ContainsKey(Chave(faixa, i)) && falha?.Impressoes.Contains(i) != true)
            .ToList();
    }

    /// <summary>O próximo trabalho: a primeira faixa pendente, a memória em uso primeiro, na ordem da playlist.</summary>
    private Trabalho? Proximo()
    {
        var foto = Fotografar();
        var emUso = _setups.MemoriaEmUso?.Id;
        var ordem = foto.Memorias.Values
            .Where(m => m.Motivo is null && m.Pendentes.Count > 0)
            .OrderBy(m => m.Memoria.Id == emUso ? 0 : 1)
            .ToArray();
        foreach (var info in ordem)
        {
            var presets = PresetsDa(info.Memoria, foto.Presets).ToArray();
            foreach (var faixa in foto.Playlists.PorMemoria.GetValueOrDefault(info.Memoria.Id) ?? [])
            {
                if (!info.Pendentes.Contains(faixa.Id))
                    continue;
                List<string> faltam;
                lock (_trava)
                    faltam = Faltam(faixa, info.Impressoes.Values);
                if (faltam.Count == 0)
                    continue;
                var cadeias = faltam
                    .Select(imp => info.Impressoes.First(p => p.Value == imp))
                    .Select(p => (Preset: presets.First(x => x.Id == p.Key), Impressao: p.Value))
                    .Select(p => new Cadeia(p.Preset.Id, p.Impressao, Leitor(info.Memoria, p.Preset)))
                    .ToArray();
                return new Trabalho(info.Memoria.Id, faixa, info.Setup!.NivelBluetooth!.Valor, cadeias);
            }
        }
        return null;
    }

    /// <summary>O trabalho ainda vale: a memória otimizada e analisável, a faixa na playlist e as impressões ainda pedidas.</summary>
    private bool AindaVale(Trabalho t)
    {
        var foto = Fotografar();
        if (!foto.Memorias.TryGetValue(t.IdMemoria, out var info) || info.Motivo is not null)
            return false;
        if ((foto.Playlists.PorMemoria.GetValueOrDefault(t.IdMemoria) ?? []).All(f => f.Id != t.Faixa.Id))
            return false;
        var pedidas = info.Impressoes.Values.ToHashSet(StringComparer.Ordinal);
        return t.Cadeias.All(c => pedidas.Contains(c.Impressao));
    }

    // ------------------------------------------------------------------ o trabalho em segundo plano

    private void Reavaliar()
    {
        try
        {
            Trabalho? atual;
            CancellationTokenSource? cancelar;
            lock (_trava)
            {
                if (_descartada)
                    return;
                atual = _atual;
                cancelar = _cancelar;
            }
            if (atual is not null && !AindaVale(atual))
            {
                lock (_trava)
                {
                    if (ReferenceEquals(_cancelar, cancelar))
                        cancelar?.Cancel();
                }
            }
        }
        catch (Exception e)
        {
            _log.Evento($"análise da playlist: falha ao conferir ({e.GetType().Name})");
        }
        Pedir();
        Notificar();
    }

    /// <summary>Pede a fila (ela roda numa tarefa; com uma andando, ela confere de novo no fim).</summary>
    private void Pedir()
    {
        lock (_trava)
        {
            if (_descartada)
                return;
            if (_rodando)
            {
                _deNovo = true;
                return;
            }
            _rodando = true;
        }
        _ = Task.Run(RodarAsync);
    }

    private async Task RodarAsync()
    {
        await Carregada;
        while (true)
        {
            lock (_trava)
            {
                if (_descartada)
                {
                    _rodando = false;
                    return;
                }
            }
            Trabalho? t = null;
            try
            {
                t = Proximo();
            }
            catch (Exception e)
            {
                _log.Evento($"análise da playlist: falha ao escolher a faixa ({e.GetType().Name})");
            }
            if (t is null)
            {
                lock (_trava)
                {
                    if (!_deNovo || _descartada)
                    {
                        _rodando = false;
                        break;
                    }
                    _deNovo = false;
                }
                continue;
            }
            await ExecutarAsync(t);
        }
        Notificar();
    }

    private async Task ExecutarAsync(Trabalho t)
    {
        var cancelar = new CancellationTokenSource();
        lock (_trava)
        {
            if (_descartada)
            {
                cancelar.Dispose();
                return;
            }
            _atual = t;
            _cancelar = cancelar;
        }
        var token = cancelar.Token;
        var nome = _setups.Memoria(t.IdMemoria)?.Nome ?? "memória";
        string? falha = null;
        try
        {
            var medidores = new List<(string Impressao, MedidorFaixa Medidor)>();
            foreach (var c in t.Cadeias)
            {
                // A fotografia só pede cadeias que se montam; uma que não se monte agora fica de fora (o teto dela dá o motivo).
                if (MedidorFaixa.Montar(c.Ler, t.NivelBluetooth).Medidor is { } medidor)
                    medidores.Add((c.Impressao, medidor));
            }
            if (medidores.Count > 0)
            {
                await _decodificador.LerAsync(t.Faixa.Uri, (bloco, canais, taxa) =>
                {
                    token.ThrowIfCancellationRequested();
                    foreach (var (_, m) in medidores)
                        m.Processar(bloco, canais, taxa);
                }, token);
                token.ThrowIfCancellationRequested();
                if (medidores[0].Medidor.Quadros == 0)
                    throw new ErroDecodificacao(MsgSemAudio);
                var resultados = medidores.Select(m => (m.Impressao, Analise: m.Medidor.Resultado())).ToArray();
                bool cancelada;
                lock (_trava)
                {
                    cancelada = token.IsCancellationRequested || _descartada;
                    if (!cancelada)
                    {
                        foreach (var (impressao, analise) in resultados)
                            _guardadas[Chave(t.Faixa, impressao)] = new Guardada(analise, ++_ordem, t.Faixa.Uri, DuracaoMs(t.Faixa), impressao);
                        _versao++;
                    }
                }
                if (cancelada)
                {
                    _log.Evento($"análise da playlist da {nome}: cancelada; nada foi gravado");
                }
                else
                {
                    await GravarAsync();
                    _log.Evento($"análise da playlist da {nome}: uma faixa pronta");
                }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            _log.Evento($"análise da playlist da {nome}: cancelada; nada foi gravado");
        }
        catch (OperationCanceledException)
        {
            // Um cancelamento que não é o deste trabalho (o decodificador desistiu sozinho): é falha da faixa, não um laço.
            falha = MsgInterrompida;
        }
        catch (ErroDecodificacao e)
        {
            falha = e.Motivo;
        }
        catch (Exception e)
        {
            falha = MsgNaoAbriu;
            _log.Evento($"análise da playlist da {nome}: erro inesperado ({e.GetType().Name})");
        }
        if (falha is not null)
        {
            var guardou = false;
            lock (_trava)
            {
                if (!token.IsCancellationRequested && !_descartada)
                {
                    var anterior = _falhas.GetValueOrDefault(t.Faixa.Id);
                    var impressoes = new HashSet<string>(anterior?.Impressoes ?? [], StringComparer.Ordinal);
                    impressoes.UnionWith(t.Cadeias.Select(c => c.Impressao));
                    _falhas[t.Faixa.Id] = new Falha(falha, impressoes);
                    _versao++;
                    guardou = true;
                }
            }
            if (guardou)
                _log.Evento($"análise da playlist da {nome}: uma faixa não deu ({falha})");
        }
        lock (_trava)
        {
            _atual = null;
            _cancelar = null;
        }
        cancelar.Dispose();
        Notificar();
    }

    // ------------------------------------------------------------------ a impressão e o leitor

    /// <summary>
    /// A impressão da cadeia de uma memória com um preset no Setup: a versão da conta, os valores guardados da cadeia (sem o
    /// master), as 15 bandas do preset e os números físicos do Setup (<see cref="ImpressaoDoSetup"/>).
    /// </summary>
    private static string Impressao(MemoriaCelular memoria, Preset preset, string doSetup)
    {
        var texto = new StringBuilder(VersaoConta).Append('\n');
        foreach (var v in memoria.Regulagem.Where(v => CodigosDaMemoria.Contains(v.Codigo))
                     .OrderBy(v => v.Codigo, StringComparer.Ordinal).ThenBy(v => v.Indice))
            texto.Append(v.Codigo).Append(':').Append(v.Indice).Append('=').Append(v.NoFio).Append(';');
        texto.Append("\npreset:");
        foreach (var v in preset.Valores.OrderBy(v => v.Codigo, StringComparer.Ordinal).ThenBy(v => v.Indice))
            texto.Append(v.Codigo).Append(':').Append(v.Indice).Append('=').Append(v.NoFio).Append(';');
        texto.Append("\nsetup:").Append(doSetup);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(texto.ToString())))[..32];
    }

    /// <summary>
    /// Os números físicos do Setup que a cadeia e os limites usam: por equipamento (pelo id), os dados da ficha do
    /// <see cref="LimiteFisico"/> (o valor; nos ganhos, também a origem, que é a calibração) e o nível do Bluetooth (valor e
    /// origem). Nomes, notas e fontes ficam de fora: mexer neles não invalida as análises.
    /// </summary>
    private static string ImpressaoDoSetup(Setup setup)
    {
        static string N(DadoTecnico? d) => d is null ? "-" : d.Valor.ToString(Fio);
        static string Com(DadoTecnico? d) => d is null ? "-" : $"{d.Valor.ToString(Fio)}/{d.Origem}";
        var texto = new StringBuilder();
        foreach (var e in setup.Equipamentos.OrderBy(e => e.Id, StringComparer.Ordinal))
        {
            var d = e.Dados;
            texto.Append(e.Id).Append('|')
                .Append(N(d.PotenciaRms)).Append('|').Append(N(d.Impedancia)).Append('|')
                .Append(N(d.Re)).Append('|').Append(N(d.Fs)).Append('|').Append(N(d.Qes)).Append('|').Append(N(d.Qms)).Append('|')
                .Append(N(d.Vas)).Append('|').Append(N(d.Bl)).Append('|').Append(N(d.Sd)).Append('|').Append(N(d.Xmax)).Append('|')
                .Append(N(d.Le)).Append('|').Append(N(d.VolumeCaixa)).Append('|').Append(N(d.Fb)).Append('|')
                .Append(d.TipoCaixa?.Valor ?? "-").Append('|')
                .Append(N(d.TensaoCeifamentoCanal)).Append('|').Append(N(d.TensaoCeifamentoPonte)).Append('|')
                .Append(Com(d.Ganho)).Append('|').Append(Com(d.GanhoPonte)).Append(';');
        }
        texto.Append("bt:").Append(Com(setup.NivelBluetooth));
        return texto.ToString();
    }

    /// <summary>A cadeia da memória com o preset: a regulagem guardada e as 15 bandas.</summary>
    private static Func<Endereco, Valor?> Leitor(MemoriaCelular memoria, Preset preset)
    {
        var valores = new Dictionary<Endereco, Valor>();
        foreach (var v in memoria.Regulagem.Concat(preset.Valores))
        {
            if (Catalogo.ParametroDe(v.Codigo)?.Decodificar(v.NoFio).Valor is { } valor)
                valores[new Endereco(v.Codigo, v.Indice)] = valor;
        }
        return e => valores.GetValueOrDefault(e);
    }

    private static long DuracaoMs(Faixa f) => (long)Math.Round(f.Duracao.TotalMilliseconds);

    private static string ChaveFaixa(Faixa f) => ChaveFaixa(f.Uri, DuracaoMs(f));

    private static string ChaveFaixa(string uri, long duracaoMs) => $"{uri}\n{duracaoMs}";

    private static string Chave(Faixa f, string impressao) => Chave(f.Uri, DuracaoMs(f), impressao);

    private static string Chave(string uri, long duracaoMs, string impressao) => $"{ChaveFaixa(uri, duracaoMs)}\n{impressao}";

    // ------------------------------------------------------------------ analises.json

    private async Task CarregarAsync()
    {
        try
        {
            if (await Gravacao.LerAsync(_repositorio) is { } json && DeJson(json) is { } lidas)
            {
                lock (_trava)
                {
                    foreach (var g in lidas)
                        _guardadas[Chave(g.Uri, g.DuracaoMs, g.Impressao)] = g with { Ordem = ++_ordem };
                    _versao++;
                }
            }
        }
        catch (Exception e)
        {
            _log.Evento($"analises.json ilegível ({e.GetType().Name}); começa sem análises");
        }
    }

    /// <summary>
    /// Grava as análises das faixas que ainda estão em alguma playlist, com uma impressão que ainda vale para alguma memória
    /// e preset; de cada faixa sem nenhuma análise atual fica a mais nova (a prova de que ela já foi analisada: na
    /// reanálise ela segue tocando). As mais novas, até o máximo.
    /// </summary>
    private async Task GravarAsync()
    {
        var foto = Fotografar();
        var atuais = foto.Memorias.Values.SelectMany(m => m.Impressoes.Values).ToHashSet(StringComparer.Ordinal);
        var uris = foto.Playlists.PorMemoria.Values.SelectMany(l => l).Select(f => f.Uri).ToHashSet(StringComparer.Ordinal);
        string json;
        lock (_trava)
        {
            var sair = new HashSet<string>(StringComparer.Ordinal);
            foreach (var faixa in _guardadas.GroupBy(g => g.Value.Faixa))
            {
                var todas = faixa.ToArray();
                if (!uris.Contains(todas[0].Value.Uri))
                {
                    sair.UnionWith(todas.Select(g => g.Key));
                    continue;
                }
                var velhas = todas.Where(g => !atuais.Contains(g.Value.Impressao)).ToArray();
                if (velhas.Length == todas.Length)
                    velhas = velhas.OrderByDescending(g => g.Value.Ordem).Skip(1).ToArray();
                sair.UnionWith(velhas.Select(g => g.Key));
            }
            sair.UnionWith(_guardadas.Where(g => !sair.Contains(g.Key)).OrderByDescending(g => g.Value.Ordem).Skip(MaximoGuardadas)
                .Select(g => g.Key));
            foreach (var chave in sair)
                _guardadas.Remove(chave);
            if (sair.Count > 0)
                _versao++;
            json = ParaJson(_guardadas.Values.OrderBy(g => g.Ordem).ToArray());
        }
        if (!await Gravacao.GravarAsync(_repositorio, json))
            _log.Evento("análise da playlist: não consegui guardar no celular");
    }

    /// <summary>
    /// O <c>analises.json</c>: por análise, a faixa (<c>uri</c>, <c>duracao</c> em ms), a <c>impressao</c> da cadeia, os
    /// <c>segundos</c> lidos, por canal o <c>pico</c> e a <c>rms</c> (dB do limitador) e os <c>trechos</c> ([início, fim] em s).
    /// </summary>
    private static string ParaJson(IReadOnlyList<Guardada> guardadas)
    {
        using var fluxo = new MemoryStream();
        using (var w = new Utf8JsonWriter(fluxo, new JsonWriterOptions { Indented = false }))
        {
            w.WriteStartObject();
            w.WriteNumber("formato", Formato);
            w.WriteStartArray("analises");
            foreach (var g in guardadas)
            {
                w.WriteStartObject();
                w.WriteString("uri", g.Uri);
                w.WriteNumber("duracao", g.DuracaoMs);
                w.WriteString("impressao", g.Impressao);
                w.WriteNumber("segundos", g.Analise.Duracao);
                w.WriteStartArray("canais");
                foreach (var c in g.Analise.Canais)
                {
                    w.WriteStartObject();
                    w.WriteNumber("canal", c.Canal);
                    w.WriteNumber("pico", c.Pico);
                    w.WriteNumber("rms", c.Rms);
                    w.WriteEndObject();
                }
                w.WriteEndArray();
                w.WriteStartArray("trechos");
                foreach (var t in g.Analise.Trechos)
                {
                    w.WriteStartArray();
                    w.WriteNumberValue(t.Inicio);
                    w.WriteNumberValue(t.Fim);
                    w.WriteEndArray();
                }
                w.WriteEndArray();
                w.WriteEndObject();
            }
            w.WriteEndArray();
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(fluxo.ToArray());
    }

    /// <summary>
    /// Lê o <c>analises.json</c>; fora do formato, null. Uma análise ilegível sai (sem os 4 canais, um de cada, de 0 a 3,
    /// com números finitos; ou com trechos e duração que não são números finitos); as outras ficam.
    /// </summary>
    internal static IReadOnlyList<(string Uri, long DuracaoMs, string Impressao, AnaliseFaixa Analise)>? LerJson(string json) =>
        DeJson(json)?.Select(g => (g.Uri, g.DuracaoMs, g.Impressao, g.Analise)).ToArray();

    private static IReadOnlyList<Guardada>? DeJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var raiz = doc.RootElement;
        if (raiz.ValueKind != JsonValueKind.Object || !raiz.TryGetProperty("formato", out var f) || !f.TryGetInt32(out var formato)
            || formato != Formato || !raiz.TryGetProperty("analises", out var analises) || analises.ValueKind != JsonValueKind.Array)
            return null;
        var lidas = new List<Guardada>();
        foreach (var a in analises.EnumerateArray())
        {
            try
            {
                var canais = a.GetProperty("canais").EnumerateArray()
                    .Select(c => new MedidaCanal(c.GetProperty("canal").GetInt32(), c.GetProperty("pico").GetDouble(), c.GetProperty("rms").GetDouble()))
                    .ToArray();
                var trechos = a.GetProperty("trechos").EnumerateArray()
                    .Select(t => new Trecho(t[0].GetDouble(), t[1].GetDouble()))
                    .ToArray();
                var segundos = a.GetProperty("segundos").GetDouble();
                var uri = a.GetProperty("uri").GetString();
                var impressao = a.GetProperty("impressao").GetString();
                var canaisCertos = canais.Length == MedidorFaixa.Canais
                                   && canais.Select(c => c.Canal).Order().SequenceEqual(Enumerable.Range(0, MedidorFaixa.Canais))
                                   && canais.All(c => double.IsFinite(c.Pico) && double.IsFinite(c.Rms));
                if (string.IsNullOrEmpty(uri) || string.IsNullOrEmpty(impressao) || !canaisCertos || !double.IsFinite(segundos)
                    || trechos.Any(t => !double.IsFinite(t.Inicio) || !double.IsFinite(t.Fim)))
                    continue;
                lidas.Add(new Guardada(new AnaliseFaixa(canais, trechos, segundos), 0, uri, a.GetProperty("duracao").GetInt64(), impressao));
            }
            catch (Exception e) when (e is KeyNotFoundException or InvalidOperationException or FormatException or IndexOutOfRangeException)
            {
                // Uma análise estragada sai; ela é refeita quando for pedida.
            }
        }
        return lidas;
    }

    // ------------------------------------------------------------------ apoio

    private void Notificar()
    {
        try
        {
            Mudou?.Invoke();
        }
        catch (Exception e)
        {
            _log.Evento($"falha no aviso da análise da playlist ({e.GetType().Name})");
        }
    }

    /// <summary>O fim do processo: a análise em curso é cancelada (nada dela é gravado) e a fila para de vez.</summary>
    public void Dispose()
    {
        lock (_trava)
        {
            if (_descartada)
                return;
            _descartada = true;
            _cancelar?.Cancel();
        }
        _setups.Mudou -= Reavaliar;
        _presets.Mudou -= Reavaliar;
        _playlists.Mudou -= Reavaliar;
    }
}
