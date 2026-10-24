using System.Windows.Input;
using BgsControle.App.Recursos;

namespace BgsControle.App.Controles;

/// <summary>
/// O botão giratório do ganho (DESIGN, botao-giratorio): arco de 16 px na cor da saída sobre
/// {colors.sup-2}, 270° de curso começando embaixo à esquerda, o arco na posição real do valor dentro da
/// faixa, o disco com o degradê radial e o contorno {colors.capa-borda} e o ponto branco na ponta. O valor
/// escrito fica por cima (um Label na página, que se toca para escrever). Arrastar gira: para cima ou para
/// a direita sobe; o curso inteiro em cerca de 300 px. O ViewModel recebe cada valor e o fim do arrasto.
/// </summary>
public sealed class GiratorioView : GraphicsView, IDrawable
{
    public static readonly BindableProperty MinimoProperty = Prop(nameof(Minimo), typeof(double), 0d);
    public static readonly BindableProperty MaximoProperty = Prop(nameof(Maximo), typeof(double), 1d);
    public static readonly BindableProperty ValorProperty = Prop(nameof(Valor), typeof(double), 0d);
    public static readonly BindableProperty CorProperty = Prop(nameof(Cor), typeof(Color), null);
    public static readonly BindableProperty TravadoProperty = Prop(nameof(Travado), typeof(bool), false);
    public static readonly BindableProperty ComandoArrastarProperty = Prop(nameof(ComandoArrastar), typeof(ICommand), null);
    public static readonly BindableProperty ComandoSoltarProperty = Prop(nameof(ComandoSoltar), typeof(ICommand), null);

    private const float Inicio = 225f; // embaixo à esquerda (graus, sentido anti-horário a partir das 3 h)
    private const float Curso = 270f;
    private const double PixelsPorCurso = 300;

    private double _inicioArrasto;
    private double? _arrastando;

    public GiratorioView()
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
    public bool Travado { get => (bool)GetValue(TravadoProperty); set => SetValue(TravadoProperty, value); }
    public ICommand? ComandoArrastar { get => (ICommand?)GetValue(ComandoArrastarProperty); set => SetValue(ComandoArrastarProperty, value); }
    public ICommand? ComandoSoltar { get => (ICommand?)GetValue(ComandoSoltarProperty); set => SetValue(ComandoSoltarProperty, value); }

    private static BindableProperty Prop(string nome, Type tipo, object? padrao) =>
        BindableProperty.Create(nome, tipo, typeof(GiratorioView), padrao, propertyChanged: (b, _, _) => ((GiratorioView)b).Invalidate());

    private double Fracao(double v) => Maximo <= Minimo ? 0 : (Math.Clamp(v, Minimo, Maximo) - Minimo) / (Maximo - Minimo);

    private void AoArrastar(object? sender, PanUpdatedEventArgs e)
    {
        // Travado, o arrasto não começa nem anda; mas um arrasto que já começou sempre solta (a trava
        // pode chegar no meio dele): o ViewModel recebe o fim e devolve o valor relido.
        switch (e.StatusType)
        {
            case GestureStatus.Started:
                if (Travado)
                    return;
                _inicioArrasto = Fracao(Valor);
                _arrastando = Valor;
                break;
            case GestureStatus.Running:
            {
                if (_arrastando is null || Travado)
                    return;
                var f = Math.Clamp(_inicioArrasto + (e.TotalX - e.TotalY) / PixelsPorCurso, 0, 1);
                var v = Minimo + f * (Maximo - Minimo);
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

    public void Draw(ICanvas canvas, RectF r)
    {
        var cor = Cor ?? Cores.De("texto");
        canvas.Alpha = Travado ? 0.42f : 1f;
        const float arco = 16;
        var lado = Math.Min(r.Width, r.Height) - arco - 4;
        var caixa = new RectF(r.Center.X - lado / 2, r.Center.Y - lado / 2, lado, lado);
        var f = (float)Fracao(_arrastando ?? Valor);

        canvas.StrokeLineCap = LineCap.Round;
        canvas.StrokeSize = arco;
        canvas.StrokeColor = Cores.De("sup-2");
        canvas.DrawArc(caixa, Inicio, Inicio - Curso, clockwise: true, closed: false);
        if (f > 0.001f)
        {
            canvas.StrokeColor = cor;
            canvas.DrawArc(caixa, Inicio, Inicio - Curso * f, clockwise: true, closed: false);
        }

        // O disco.
        var raioDisco = lado / 2 - arco - 10;
        var disco = new RectF(r.Center.X - raioDisco, r.Center.Y - raioDisco, raioDisco * 2, raioDisco * 2);
        canvas.SaveState();
        canvas.SetShadow(new SizeF(0, 10), 24, Color.FromRgba(0, 0, 0, 153));
        canvas.SetFillPaint(new RadialGradientPaint(
            [new PaintGradientStop(0, Cores.De("capa-a")), new PaintGradientStop(1, Cores.De("sup-1"))]), disco);
        canvas.FillEllipse(disco);
        canvas.RestoreState();
        canvas.StrokeSize = 1;
        canvas.StrokeColor = Cores.De("capa-borda");
        canvas.DrawEllipse(disco);

        // O ponto branco na ponta do arco, com o brilho na cor da saída.
        var angulo = (Inicio - Curso * f) * Math.PI / 180;
        var px = (float)(r.Center.X + lado / 2 * Math.Cos(angulo));
        var py = (float)(r.Center.Y - lado / 2 * Math.Sin(angulo));
        canvas.SaveState();
        canvas.SetShadow(new SizeF(0, 0), 8, cor);
        canvas.FillColor = Cores.De("texto");
        canvas.FillCircle(px, py, 5);
        canvas.RestoreState();
    }
}
