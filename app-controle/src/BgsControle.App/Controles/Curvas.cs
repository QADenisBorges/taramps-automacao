namespace BgsControle.App.Controles;

/// <summary>
/// As curvas do DESIGN (motion) como <see cref="Easing"/> do MAUI: a <c>cubic-bezier(x1, y1, x2, y2)</c> do CSS, e
/// as que a criação da memória usa. A cada t (0 a 1, o tempo), acha o ponto da curva com esse x e devolve o y.
/// </summary>
public static class Curvas
{
    /// <summary>A pergunta entrando (motion.question-in) e os itens do resumo: (.2, .8, .2, 1).</summary>
    public static readonly Easing Entrada = Bezier(0.2, 0.8, 0.2, 1);

    /// <summary>O Foco de entrada da frase e do valor (motion.phrase-in): (.3, .55, .35, 1).</summary>
    public static readonly Easing FocoEntrada = Bezier(0.3, 0.55, 0.35, 1);

    /// <summary>O Foco de saída (motion.phrase-out): (.6, 0, .7, .45).</summary>
    public static readonly Easing FocoSaida = Bezier(0.6, 0, 0.7, 0.45);

    /// <summary>A barrinha, o horizonte e a passagem (motion.value-bar, horizon-follow, passage): (.45, 0, .2, 1).</summary>
    public static readonly Easing Suave = Bezier(0.45, 0, 0.2, 1);

    public static Easing Bezier(double x1, double y1, double x2, double y2) => new(t => Resolver(t, x1, y1, x2, y2));

    /// <summary>(1 − cos πt) / 2: a volta dos anéis na passagem (motion.passage-spin).</summary>
    public static double Seno(double t) => (1 - Math.Cos(Math.PI * t)) / 2;

    /// <summary>O smoothstep de a a b.</summary>
    public static double Degrau(double a, double b, double t)
    {
        var x = Math.Clamp((t - a) / (b - a), 0, 1);
        return x * x * (3 - 2 * x);
    }

    private static double Resolver(double x, double x1, double y1, double x2, double y2)
    {
        if (x <= 0)
            return 0;
        if (x >= 1)
            return 1;
        // Newton no x(t), com a bissecção de reserva.
        var t = x;
        for (var i = 0; i < 8; i++)
        {
            var erro = Ponto(t, x1, x2) - x;
            if (Math.Abs(erro) < 1e-5)
                return Ponto(t, y1, y2);
            var d = Derivada(t, x1, x2);
            if (Math.Abs(d) < 1e-6)
                break;
            t -= erro / d;
        }
        double baixo = 0, alto = 1;
        t = x;
        for (var i = 0; i < 30; i++)
        {
            var v = Ponto(t, x1, x2);
            if (Math.Abs(v - x) < 1e-5)
                break;
            if (v < x)
                baixo = t;
            else
                alto = t;
            t = (baixo + alto) / 2;
        }
        return Ponto(t, y1, y2);
    }

    private static double Ponto(double t, double p1, double p2)
    {
        var u = 1 - t;
        return 3 * u * u * t * p1 + 3 * u * t * t * p2 + t * t * t;
    }

    private static double Derivada(double t, double p1, double p2)
    {
        var u = 1 - t;
        return 3 * u * u * p1 + 6 * u * t * (p2 - p1) + 3 * t * t * (1 - p2);
    }
}
