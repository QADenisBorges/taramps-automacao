using System.Globalization;
using BgsControle.Nucleo.Escrita;

namespace BgsControle.Nucleo.Setups;

/// <summary>A camada dona de um parâmetro (EXPERIENCE, "Três camadas do som"; forja de 29/10/2026).</summary>
public enum Camada
{
    /// <summary>Toda a regulagem, com o valor final: vai para a memória do celular.</summary>
    Regulagem,

    /// <summary>O EQ de 15 bandas (vai para o preset, CAP-5) e o preset de EQ do aparelho (220, que não entra em preset nem em memória).</summary>
    Bandas,

    /// <summary>O mute e os extras: valem na hora; o Salvar não guarda.</summary>
    Fora,
}

/// <summary>
/// A camada de cada parâmetro do Marco 1 (decisão do Denis de 29/10/2026: o Setup é só o físico e a
/// memória guarda toda a regulagem, com o valor final). Uma regra só para a conferência ao conectar, o
/// aplicar e o Salvar.
/// <list type="bullet">
/// <item><b>Regulagem:</b> 010 rota, 060 fase, 040/041/042/043 cortes, 050 atraso, 030/031/032 EQ de
/// entrada, 080 MASTER, 081 ganho, 070–073 limiter, 090–092 EQ de saída.</item>
/// <item><b>Bandas:</b> 020 (15 bandas) e 220 (preset de EQ do aparelho).</item>
/// <item><b>Fora:</b> 100 mute e os extras.</item>
/// </list>
/// Na conferência ao conectar, só a rota, a fase, os cortes e o atraso mutam (<see cref="MutaNaConferencia"/>):
/// errados, eles podem forçar o tweeter ou indicar outra instalação; o resto diferente quase sempre é
/// um ajuste que não foi salvo (decisão D3 do Denis, 29/10/2026).
/// </summary>
public static class Camadas
{
    public const string CodigoMaster = "080";
    public const string CodigoGanho = "081";
    public const string CodigoMute = "100";
    public const string CodigoLimiter = "071";

    private static readonly CultureInfo Fio = CultureInfo.InvariantCulture;

    private static readonly HashSet<string> CodigosRegulagem =
        ["010", "060", "040", "041", "042", "043", "050", "030", "031", "032", "080", "081", "070", "071", "072", "073", "090", "091", "092"];
    private static readonly HashSet<string> CodigosQueMutam = ["010", "050", "060", "040", "041", "042", "043"];
    private static readonly HashSet<string> CodigosBandas = ["020", "220"];

    /// <summary>A camada do código (um extra ou o mute: <see cref="Camada.Fora"/>).</summary>
    public static Camada De(string codigo) =>
        CodigosRegulagem.Contains(codigo) ? Camada.Regulagem
        : CodigosBandas.Contains(codigo) ? Camada.Bandas
        : Camada.Fora;

    /// <summary>
    /// Este código, diferente da memória ativa na conferência ao conectar, muta o som e pede a escolha:
    /// a rota (010), o atraso (050), a fase (060) e os cortes (040 a 043).
    /// </summary>
    public static bool MutaNaConferencia(string codigo) => CodigosQueMutam.Contains(codigo);

    /// <summary>As consultas do Marco 1 da camada, na ordem da leitura.</summary>
    public static IReadOnlyList<Consulta> Consultas(Camada camada) => camada switch
    {
        Camada.Regulagem => ConsultasRegulagem,
        Camada.Bandas => ConsultasBandas,
        _ => ConsultasFora,
    };

    private static readonly IReadOnlyList<Consulta> ConsultasRegulagem = Filtrar(Camada.Regulagem);
    private static readonly IReadOnlyList<Consulta> ConsultasBandas = Filtrar(Camada.Bandas);
    private static readonly IReadOnlyList<Consulta> ConsultasFora = Filtrar(Camada.Fora);

    private static Consulta[] Filtrar(Camada camada) =>
        Catalogo.ConsultasMarco1.Where(c => De(c.Parametro.Codigo) == camada).ToArray();

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
