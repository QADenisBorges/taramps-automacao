using System.Globalization;
using BgsControle.Nucleo.Escrita;

namespace BgsControle.Nucleo.Setups;

/// <summary>A camada dona de um parâmetro (EXPERIENCE, "Três camadas do som").</summary>
public enum Camada
{
    /// <summary>A instalação e a sala: vai para o Setup.</summary>
    Calibracao,

    /// <summary>O caráter do som: vai para a memória do celular.</summary>
    Carater,

    /// <summary>O EQ de 15 bandas (vai para o preset, CAP-5) e o preset de EQ do aparelho (220, que não entra em preset nem em memória).</summary>
    Bandas,

    /// <summary>O mute e os extras: valem na hora; o Salvar não guarda.</summary>
    Fora,
}

/// <summary>
/// O equilíbrio L/R de um Setup (CAP-16): um ganho e um limiter para a saída 1 (JBL L) e para a
/// saída 2 (JBL R), em dB, de −12,0 a +12,0, passo 0,1. No aparelho vale a soma com a memória.
/// </summary>
public sealed record Equilibrio(decimal Ganho1, decimal Limiter1, decimal Ganho2, decimal Limiter2)
{
    public static Equilibrio Zero { get; } = new(0m, 0m, 0m, 0m);

    /// <summary>O valor da saída (0 = saída 1, 1 = saída 2) no ganho ou no limiter.</summary>
    public decimal Valor(int saida, bool limiter) => (saida, limiter) switch
    {
        (0, false) => Ganho1,
        (0, true) => Limiter1,
        (1, false) => Ganho2,
        (1, true) => Limiter2,
        _ => throw new ArgumentOutOfRangeException(nameof(saida), "O equilíbrio é só das saídas 1 e 2."),
    };

    /// <summary>Uma cópia com um valor trocado.</summary>
    public Equilibrio Com(int saida, bool limiter, decimal valor) => (saida, limiter) switch
    {
        (0, false) => this with { Ganho1 = valor },
        (0, true) => this with { Limiter1 = valor },
        (1, false) => this with { Ganho2 = valor },
        (1, true) => this with { Limiter2 = valor },
        _ => throw new ArgumentOutOfRangeException(nameof(saida), "O equilíbrio é só das saídas 1 e 2."),
    };
}

/// <summary>
/// A camada de cada parâmetro do Marco 1 (decisão da história 8c, pela tabela "Três camadas do
/// som") e a soma e a subtração do equilíbrio L/R. Uma regra só para a conferência ao conectar, o
/// aplicar e o Salvar.
/// <list type="bullet">
/// <item><b>Calibração:</b> 010 rota, 060 fase, 040/041/042/043 cortes, 050 atraso, 030/031/032 EQ de
/// entrada (mais o link 1–2 e 3–4 e o equilíbrio, que são só do app).</item>
/// <item><b>Caráter:</b> 080 MASTER, 081 ganho, 070–073 limiter, 090–092 EQ de saída.</item>
/// <item><b>Bandas:</b> 020 (15 bandas) e 220 (preset de EQ do aparelho).</item>
/// <item><b>Fora:</b> 100 mute e os extras.</item>
/// </list>
/// </summary>
public static class Camadas
{
    public const decimal EquilibrioMinimo = -12.0m;
    public const decimal EquilibrioMaximo = 12.0m;

    /// <summary>O limiter em OFF no fio (MAPA §4).</summary>
    public const decimal LimiterOff = 0.5m;

    public const string CodigoMaster = "080";
    public const string CodigoGanho = "081";
    public const string CodigoMute = "100";
    public const string CodigoLimiter = "071";

    private static readonly CultureInfo Fio = CultureInfo.InvariantCulture;

    private static readonly HashSet<string> CodigosCalibracao = ["010", "060", "040", "041", "042", "043", "050", "030", "031", "032"];
    private static readonly HashSet<string> CodigosCarater = ["080", "081", "070", "071", "072", "073", "090", "091", "092"];
    private static readonly HashSet<string> CodigosBandas = ["020", "220"];

    /// <summary>A camada do código (um extra ou o mute: <see cref="Camada.Fora"/>).</summary>
    public static Camada De(string codigo) =>
        CodigosCalibracao.Contains(codigo) ? Camada.Calibracao
        : CodigosCarater.Contains(codigo) ? Camada.Carater
        : CodigosBandas.Contains(codigo) ? Camada.Bandas
        : Camada.Fora;

    /// <summary>As consultas do Marco 1 da camada, na ordem da leitura.</summary>
    public static IReadOnlyList<Consulta> Consultas(Camada camada) => camada switch
    {
        Camada.Calibracao => ConsultasCalibracao,
        Camada.Carater => ConsultasCarater,
        Camada.Bandas => ConsultasBandas,
        _ => ConsultasFora,
    };

    private static readonly IReadOnlyList<Consulta> ConsultasCalibracao = Filtrar(Camada.Calibracao);
    private static readonly IReadOnlyList<Consulta> ConsultasCarater = Filtrar(Camada.Carater);
    private static readonly IReadOnlyList<Consulta> ConsultasBandas = Filtrar(Camada.Bandas);
    private static readonly IReadOnlyList<Consulta> ConsultasFora = Filtrar(Camada.Fora);

    private static Consulta[] Filtrar(Camada camada) =>
        Catalogo.ConsultasMarco1.Where(c => De(c.Parametro.Codigo) == camada).ToArray();

    // ------------------------------------------------------------------ equilíbrio

    /// <summary>O ganho (081) e o limiter (071) das saídas 1 e 2 levam o equilíbrio.</summary>
    public static bool TemEquilibrio(string codigo, int? indice) =>
        codigo is CodigoGanho or CodigoLimiter && indice is 0 or 1;

    /// <summary>
    /// O valor final no aparelho: a parte da memória mais o equilíbrio, preso à faixa do MAPA
    /// (ganho −45,0 a +15,0; limiter −48,0 a 0,0). O limiter em OFF (<c>0.5</c>) fica OFF, sem soma.
    /// Fora do ganho e do limiter das saídas 1 e 2, o valor passa como está.
    /// </summary>
    public static string Somar(string codigo, int? indice, string noFio, Equilibrio equilibrio)
    {
        if (!TemEquilibrio(codigo, indice) || Numero(noFio) is not { } v)
            return noFio;
        var limiter = codigo == CodigoLimiter;
        if (limiter && v == LimiterOff)
            return noFio;
        var p = Catalogo.ParametroDe(codigo)!;
        var soma = v + equilibrio.Valor(indice!.Value, limiter);
        soma = Math.Clamp(soma, p.Minimo!.Value, p.Maximo!.Value);
        return Db(soma);
    }

    /// <summary>
    /// A parte da memória: o valor final lido menos o equilíbrio. O limiter em OFF fica OFF. Uma
    /// parte do limiter que daria exatamente <c>+0.5</c> (que no fio quer dizer OFF) fica em
    /// <c>+0.4</c>: o caso só acontece com o limiter final 0,5 dB acima do equilíbrio.
    /// </summary>
    public static string Subtrair(string codigo, int? indice, string noFio, Equilibrio equilibrio)
    {
        if (!TemEquilibrio(codigo, indice) || Numero(noFio) is not { } v)
            return noFio;
        var limiter = codigo == CodigoLimiter;
        if (limiter && v == LimiterOff)
            return noFio;
        var parte = v - equilibrio.Valor(indice!.Value, limiter);
        if (limiter && parte == LimiterOff)
            parte = 0.4m;
        return Db(parte);
    }

    /// <summary>Os valores finais do caráter: cada parte da memória somada ao equilíbrio.</summary>
    public static IReadOnlyList<ValorCopiado> Compor(IEnumerable<ValorCopiado> carater, Equilibrio equilibrio) =>
        carater.Select(v => v with { NoFio = Somar(v.Codigo, v.Indice, v.NoFio, equilibrio) }).ToArray();

    /// <summary>As partes da memória: cada valor final menos o equilíbrio.</summary>
    public static IReadOnlyList<ValorCopiado> Separar(IEnumerable<ValorCopiado> finais, Equilibrio equilibrio) =>
        finais.Select(v => v with { NoFio = Subtrair(v.Codigo, v.Indice, v.NoFio, equilibrio) }).ToArray();

    /// <summary>
    /// Os valores do espelho na camada, na ordem da leitura, e quantas consultas da camada não têm
    /// valor (uma leitura que falhou).
    /// </summary>
    public static (IReadOnlyList<ValorCopiado> Valores, int Faltam) Tirar(IReadOnlyDictionary<Endereco, Valor> espelho, Camada camada)
    {
        var valores = new List<ValorCopiado>();
        var faltam = 0;
        foreach (var c in Consultas(camada))
        {
            if (espelho.TryGetValue(new Endereco(c.Parametro.Codigo, c.Indice), out var v))
                valores.Add(new ValorCopiado(c.Parametro.Codigo, c.Indice, v.NoFio));
            else
                faltam++;
        }
        return (valores, faltam);
    }

    /// <summary>
    /// Os endereços do alvo cujo valor difere do espelho (ou falta nele), na ordem do alvo. Um valor
    /// do alvo que não se decodifica conta como diferente.
    /// </summary>
    public static IReadOnlyList<ValorCopiado> Diferentes(IEnumerable<ValorCopiado> alvo, IReadOnlyDictionary<Endereco, Valor> espelho)
    {
        var saida = new List<ValorCopiado>();
        foreach (var v in alvo)
        {
            if (!Iguais(v, espelho))
                saida.Add(v);
        }
        return saida;
    }

    /// <summary>O valor guardado bate com o do espelho.</summary>
    public static bool Iguais(ValorCopiado guardado, IReadOnlyDictionary<Endereco, Valor> espelho)
    {
        if (!espelho.TryGetValue(new Endereco(guardado.Codigo, guardado.Indice), out var agora))
            return false;
        return Catalogo.ParametroDe(guardado.Codigo)?.Decodificar(guardado.NoFio).Valor is { } valor && CopiaAntes.Iguais(agora, valor);
    }

    /// <summary>Dois textos no fio dizem o mesmo para o parâmetro.</summary>
    public static bool Iguais(string codigo, string a, string b)
    {
        var p = Catalogo.ParametroDe(codigo);
        if (p?.Decodificar(a).Valor is not { } va || p.Decodificar(b).Valor is not { } vb)
            return string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
        return CopiaAntes.Iguais(va, vb);
    }

    // ------------------------------------------------------------------ textos da tela

    /// <summary>"Atraso da saída 3", "MASTER", "Ganho do EQ de entrada".</summary>
    public static string Rotulo(string codigo, int? indice)
    {
        var saida = indice is { } i ? $" da saída {i + 1}" : "";
        return codigo switch
        {
            "010" => $"Rota{saida}",
            "060" => $"Fase{saida}",
            "040" => $"Passa-alta{saida}",
            "041" => $"Passa-baixa{saida}",
            "042" => $"Tipo do passa-alta{saida}",
            "043" => $"Tipo do passa-baixa{saida}",
            "050" => $"Atraso{saida}",
            "030" => "Ganho do EQ de entrada",
            "031" => "Frequência do EQ de entrada",
            "032" => "Q do EQ de entrada",
            CodigoMaster => "MASTER",
            CodigoGanho => $"Ganho{saida}",
            "070" => $"Limitador auto{saida}",
            CodigoLimiter => $"Limitador{saida}",
            "072" => $"Ataque do limitador{saida}",
            "073" => $"Liberação do limitador{saida}",
            "090" => $"Frequência do EQ{saida}",
            "091" => $"Ganho do EQ{saida}",
            "092" => $"Q do EQ{saida}",
            CodigoMute => $"Mute{saida}",
            "020" => $"Banda {Catalogo.RotuloBanda(indice ?? 0)}",
            "220" => "Preset de EQ do aparelho",
            _ => Catalogo.ParametroDe(codigo)?.Nome ?? codigo,
        };
    }

    /// <summary>
    /// O valor para a tela, em português: vírgula, sinal de menos verdadeiro (−), a unidade ao
    /// lado ("80 cm", "−3,0 dB", "150 Hz", "LR24", "OFF"); "—" sem valor.
    /// </summary>
    public static string Exibir(string codigo, string? noFio)
    {
        if (noFio is null)
            return "—";
        var p = Catalogo.ParametroDe(codigo);
        if (p?.Decodificar(noFio).Valor is not { } v)
            return noFio;
        if (v.Rotulo is { } rotulo)
            return rotulo;
        if (p.Tipo == TipoValor.Booleano)
            return v.Exibicao;
        if (v.Numero is not { } n)
            return v.Exibicao;
        return p.Tipo switch
        {
            TipoValor.Decibel => $"{Numero(n, "0.0", sinal: true)} dB",
            TipoValor.Hertz => $"{Numero(n, "0", sinal: false)} Hz",
            TipoValor.Centimetro => $"{Numero(n, "0.#", sinal: false)} cm",
            TipoValor.Attack or TipoValor.Release => $"{Numero(n, "0.#", sinal: false)} ms",
            TipoValor.Q => Numero(n, "0.0", sinal: false),
            _ => Numero(n, "0.#", sinal: false),
        };
    }

    /// <summary>"−3,0 dB", "+2,5 dB", "0,0 dB": um valor do equilíbrio.</summary>
    public static string ExibirDb(decimal v) => $"{Numero(v, "0.0", sinal: true)} dB";

    private static string Numero(decimal n, string formato, bool sinal)
    {
        var texto = Math.Abs(n).ToString(formato, Fio).Replace('.', ',');
        if (n < 0)
            return "−" + texto;
        return sinal && n > 0 ? "+" + texto : texto;
    }

    /// <summary>O número no fio, ou null se não é número.</summary>
    internal static decimal? Numero(string noFio) =>
        decimal.TryParse(noFio.Trim(), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, Fio, out var v) ? v : null;

    /// <summary>Um dB como o aparelho devolve: com sinal e uma casa (<c>+7.0</c>, <c>-18.0</c>, <c>+0.0</c>).</summary>
    internal static string Db(decimal v) => (v >= 0 ? "+" : "-") + Math.Abs(v).ToString("0.0", Fio);
}
