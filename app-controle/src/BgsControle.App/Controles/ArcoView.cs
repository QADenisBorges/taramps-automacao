using BgsControle.App.Recursos;

namespace BgsControle.App.Controles;

/// <summary>
/// O arco de luz do cartão da memória em uso (DESIGN, cartao-memoria-em-uso): um anel fino nas três luzes
/// ({colors.canal-1} → {colors.canal-2} → {colors.canal-3}), no canto superior direito, opacidade 0,85.
/// Só enfeite: não é tocável.
/// </summary>
public sealed class ArcoView : GraphicsView, IDrawable
{
    public ArcoView()
    {
        Drawable = this;
        InputTransparent = true;
    }

    public void Draw(ICanvas canvas, RectF r)
    {
        canvas.Alpha = 0.85f;
        canvas.StrokeSize = 2;
        canvas.StrokeLineCap = LineCap.Round;
        var caixa = new RectF(r.Left + 2, r.Top + 2, r.Width - 4, r.Height - 4);
        string[] luzes = ["canal-1", "canal-2", "canal-3"];
        // Três trechos do anel, de 90° a 360°, um por luz.
        for (var i = 0; i < luzes.Length; i++)
        {
            canvas.StrokeColor = Cores.De(luzes[i]);
            var de = 90f + i * 90f;
            canvas.DrawArc(caixa, de, de + 90f, clockwise: false, closed: false);
        }
    }
}
