using System.Numerics;

namespace BgsControle.Nucleo.Rendimento;

/// <summary>A forma do filtro: Linkwitz-Riley ou Butterworth.</summary>
public enum FormaFiltro
{
    Lr,
    Bt,
}

/// <summary>
/// O passa-alta (ou subsônico) de uma saída, só pelo módulo (história 13a): |BTn| = 1/√(1+(fc/f)^(2n)) e
/// |LRn| = 1/(1+(fc/f)^n), com n = inclinação/6. Só o pico de |x| importa, e o módulo dessas formas é exato.
/// </summary>
/// <param name="Forma">LR ou BT.</param>
/// <param name="Inclinacao">Em dB/oit (6 a 48).</param>
/// <param name="Frequencia">A frequência de corte, em Hz.</param>
public sealed record PassaAlta(FormaFiltro Forma, int Inclinacao, double Frequencia)
{
    /// <summary>n = inclinação/6.</summary>
    public int Ordem => Inclinacao / 6;

    /// <summary>"LR24", "BT24".</summary>
    public string Rotulo => $"{(Forma == FormaFiltro.Lr ? "LR" : "BT")}{Inclinacao}";

    /// <summary>|H(f)|.</summary>
    public double Modulo(double f)
    {
        var r = Frequencia / f;
        return Forma == FormaFiltro.Lr
            ? 1 / (1 + Math.Pow(r, Ordem))
            : 1 / Math.Sqrt(1 + Math.Pow(r, 2 * Ordem));
    }

    /// <summary>
    /// O passa-alta do aparelho: o tipo (o índice do <c>042</c>, 0 = OFF) e a frequência (o <c>040</c>).
    /// Null com o filtro em OFF, sem leitura ou com um índice que o catálogo não conhece: sem filtro, que é o
    /// lado conservador.
    /// </summary>
    public static PassaAlta? DoAparelho(decimal? indice, decimal? frequencia)
    {
        if (indice is not { } i || frequencia is not { } f || f <= 0 || i != decimal.Truncate(i))
            return null;
        if (!Catalogo.Filtros.TryGetValue((int)i, out var rotulo) || rotulo.Length < 3)
            return null;
        var forma = rotulo.StartsWith("LR", StringComparison.Ordinal) ? FormaFiltro.Lr
            : rotulo.StartsWith("BT", StringComparison.Ordinal) ? FormaFiltro.Bt
            : (FormaFiltro?)null;
        if (forma is null || !int.TryParse(rotulo[2..], System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out var inclinacao))
            return null;
        return new PassaAlta(forma.Value, inclinacao, (double)f);
    }
}

/// <summary>Selada ou dutada (o modelo da caixa).</summary>
public enum TipoCaixaModelo
{
    Selada,
    Dutada,
}

/// <summary>Os dados do alto-falante no SI (Ω, Hz, m³, m², m, T·m, H). Sem Bl, ele sai do Qes; sem Le, Le = 0.</summary>
public sealed record AltoFalanteModelo(double Re, double Fs, double Qms, double Vas, double Sd, double Xmax, double? Bl, double? Qes, double Le);

/// <summary>A caixa no SI: o tipo, o volume (m³) e, na dutada, a sintonia (Hz).</summary>
public sealed record CaixaModelo(TipoCaixaModelo Tipo, double Volume, double? Fb);

/// <summary>A tensão RMS de um tom em que o cone chega ao Xmax, e a pior frequência.</summary>
public sealed record ResultadoExcursao(double Volts, double PiorFrequencia);

/// <summary>
/// O modelo eletromecânico da excursão (história 13a; <c>calculo_excursao_jbl.py</c> e
/// <c>calculo_excursao_sub.py</c>): Cms = Vas/(ρc²Sd²), Mms = 1/((2πFs)²·Cms), Rms = 2πFs·Mms/Qms e, sem
/// Bl, Bl = √(2πFs·Mms·Re/Qes). A selada é o pistão com a mola do ar da caixa, só com a Re; a dutada, o modelo de
/// Small com Ze = Re + s·Le, perda Ql = 7 e o duto sintonizado na Fb. O filtro entra só pelo módulo, e a
/// pior frequência sai de uma grade logarítmica de 10 Hz a 20 kHz.
/// </summary>
public static class ModeloExcursao
{
    /// <summary>A densidade do ar, em kg/m³.</summary>
    public const double Rho = 1.204;

    /// <summary>A velocidade do som, em m/s.</summary>
    public const double C = 343.0;

    /// <summary>A perda da caixa dutada.</summary>
    public const double Ql = 7.0;

    public const double FrequenciaMinima = 10;
    public const double FrequenciaMaxima = 20000;
    public const int Pontos = 4000;

    /// <summary>A grade logarítmica de 10 Hz a 20 kHz, com <see cref="Pontos"/> pontos.</summary>
    public static IReadOnlyList<double> Grade { get; } = Enumerable.Range(0, Pontos)
        .Select(i => FrequenciaMinima * Math.Pow(FrequenciaMaxima / FrequenciaMinima, i / (double)(Pontos - 1)))
        .ToArray();

    /// <summary>A tensão RMS de um tom em que o cone chega ao Xmax na pior frequência, com o filtro (null = sem filtro).</summary>
    public static ResultadoExcursao TensaoNoXmax(AltoFalanteModelo af, CaixaModelo caixa, PassaAlta? filtro)
    {
        var pior = 0.0;
        var fPior = Grade[0];
        foreach (var f in Grade)
        {
            var x = ExcursaoPorVolt(af, caixa, f) * (filtro?.Modulo(f) ?? 1.0);
            if (x > pior)
            {
                pior = x;
                fPior = f;
            }
        }
        return new ResultadoExcursao(pior > 0 ? af.Xmax / pior : double.PositiveInfinity, fPior);
    }

    /// <summary>A excursão de pico (m) de um tom de 1 V RMS na frequência, sem filtro.</summary>
    public static double ExcursaoPorVolt(AltoFalanteModelo af, CaixaModelo caixa, double f)
    {
        var cms = af.Vas / (Rho * C * C * af.Sd * af.Sd);
        var ws = 2 * Math.PI * af.Fs;
        var mms = 1 / (ws * ws * cms);
        var rms = ws * mms / af.Qms;
        var bl = af.Bl ?? Math.Sqrt(ws * mms * af.Re / (af.Qes ?? double.NaN));
        var s = new Complex(0, 2 * Math.PI * f);
        var ze = af.Re + s * af.Le;
        var pico = Math.Sqrt(2);

        if (caixa.Tipo == TipoCaixaModelo.Selada)
        {
            var kt = 1 / cms + Rho * C * C * af.Sd * af.Sd / caixa.Volume;
            // A selada usa só a Re (o calculo_excursao_jbl.py); a Le entra só na dutada.
            var x = bl * pico / af.Re / (mms * s * s + (rms + bl * bl / af.Re) * s + kt);
            return x.Magnitude;
        }

        var fb = caixa.Fb ?? double.NaN;
        var cab = caixa.Volume / (Rho * C * C);
        var map = 1 / (Math.Pow(2 * Math.PI * fb, 2) * cab);
        var ral = Ql / (2 * Math.PI * fb * cab);
        var sd = af.Sd;
        var pg = bl / (ze * sd);
        var zae = bl * bl / (ze * sd * sd);
        var zd = s * mms / (sd * sd) + 1 / (s * cms * sd * sd) + rms / (sd * sd);
        var zb = 1 / (s * cab + 1 / ral + 1 / (s * map));
        var ud = pg / (zae + zd + zb);
        return pico * ud.Magnitude / (sd * s.Magnitude);
    }
}
