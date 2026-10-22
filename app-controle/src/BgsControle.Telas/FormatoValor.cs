using System.Globalization;
using BgsControle.Nucleo;

namespace BgsControle.Telas;

/// <summary>
/// Números para a tela, em português: vírgula decimal, sinal de menos verdadeiro (−), + nos dB
/// positivos e a unidade ao lado (EXPERIENCE, Voice and Tone).
/// </summary>
public static class FormatoValor
{
    public const char Menos = '−';

    private static readonly CultureInfo Fio = CultureInfo.InvariantCulture;

    /// <summary>"−3,0", "+15,0", "2119": sem a unidade.</summary>
    public static string Numero(Parametro p, decimal v)
    {
        var texto = Math.Abs(v).ToString(p.Casas == 0 ? "0" : "0.0", Fio).Replace('.', ',');
        if (v < 0)
            return Menos + texto;
        if (v > 0 && p.Tipo == TipoValor.Decibel)
            return "+" + texto;
        return texto;
    }

    /// <summary>"−3,0 dB", "2119 Hz", "0,7" (o Q não tem unidade).</summary>
    public static string ComUnidade(Parametro p, decimal v) =>
        string.IsNullOrEmpty(Unidade(p)) ? Numero(p, v) : $"{Numero(p, v)} {Unidade(p)}";

    /// <summary>"−45,0 a +15,0 dB"; com o limitador, "−48,0 a 0,0 dB ou OFF". Vazio para quem não tem faixa.</summary>
    public static string Faixa(Parametro p)
    {
        if (p.Minimo is not { } min || p.Maximo is not { } max)
            return "";
        var faixa = string.IsNullOrEmpty(Unidade(p))
            ? $"{Numero(p, min)} a {Numero(p, max)}"
            : $"{Numero(p, min)} a {Numero(p, max)} {Unidade(p)}";
        return TemOff(p) ? $"{faixa} ou OFF" : faixa;
    }

    /// <summary>O limitador: <c>0.5</c> no fio = OFF.</summary>
    public static bool TemOff(Parametro p) => p.Especiais?.Values.Contains("OFF") == true;

    public static decimal? ValorOff(Parametro p) =>
        p.Especiais?.FirstOrDefault(e => e.Value == "OFF") is { Value: "OFF" } e ? e.Key : null;

    private static string Unidade(Parametro p) => p.Tipo switch
    {
        TipoValor.Q => "",
        _ => p.Unidade ?? "",
    };

    /// <summary>
    /// Lê o que foi escrito no campo: vírgula ou ponto, − ou -, + opcional. Devolve null se
    /// não é um número.
    /// </summary>
    public static decimal? Ler(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return null;
        var t = texto.Trim().Replace(Menos, '-').Replace(',', '.').Replace(" ", "");
        return decimal.TryParse(t, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, Fio, out var v) ? v : null;
    }
}
