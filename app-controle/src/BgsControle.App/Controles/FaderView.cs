using System.Windows.Input;
using BgsControle.App.Recursos;

namespace BgsControle.App.Controles;

/// <summary>
/// O fader (DESIGN, canal da mesa e controle de parâmetro): trilho de 8 px em {colors.sup-2}, o nível na
/// cor da saída, a marca do 0 dB e uma só capa de fader (degradê {colors.capa-a} → {colors.capa-b}, com
/// o risco de 2 px na cor). Vertical (Mesa) ou horizontal. Arrastar a capa é o mesmo que arrastar o
/// fader; tocar no trilho não pula o valor. Cada posição vai ao <see cref="ComandoArrastar"/> e o fim do
/// arrasto ao <see cref="ComandoSoltar"/>; a lógica (ritmo, faixa, passo) é do ViewModel.
/// </summary>
public sealed class FaderView : GraphicsView, IDrawable
{
    public static readonly BindableProperty MinimoProperty = Prop(nameof(Minimo), typeof(double), 0d);
    public static readonly BindableProperty MaximoProperty = Prop(nameof(Maximo), typeof(double), 1d);
    public static readonly BindableProperty ValorProperty = Prop(nameof(Valor), typeof(double), 0d);
    public static readonly BindableProperty CorProperty = Prop(nameof(Cor), typeof(Color), null);
    public static readonly BindableProperty VerticalProperty = Prop(nameof(Vertical), typeof(bool), false);
    public static readonly BindableProperty LogaritmicoProperty = Prop(nameof(Logaritmico), typeof(bool), false);
    public static readonly BindableProperty TravadoProperty = Prop(nameof(Travado), typeof(bool), false);
    public static readonly BindableProperty ComandoArrastarProperty = Prop(nameof(ComandoArrastar), typeof(ICommand), null);
    public static readonly BindableProperty ComandoSoltarProperty = Prop(nameof(ComandoSoltar), typeof(ICommand), null);

    private double _inicio;
    private double? _arrastando;

    public FaderView()
    {
        Drawable = this;
        var pan = new PanGestureRecognizer();
        pan.PanUpdated += AoArrastar;
        GestureRecognizers.Add(pan);
    }

    public double Minimo { get => (double)GetValue(MinimoProperty); set => SetValue(MinimoProperty, value); }
    public double Maximo { get => (double)GetValue(MaximoProperty); set => SetValue(MaximoProperty, value); }
    public double Valor { get => (double)GetValue(ValorProperty); set => SetValue(ValorProperty, value); }
    public Color? Cor { get => (Color?)GetValue(CorProperty); set => SetValue(CorProperty, value); }
    public bool Vertical { get => (bool)GetValue(VerticalProperty); set => SetValue(VerticalProperty, value); }
    public bool Logaritmico { get => (bool)GetValue(LogaritmicoProperty); set => SetValue(LogaritmicoProperty, value); }
    public bool Travado { get => (bool)GetValue(TravadoProperty); set => SetValue(TravadoProperty, value); }
    public ICommand? ComandoArrastar { get => (ICommand?)GetValue(ComandoArrastarProperty); set => SetValue(ComandoArrastarProperty, value); }
    public ICommand? ComandoSoltar { get => (ICommand?)GetValue(ComandoSoltarProperty); set => SetValue(ComandoSoltarProperty, value); }

    private static BindableProperty Prop(string nome, Type tipo, object? padrao) =>
        BindableProperty.Create(nome, tipo, typeof(FaderView), padrao, propertyChanged: (b, _, _) => ((FaderView)b).Invalidate());

    // ------------------------------------------------------------------ posição

    private double Fracao(double v)
    {
        var (min, max) = (Minimo, Maximo);
        if (max <= min)
            return 0;
        v = Math.Clamp(v, min, max);
        if (Logaritmico && min > 0)
            return Math.Log(v / min) / Math.Log(max / min);
        return (v - min) / (max - min);
    }

    private double DaFracao(double f)
    {
        f = Math.Clamp(f, 0, 1);
        var (min, max) = (Minimo, Maximo);
        if (Logaritmico && min > 0)
            return min * Math.Pow(max / min, f);
        return min + f * (max - min);
    }

    /// <summary>O curso do fader: o comprimento menos a capa (30 px no sentido do curso, nos dois sentidos).</summary>
    private double Comprimento => Math.Max(1, (Vertical ? Height : Width) - 30);

    private void AoArrastar(object? sender, PanUpdatedEventArgs e)
    {
        // Travado, o arrasto não começa nem anda; mas um arrasto que já começou sempre solta (a trava
        // pode chegar no meio dele): o ViewModel recebe o fim e devolve o valor relido.
        switch (e.StatusType)
        {
            case GestureStatus.Started:
                if (Travado)
                    return;
                _inicio = Fracao(Valor);
                _arrastando = Valor;
                break;
            case GestureStatus.Running:
            {
                if (_arrastando is null || Travado)
                    return;
                var delta = Vertical ? -e.TotalY : e.TotalX;
                var v = DaFracao(_inicio + delta / Comprimento);
                _arrastando = v;
                Invalidate();
                if (ComandoArrastar?.CanExecute(v) == true)
                    ComandoArrastar.Execute(v);
                break;
            }
            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                if (_arrastando is null)
                    return;
                _arrastando = null;
                Invalidate();
                if (ComandoSoltar?.CanExecute(null) == true)
                    ComandoSoltar.Execute(null);
                break;
        }
    }

    // ------------------------------------------------------------------ desenho

    public void Draw(ICanvas canvas, RectF r)
    {
        var cor = Cor ?? Cores.De("texto");
        var f = (float)Fracao(_arrastando ?? Valor);
        canvas.Alpha = Travado ? 0.42f : 1f;
        const float trilho = 8;

        if (Vertical)
        {
            const float capaL = 56, capaA = 30;
            var x = r.Center.X - trilho / 2;
            var topo = r.Top + capaA / 2;
            var fundo = r.Bottom - capaA / 2;
            var y = fundo - f * (fundo - topo);
            canvas.FillColor = Cores.De("sup-2");
            canvas.FillRoundedRectangle(x, topo, trilho, fundo - topo, 4);
            canvas.FillColor = cor;
            canvas.FillRoundedRectangle(x, y, trilho, fundo - y, 4);
            Zero(canvas, (float)Fracao(0), zero => new RectF(r.Center.X - 14, fundo - zero * (fundo - topo) - 1, 28, 2));
            Capa(canvas, new RectF(r.Center.X - capaL / 2, y - capaA / 2, capaL, capaA), cor, vertical: true);
        }
        else
        {
            const float capaL = 30, capaA = 44;
            var y = r.Center.Y - trilho / 2;
            var esquerda = r.Left + capaL / 2;
            var direita = r.Right - capaL / 2;
            var x = esquerda + f * (direita - esquerda);
            canvas.FillColor = Cores.De("sup-2");
            canvas.FillRoundedRectangle(esquerda, y, direita - esquerda, trilho, 4);
            canvas.FillColor = cor;
            canvas.FillRoundedRectangle(esquerda, y, x - esquerda, trilho, 4);
            Zero(canvas, (float)Fracao(0), zero => new RectF(esquerda + zero * (direita - esquerda) - 1, r.Center.Y - 12, 2, 24));
            Capa(canvas, new RectF(x - capaL / 2, r.Center.Y - capaA / 2, capaL, capaA), cor, vertical: false);
        }
    }

    /// <summary>A marca do 0 dB, quando o 0 está dentro da faixa e não é uma ponta.</summary>
    private void Zero(ICanvas canvas, float fracao, Func<float, RectF> onde)
    {
        if (Logaritmico || Minimo >= 0 || Maximo <= 0)
            return;
        canvas.FillColor = Cores.De("apagado");
        canvas.FillRectangle(onde(fracao));
    }

    private static void Capa(ICanvas canvas, RectF capa, Color cor, bool vertical)
    {
        canvas.SaveState();
        canvas.SetShadow(new SizeF(0, 6), 12, Color.FromRgba(0, 0, 0, 140));
        canvas.SetFillPaint(new LinearGradientPaint(
            [new PaintGradientStop(0, Cores.De("capa-a")), new PaintGradientStop(1, Cores.De("capa-b"))],
            new Point(0, 0), new Point(0, 1)), capa);
        canvas.FillRoundedRectangle(capa, 9);
        canvas.RestoreState();
        canvas.StrokeColor = Cores.De("capa-borda");
        canvas.StrokeSize = 1;
        canvas.DrawRoundedRectangle(capa, 9);
        canvas.FillColor = cor;
        if (vertical)
            canvas.FillRectangle(capa.Left + 8, capa.Center.Y - 1, capa.Width - 16, 2);
        else
            canvas.FillRectangle(capa.Center.X - 1, capa.Top + 8, 2, capa.Height - 16);
    }
}
