using System.Diagnostics;
using BgsControle.App.Recursos;

namespace BgsControle.App.Controles;

/// <summary>
/// O spinner Anel duplo (DESIGN, spinner-dual-ring; opção 8): no viewBox 100, a trilha (r 42, branco a 8 %), o
/// arco ciano {colors.brand} (r 42, ~30 % do círculo) em sentido horário, 1,1 s por volta, e o arco rosa
/// {colors.accent-pink} (r 34, ~23 %) em sentido anti-horário, 1,5 s por volta. Pontas redondas, sem números. Com o
/// movimento reduzido (<see cref="Lento"/>), 2,2 s e 3 s. O <see cref="Traco"/> vai de 2,5 (o anel grande e apagado
/// do chão do palco) a 6 (o do Aplicando). Não é tocável.
/// </summary>
public sealed class AnelDuploView : GraphicsView, IDrawable
{
    public static readonly BindableProperty TracoProperty = BindableProperty.Create(
        nameof(Traco), typeof(double), typeof(AnelDuploView), 6.0, propertyChanged: (b, _, _) => ((AnelDuploView)b).Invalidate());

    public static readonly BindableProperty LentoProperty = BindableProperty.Create(nameof(Lento), typeof(bool), typeof(AnelDuploView), false);

    public static readonly BindableProperty GirandoProperty = BindableProperty.Create(
        nameof(Girando), typeof(bool), typeof(AnelDuploView), true, propertyChanged: (b, _, _) => ((AnelDuploView)b).Rodar());

    /// <summary>A fração do círculo de cada arco (80/264 e 50/214 no mock).</summary>
    private const float FracaoCiano = 80f / 264f;
    private const float FracaoRosa = 50f / 214f;

    private readonly Stopwatch _relogio = Stopwatch.StartNew();
    private bool _rodando;

    public AnelDuploView()
    {
        Drawable = this;
        InputTransparent = true;
        BackgroundColor = Colors.Transparent;
        Loaded += (_, _) => Rodar();
        Unloaded += (_, _) => _rodando = false;
    }

    /// <summary>A espessura no viewBox 100.</summary>
    public double Traco { get => (double)GetValue(TracoProperty); set => SetValue(TracoProperty, value); }

    /// <summary>Movimento reduzido: as voltas ficam mais lentas.</summary>
    public bool Lento { get => (bool)GetValue(LentoProperty); set => SetValue(LentoProperty, value); }

    public bool Girando { get => (bool)GetValue(GirandoProperty); set => SetValue(GirandoProperty, value); }

    private void Rodar()
    {
        if (_rodando || !Girando || !IsLoaded)
            return;
        _rodando = true;
        Dispatcher.StartTimer(TimeSpan.FromMilliseconds(16), () =>
        {
            if (!_rodando || !Girando || !IsLoaded)
            {
                _rodando = false;
                return false;
            }
            if (IsVisible && Opacity > 0)
                Invalidate();
            return true;
        });
    }

    public void Draw(ICanvas canvas, RectF r)
    {
        var lado = Math.Min(r.Width, r.Height);
        if (lado <= 0)
            return;
        var escala = lado / 100f;
        var cx = r.Center.X;
        var cy = r.Center.Y;
        var traco = (float)Traco * escala;
        var segundos = _relogio.Elapsed.TotalSeconds;
        var voltaCiano = Lento ? 2.2 : 1.1;
        var voltaRosa = Lento ? 3.0 : 1.5;
        var anguloCiano = (float)(segundos / voltaCiano % 1 * 360);
        var anguloRosa = (float)(-(segundos / voltaRosa % 1) * 360);

        canvas.StrokeLineCap = LineCap.Round;
        canvas.StrokeSize = traco;

        var r42 = 42 * escala;
        canvas.StrokeColor = Cores.De("ring-track");
        canvas.DrawCircle(cx, cy, r42);

        // O MAUI conta o ângulo no sentido anti-horário a partir das 3 horas; o horário é o negativo.
        canvas.StrokeColor = Cores.De("brand");
        DesenharArco(canvas, cx, cy, r42, -anguloCiano + 90, FracaoCiano);

        var r34 = 34 * escala;
        canvas.StrokeColor = Cores.De("accent-pink");
        DesenharArco(canvas, cx, cy, r34, -anguloRosa + 90, FracaoRosa);
    }

    private static void DesenharArco(ICanvas canvas, float cx, float cy, float raio, float inicio, float fracao)
    {
        var caixa = new RectF(cx - raio, cy - raio, raio * 2, raio * 2);
        canvas.DrawArc(caixa, inicio, inicio - fracao * 360, clockwise: true, closed: false);
    }
}
