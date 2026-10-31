using BgsControle.App.Recursos;
using Microsoft.Maui.Controls.Shapes;

namespace BgsControle.App.Controles;

/// <summary>
/// O aviso rápido (DESIGN, toast; CAP-14): um bloco {colors.surface-2} de cantos 12, a 16 px das laterais e 76 px
/// acima do pé, com a sombra preta, mostrando <see cref="Texto"/> ("Memória MUSICA ORIG criada no Casa 2.1."). Quem
/// conta os 2,6 s é a casca; aqui ele só entra e sai pela opacidade. Não pega toque. Vai por último na grade raiz.
/// </summary>
public sealed class AvisoRapidoView : ContentView
{
    public static readonly BindableProperty TextoProperty = BindableProperty.Create(
        nameof(Texto), typeof(string), typeof(AvisoRapidoView), null, propertyChanged: (b, _, n) => ((AvisoRapidoView)b).Mostrar(n as string));

    /// <summary>O TalkBack lê o aviso (só um lugar anuncia: a casca; a tela Memórias só mostra).</summary>
    public static readonly BindableProperty AnunciarProperty = BindableProperty.Create(nameof(Anunciar), typeof(bool), typeof(AvisoRapidoView), true);

    private readonly Label _texto;

    public AvisoRapidoView()
    {
        IsVisible = false;
        InputTransparent = true;
        VerticalOptions = LayoutOptions.End;
        _texto = new Label { FontSize = 12, LineBreakMode = LineBreakMode.WordWrap };
        _texto.SetDynamicResource(Label.TextColorProperty, "text-primary");
        var bloco = new Border
        {
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            Padding = new Thickness(14, 12),
            Margin = new Thickness(16, 0, 16, 76),
            Content = _texto,
            Shadow = new Shadow { Brush = new SolidColorBrush(Cores.De("bg")), Offset = new Point(0, 10), Radius = 30, Opacity = 1 },
        };
        bloco.SetDynamicResource(BackgroundColorProperty, "surface-2");
        Content = bloco;
        SemanticProperties.SetHeadingLevel(_texto, SemanticHeadingLevel.None);
    }

    public string? Texto { get => (string?)GetValue(TextoProperty); set => SetValue(TextoProperty, value); }

    public bool Anunciar { get => (bool)GetValue(AnunciarProperty); set => SetValue(AnunciarProperty, value); }

    private async void Mostrar(string? texto)
    {
        if (string.IsNullOrEmpty(texto))
        {
            await this.FadeToAsync(0, 200, Easing.CubicOut);
            if (string.IsNullOrEmpty(Texto))
                IsVisible = false;
            return;
        }
        _texto.Text = texto;
        Opacity = 0;
        IsVisible = true;
        if (Anunciar)
            SemanticScreenReader.Default.Announce(texto);
        await this.FadeToAsync(1, 200, Easing.CubicOut);
    }
}
