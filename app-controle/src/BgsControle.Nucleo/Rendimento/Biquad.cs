using System.Numerics;

namespace BgsControle.Nucleo.Rendimento;

/// <summary>
/// Um filtro de segunda ordem (ou de primeira, com b2 = a2 = 0) no tempo, por amostra, em <c>double</c> (história 13d):
/// a forma direta II transposta, com a0 = 1. As receitas são as do RBJ ("Audio EQ Cookbook"), pela transformada
/// bilinear com a frequência pré-distorcida, então o módulo em f0 (ou no corte) é exato:
/// <list type="bullet">
/// <item>o sino ("peaking") com o ganho, a frequência e o Q (<see cref="Sino"/>), o mesmo
/// (s² + s·a/Q + 1)/(s² + s/(a·Q) + 1) do <see cref="PiorCaso.Sino"/>;</item>
/// <item>o Butterworth de ordem n em seções de segunda ordem com Q = 1/(2·sen((2k−1)·π/(2n))), e uma seção de primeira
/// ordem antes quando n é ímpar (<see cref="Butterworth"/>);</item>
/// <item>o Linkwitz-Riley de ordem n como dois Butterworth em cascata, de ordens ⌈n/2⌉ e ⌊n/2⌋ (<see cref="Corte"/>): nas
/// ordens pares, os dois iguais (|LRn| = |BT(n/2)|², exato); nas ímpares (LR18, LR30, LR42), a mesma queda de −6 dB no
/// corte e a mesma inclinação, com até ~0,3 dB de diferença do módulo ideal 1/(1+(fc/f)^n) perto do corte (o
/// ideal não se realiza com um filtro de verdade).</item>
/// </list>
/// Um corte ou um sino acima de 0,49 × a taxa fica de fora (não há como filtrar acima de Nyquist).
/// </summary>
public sealed class Biquad
{
    /// <summary>A maior frequência que um filtro aceita, em fração da taxa.</summary>
    public const double FracaoMaxima = 0.49;

    private readonly double _b0;
    private readonly double _b1;
    private readonly double _b2;
    private readonly double _a1;
    private readonly double _a2;
    private double _z1;
    private double _z2;

    /// <summary>Os coeficientes já divididos por a0.</summary>
    public Biquad(double b0, double b1, double b2, double a1, double a2)
    {
        _b0 = b0;
        _b1 = b1;
        _b2 = b2;
        _a1 = a1;
        _a2 = a2;
    }

    /// <summary>Uma amostra: entra x, sai y.</summary>
    public double Processar(double x)
    {
        var y = _b0 * x + _z1;
        _z1 = _b1 * x - _a1 * y + _z2;
        _z2 = _b2 * x - _a2 * y;
        return y;
    }

    /// <summary>|H(e^{jω})| na frequência <paramref name="f"/>, com ω = 2π·f/taxa.</summary>
    public double Modulo(double f, double taxa)
    {
        var w = 2 * Math.PI * f / taxa;
        var z1 = Complex.FromPolarCoordinates(1, -w);
        var z2 = z1 * z1;
        var num = _b0 + _b1 * z1 + _b2 * z2;
        var den = 1 + _a1 * z1 + _a2 * z2;
        return (num / den).Magnitude;
    }

    /// <summary>O módulo de uma cascata, em dB.</summary>
    public static double ModuloDb(IEnumerable<Biquad> cascata, double f, double taxa) =>
        20 * Math.Log10(cascata.Aggregate(1.0, (m, b) => m * b.Modulo(f, taxa)));

    // ------------------------------------------------------------------ as receitas

    /// <summary>O sino RBJ: o ganho em dB em <paramref name="f0"/>, com o Q. Null com o ganho em 0 ou acima de Nyquist.</summary>
    public static Biquad? Sino(double taxa, double f0, double ganhoDb, double q)
    {
        if (ganhoDb == 0 || !Cabe(taxa, f0) || q <= 0)
            return null;
        var a = Math.Pow(10, ganhoDb / 40);
        var w0 = 2 * Math.PI * f0 / taxa;
        var cos = Math.Cos(w0);
        var alfa = Math.Sin(w0) / (2 * q);
        var a0 = 1 + alfa / a;
        return new Biquad((1 + alfa * a) / a0, -2 * cos / a0, (1 - alfa * a) / a0, -2 * cos / a0, (1 - alfa / a) / a0);
    }

    /// <summary>O passa-alta ou o passa-baixa de segunda ordem RBJ, com o Q.</summary>
    public static Biquad SegundaOrdem(bool passaAlta, double taxa, double f0, double q)
    {
        var w0 = 2 * Math.PI * f0 / taxa;
        var cos = Math.Cos(w0);
        var alfa = Math.Sin(w0) / (2 * q);
        var a0 = 1 + alfa;
        var a1 = -2 * cos / a0;
        var a2 = (1 - alfa) / a0;
        return passaAlta
            ? new Biquad((1 + cos) / 2 / a0, -(1 + cos) / a0, (1 + cos) / 2 / a0, a1, a2)
            : new Biquad((1 - cos) / 2 / a0, (1 - cos) / a0, (1 - cos) / 2 / a0, a1, a2);
    }

    /// <summary>O passa-alta ou o passa-baixa de primeira ordem, bilinear com o corte pré-distorcido.</summary>
    public static Biquad PrimeiraOrdem(bool passaAlta, double taxa, double fc)
    {
        var k = Math.Tan(Math.PI * fc / taxa);
        var a1 = (k - 1) / (k + 1);
        return passaAlta
            ? new Biquad(1 / (1 + k), -1 / (1 + k), 0, a1, 0)
            : new Biquad(k / (1 + k), k / (1 + k), 0, a1, 0);
    }

    /// <summary>
    /// O Butterworth de ordem <paramref name="ordem"/> (1 a 8) em cascata: a seção de primeira ordem primeiro quando a
    /// ordem é ímpar, depois as de segunda com Q = 1/(2·sen((2k−1)·π/(2n))). Vazio com a ordem 0 ou acima de Nyquist.
    /// </summary>
    public static IReadOnlyList<Biquad> Butterworth(bool passaAlta, int ordem, double fc, double taxa)
    {
        if (ordem <= 0 || !Cabe(taxa, fc))
            return [];
        var secoes = new List<Biquad>();
        if (ordem % 2 == 1)
            secoes.Add(PrimeiraOrdem(passaAlta, taxa, fc));
        for (var k = 1; k <= ordem / 2; k++)
        {
            var q = 1 / (2 * Math.Sin((2 * k - 1) * Math.PI / (2 * ordem)));
            secoes.Add(SegundaOrdem(passaAlta, taxa, fc, q));
        }
        return secoes;
    }

    /// <summary>
    /// Um corte do aparelho (o passa-alta ou o passa-baixa de uma saída) em cascata: BT de ordem n = inclinação/6; LR de
    /// ordem n como BT(⌈n/2⌉) seguido de BT(⌊n/2⌋).
    /// </summary>
    public static IReadOnlyList<Biquad> Corte(bool passaAlta, FormaFiltro forma, int inclinacao, double fc, double taxa)
    {
        var ordem = inclinacao / 6;
        if (forma == FormaFiltro.Bt)
            return Butterworth(passaAlta, ordem, fc, taxa);
        return [.. Butterworth(passaAlta, (ordem + 1) / 2, fc, taxa), .. Butterworth(passaAlta, ordem / 2, fc, taxa)];
    }

    /// <summary>O passa-alta de uma saída no tempo.</summary>
    public static IReadOnlyList<Biquad> De(PassaAlta? filtro, double taxa) =>
        filtro is null ? [] : Corte(true, filtro.Forma, filtro.Inclinacao, filtro.Frequencia, taxa);

    /// <summary>O passa-baixa de uma saída no tempo.</summary>
    public static IReadOnlyList<Biquad> De(PassaBaixa? filtro, double taxa) =>
        filtro is null ? [] : Corte(false, filtro.Forma, filtro.Inclinacao, filtro.Frequencia, taxa);

    private static bool Cabe(double taxa, double f) => f > 0 && taxa > 0 && f < FracaoMaxima * taxa;
}
