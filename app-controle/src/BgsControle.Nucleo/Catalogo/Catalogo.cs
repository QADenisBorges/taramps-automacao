using BgsControle.Nucleo.Protocolo;

namespace BgsControle.Nucleo;

/// <summary>Uma escrita de ajuste de áudio reconhecida pelo catálogo: o parâmetro, o canal ou a banda e o valor no fio.</summary>
public sealed record EscritaAudio(Parametro Parametro, int? Indice, string NoFio);

/// <summary>
/// A fonte única dos parâmetros do PRO 2.4BT + AUDIO (MAPA §4): a trava pergunta a ele se um
/// quadro é uma leitura conhecida, o leitor monta a fila a partir dele, e as escritas futuras
/// usam o mesmo codificador.
/// </summary>
public static class Catalogo
{
    /// <summary>Frequências das 15 bandas do EQ gráfico, banda 0 a 14 (MAPA §4).</summary>
    public static IReadOnlyList<int> BandasHz { get; } =
        [25, 40, 63, 100, 160, 250, 400, 630, 1000, 1600, 2500, 4000, 6300, 10000, 16000];

    /// <summary>Filtros do crossover: 0 OFF, 1–7 LR, 8–15 BT (MAPA §4).</summary>
    public static IReadOnlyDictionary<int, string> Filtros { get; } = new Dictionary<int, string>
    {
        [0] = "OFF",
        [1] = "LR12", [2] = "LR18", [3] = "LR24", [4] = "LR30", [5] = "LR36", [6] = "LR42", [7] = "LR48",
        [8] = "BT6", [9] = "BT12", [10] = "BT18", [11] = "BT24", [12] = "BT30", [13] = "BT36", [14] = "BT42", [15] = "BT48",
    };

    /// <summary>Presets do EQ de 15 bandas: 0–11 e 255 Custom (MAPA §4).</summary>
    public static IReadOnlyDictionary<int, string> PresetsEq { get; } = new Dictionary<int, string>
    {
        [0] = "Flat", [1] = "Loudness", [2] = "Bass Boost", [3] = "Mid-Bass Boost", [4] = "Treble Boost",
        [5] = "Powerful", [6] = "Electronic", [7] = "Rock Style", [8] = "Hip-Hop Style", [9] = "Pop Music",
        [10] = "Vocal", [11] = "Competition", [255] = "Custom",
    };

    private static readonly IReadOnlyDictionary<int, string> Fases = new Dictionary<int, string>
    {
        [0] = "0°",
        [180] = "180°",
    };

    private static readonly IReadOnlyDictionary<int, string> VelocidadesSweep = new Dictionary<int, string>
    {
        [0] = "desligado", [1] = "lento", [2] = "médio", [3] = "rápido",
    };

    /// <summary>Os 22 parâmetros do Marco 1, na ordem da leitura: 5 globais + 16 × 4 canais + 15 bandas = 84.</summary>
    public static IReadOnlyList<Parametro> Marco1 { get; } =
    [
        Db("080", "master", Escopo.Global, -80, 0),
        Db("081", "ganho", Escopo.Canal, -45, 15),
        Bool("100", "mute", Escopo.Canal, "mutado", "com som"),
        new() { Codigo = "010", Nome = "roteamento", Escopo = Escopo.Canal, Tipo = TipoValor.Roteamento },
        new() { Codigo = "060", Nome = "fase", Escopo = Escopo.Canal, Tipo = TipoValor.Fase, Unidade = "graus", Opcoes = Fases },
        Hz("040", "HPF frequência", Escopo.Canal),
        Hz("041", "LPF frequência", Escopo.Canal),
        Indice("042", "HPF filtro", Escopo.Canal, Filtros),
        Indice("043", "LPF filtro", Escopo.Canal, Filtros),
        new() { Codigo = "050", Nome = "delay", Escopo = Escopo.Canal, Tipo = TipoValor.Centimetro, Unidade = "cm", Minimo = 0, Maximo = 275 },
        Bool("070", "limiter auto", Escopo.Canal, "auto", "manual"),
        new()
        {
            Codigo = "071", Nome = "limiter threshold", Escopo = Escopo.Canal, Tipo = TipoValor.Decibel, Unidade = "dB",
            Minimo = -48, Maximo = 0, Especiais = new Dictionary<decimal, string> { [0.5m] = "OFF" },
        },
        new()
        {
            // No modo Auto o app manda 0.01 ou 0.05, com 2 casas (MAPA §8, lacuna 6).
            Codigo = "072", Nome = "limiter attack", Escopo = Escopo.Canal, Tipo = TipoValor.Attack, Unidade = "ms",
            Minimo = 0.1m, Maximo = 100, Especiais = new Dictionary<decimal, string> { [0.01m] = "auto", [0.05m] = "auto" },
        },
        new() { Codigo = "073", Nome = "limiter release", Escopo = Escopo.Canal, Tipo = TipoValor.Release, Unidade = "ms", Minimo = 1, Maximo = 1600 },
        Hz("090", "EQ saída frequência", Escopo.Canal),
        Db("091", "EQ saída ganho", Escopo.Canal, -12, 12),
        Q("092", "EQ saída Q", Escopo.Canal, 0.1m, 10),
        Db("030", "EQ entrada ganho", Escopo.Global, -12, 12),
        Hz("031", "EQ entrada frequência", Escopo.Global),
        Q("032", "EQ entrada Q", Escopo.Global, 0.4m, 10),
        Db("020", "EQ gráfico", Escopo.Banda, -12, 12),
        Indice("220", "preset de EQ", Escopo.Global, PresetsEq),
    ];

    /// <summary>
    /// Fora do Marco 1 (MAPA §4): voltímetro, clip, gerador, nomes das memórias, Audio
    /// Classic e remote out. Perguntados uma vez em cada leitura; o texto cru fica registrado,
    /// seja o que vier. As faixas do gerador que o MAPA não dá (frequências, ganho do sweep)
    /// seguem as do resto do aparelho.
    /// </summary>
    public static IReadOnlyList<Parametro> Extras { get; } =
    [
        Extra(new() { Codigo = "005", Nome = "voltímetro", Escopo = Escopo.Global, Tipo = TipoValor.Decivolt, Unidade = "V × 10", SomenteLeitura = true }),
        Extra(Bool("111", "clip da entrada", Escopo.Global, "clip", "sem clip") with { SomenteLeitura = true }),
        Extra(new() { Codigo = "150", Nome = "clip das saídas", Escopo = Escopo.Global, Tipo = TipoValor.BitsPorSaida, SomenteLeitura = true }),
        Extra(Db("200", "gerador ganho", Escopo.Global, -60, 0)),
        Extra(Hz("201", "gerador frequência", Escopo.Global)),
        Extra(Bool("202", "gerador ligado", Escopo.Global, "ligado", "desligado")),
        Extra(Hz("210", "sweep frequência inicial", Escopo.Global)),
        Extra(Hz("211", "sweep frequência final", Escopo.Global)),
        Extra(Db("212", "sweep ganho", Escopo.Global, -60, 0)),
        Extra(Indice("213", "sweep velocidade", Escopo.Global, VelocidadesSweep)),
        Extra(Nome("231", "nome da memória 1")),
        Extra(Nome("232", "nome da memória 2")),
        Extra(Nome("233", "nome da memória 3")),
        Extra(Bool("251", "Audio Classic", Escopo.Global, "ligado", "desligado")),
        Extra(Bool("252", "remote out", Escopo.Global, "ligado", "desligado")),
    ];

    public static IReadOnlyList<Parametro> Todos { get; } = [.. Marco1, .. Extras];

    /// <summary>As 84 consultas do Marco 1, na ordem da leitura.</summary>
    public static IReadOnlyList<Consulta> ConsultasMarco1 { get; } = Marco1.SelectMany(p => p.Consultas()).ToArray();

    public static IReadOnlyList<Consulta> ConsultasExtras { get; } = Extras.SelectMany(p => p.Consultas()).ToArray();

    /// <summary>A fila inteira: o Marco 1 primeiro, depois os extras.</summary>
    public static IReadOnlyList<Consulta> Consultas { get; } = [.. ConsultasMarco1, .. ConsultasExtras];

    public static int TotalMarco1 => ConsultasMarco1.Count;
    public static int TotalExtras => ConsultasExtras.Count;

    private static readonly Dictionary<string, Parametro> PorCodigo = Todos.ToDictionary(p => p.Codigo);
    private static readonly Dictionary<string, Consulta> PorTexto = Consultas.ToDictionary(c => c.Texto, StringComparer.Ordinal);

    public static Parametro? ParametroDe(string codigo) => PorCodigo.GetValueOrDefault(codigo);

    /// <summary>
    /// O quadro é exatamente uma consulta de leitura do catálogo, no escopo certo
    /// (<c>#080#</c> sim; <c>#004#</c>, <c>#080#0#</c>, <c>#081#</c>, <c>#081#4#</c>, <c>020#15</c> não).
    /// </summary>
    public static bool EhLeitura(string quadro) => PorTexto.ContainsKey(quadro);

    public static Consulta? ConsultaDe(string quadro) => PorTexto.GetValueOrDefault(quadro);

    private static readonly Dictionary<string, Parametro> Marco1PorCodigo = Marco1.ToDictionary(p => p.Codigo);

    /// <summary>
    /// Se o quadro é exatamente o texto que <see cref="Parametro.CodificarEscrita(int?, decimal)"/>
    /// (ou a versão de texto, para o roteamento) produz para um parâmetro do Marco 1, devolve o
    /// parâmetro, o índice e o valor no fio; senão null. É a regra da trava para os ajustes de
    /// áudio (MAPA §4): <c>*081*0*-3.0*</c> sim; <c>*081*0*-3*</c>, <c>*240*0*</c>, <c>*202*1*</c> não.
    /// </summary>
    public static EscritaAudio? EscritaDeAudio(string quadro)
    {
        var q = Quadros.Analisar(quadro);
        if (q is null || !Marco1PorCodigo.TryGetValue(q.Codigo, out var p))
            return null;
        var segmentos = p.Escopo == Escopo.Global ? 1 : 2;
        if (q.Segmentos.Count != segmentos)
            return null;
        int? indice = null;
        if (segmentos == 2)
        {
            if (!int.TryParse(q.Segmentos[0], System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture, out var i))
                return null;
            indice = i;
        }
        var valor = q.Segmentos[^1];
        try
        {
            string codificado;
            if (p.Tipo == TipoValor.Roteamento)
            {
                codificado = p.CodificarEscrita(indice, valor);
            }
            else
            {
                if (!decimal.TryParse(valor, System.Globalization.NumberStyles.AllowLeadingSign | System.Globalization.NumberStyles.AllowDecimalPoint,
                        System.Globalization.CultureInfo.InvariantCulture, out var numero))
                    return null;
                codificado = p.CodificarEscrita(indice, numero);
            }
            return codificado == quadro ? new EscritaAudio(p, indice, valor) : null;
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }

    public static string RotuloBanda(int banda)
    {
        var hz = BandasHz[banda];
        return hz >= 1000 ? $"{(hz / 1000m).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',')} kHz" : $"{hz} Hz";
    }

    /// <summary>
    /// Uma resposta a uma consulta do catálogo que chegou sem <c>\r\n</c> já está inteira?
    /// Nunca para um extra (o número de segmentos não é conhecido). Para o Marco 1, sim
    /// quando termina num separador e tem todos os segmentos do escopo (<c>#080#+0.0#</c>,
    /// <c>#081#0#+10.0#</c>), ou quando é uma banda com o número inteiro (<c>020#3#-1.0</c>).
    /// O eco <c>#081#0#</c> ainda não está: o valor pode estar a caminho.
    /// </summary>
    public static bool RespostaInteira(string texto)
    {
        var q = Quadros.Analisar(texto);
        // Um extra pode ter vários segmentos (#232#0#MUSICA#): só o \r\n diz que acabou.
        if (q is null || ParametroDe(q.Codigo) is not { } p || p.Extra)
            return false;
        if (p.Escopo == Escopo.Banda && !texto.TrimStart().StartsWith('#'))
            return q.Segmentos.Count == 2 && BandaInteira(q.Segmentos[1]);
        if (!Quadros.TerminaEmSeparador(texto))
            return false;
        return q.Segmentos.Count >= (p.Escopo == Escopo.Global ? 1 : 2);
    }

    private static bool BandaInteira(string valor)
    {
        var v = valor.Trim();
        var ponto = v.IndexOf('.');
        return ponto > 0 && ponto == v.Length - 2 && char.IsAsciiDigit(v[^1]);
    }

    // ------------------------------------------------------------------ construtores

    private static Parametro Db(string codigo, string nome, Escopo escopo, decimal min, decimal max) =>
        new() { Codigo = codigo, Nome = nome, Escopo = escopo, Tipo = TipoValor.Decibel, Unidade = "dB", Minimo = min, Maximo = max };

    private static Parametro Hz(string codigo, string nome, Escopo escopo) =>
        new() { Codigo = codigo, Nome = nome, Escopo = escopo, Tipo = TipoValor.Hertz, Unidade = "Hz", Minimo = 10, Maximo = 22000 };

    private static Parametro Q(string codigo, string nome, Escopo escopo, decimal min, decimal max) =>
        new() { Codigo = codigo, Nome = nome, Escopo = escopo, Tipo = TipoValor.Q, Minimo = min, Maximo = max };

    private static Parametro Indice(string codigo, string nome, Escopo escopo, IReadOnlyDictionary<int, string> opcoes) =>
        new() { Codigo = codigo, Nome = nome, Escopo = escopo, Tipo = TipoValor.Indice, Opcoes = opcoes };

    private static Parametro Bool(string codigo, string nome, Escopo escopo, string ligado, string desligado) =>
        new() { Codigo = codigo, Nome = nome, Escopo = escopo, Tipo = TipoValor.Booleano, RotuloLigado = ligado, RotuloDesligado = desligado };

    private static Parametro Nome(string codigo, string nome) =>
        new() { Codigo = codigo, Nome = nome, Escopo = Escopo.Global, Tipo = TipoValor.Nome };

    private static Parametro Extra(Parametro p) => p with { Extra = true };
}
