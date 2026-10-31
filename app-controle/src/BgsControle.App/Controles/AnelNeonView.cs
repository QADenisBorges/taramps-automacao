using BgsControle.App.Recursos;

namespace BgsControle.App.Controles;

/// <summary>
/// Um anel de neon da passagem até o Aplicando (DESIGN, neon-sphere): uma linha só, r 44 no viewBox 100, traço 2,2,
/// sem preenchimento, com o brilho da cor (2 px forte e 6 px fraco). O ciano ({colors.brand}) gira em pé, no eixo
/// vertical; o rosa ({colors.accent-pink}), deitado, no horizontal. Quem gira é a página (RotationX/Y).
/// </summary>
public sealed class AnelNeonView : GraphicsView, IDrawable
{
    public static readonly BindableProperty TokenProperty = BindableProperty.Create(
        nameof(Token), typeof(string), typeof(AnelNeonView), "brand", propertyChanged: (b, _, _) => ((AnelNeonView)b).Invalidate());

    public AnelNeonView()
    {
        Drawable = this;
        InputTransparent = true;
        BackgroundColor = Colors.Transparent;
    }

    /// <summary>O token da cor: "brand" ou "accent-pink".</summary>
    public string Token { get => (string)GetValue(TokenProperty); set => SetValue(TokenProperty, value); }

    public void Draw(ICanvas canvas, RectF r)
    {
        var lado = Math.Min(r.Width, r.Height);
        if (lado <= 0)
            return;
        var escala = lado / 100f;
        var cor = Cores.De(Token);
        canvas.StrokeColor = cor;
        canvas.StrokeSize = 2.2f * escala;
        canvas.SetShadow(SizeF.Zero, 6 * escala, cor.WithAlpha(0.5f));
        canvas.DrawCircle(r.Center.X, r.Center.Y, 44 * escala);
        canvas.SetShadow(SizeF.Zero, 2 * escala, cor.WithAlpha(0.9f));
        canvas.DrawCircle(r.Center.X, r.Center.Y, 44 * escala);
    }
}
