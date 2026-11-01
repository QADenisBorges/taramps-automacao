using System.Windows.Input;
using Microsoft.Maui.Controls.Shapes;

namespace BgsControle.App.Controles;

/// <summary>
/// A folha (DESIGN, folha-de-confirmacao): sobe de baixo sobre o {colors.veu}, fundo {colors.sup-1},
/// cantos de cima {rounded.folha}, pega de 40 × 4 px. Tocar no véu chama <see cref="Fechar"/> (desiste).
/// Fica por cima da página: vai por último na grade raiz, na mesma célula do conteúdo.
/// <para>
/// História 13g: com <see cref="Bgs"/>, o visual da folha do BGS Sound (DESIGN, sheet): fundo {colors.surface-sheet} com
/// o fio {colors.border-sheet}, cantos de cima {rounded.sheet} (22), pegador de 36 × 4 px {sheet-grab}, espaço de 9 px,
/// a sombra preta para cima e o véu preto a 60 %.
/// </para>
/// </summary>
[ContentProperty(nameof(Corpo))]
public sealed class FolhaView : ContentView
{
    public static readonly BindableProperty AbertaProperty = BindableProperty.Create(
        nameof(Aberta), typeof(bool), typeof(FolhaView), false,
        propertyChanged: (b, _, n) => ((FolhaView)b).AoAbrir((bool)n));

    public static readonly BindableProperty FecharProperty =
        BindableProperty.Create(nameof(Fechar), typeof(ICommand), typeof(FolhaView));

    public static readonly BindableProperty BgsProperty = BindableProperty.Create(
        nameof(Bgs), typeof(bool), typeof(FolhaView), false, propertyChanged: (b, _, n) => ((FolhaView)b).AplicarVisual((bool)n));

    public static readonly BindableProperty CorpoProperty = BindableProperty.Create(
        nameof(Corpo), typeof(View), typeof(FolhaView),
        propertyChanged: (b, _, n) => ((FolhaView)b)._miolo.Content = (View?)n);

    private readonly ContentView _miolo = new();
    private readonly Border _folha;
    private readonly BoxView _veu;
    private readonly BoxView _pega;
    private readonly VerticalStackLayout _pilha;

    public FolhaView()
    {
        IsVisible = false;
        var veu = _veu = new BoxView();
        veu.SetDynamicResource(BoxView.ColorProperty, "veu");
        var toque = new TapGestureRecognizer();
        toque.Tapped += (_, _) =>
        {
            if (Fechar?.CanExecute(null) == true)
                Fechar.Execute(null);
        };
        veu.GestureRecognizers.Add(toque);

        var pega = _pega = new BoxView { WidthRequest = 40, HeightRequest = 4, CornerRadius = 2, HorizontalOptions = LayoutOptions.Center };
        pega.SetDynamicResource(BoxView.ColorProperty, "sup-2");

        _folha = new Border
        {
            VerticalOptions = LayoutOptions.End,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(26, 26, 0, 0) },
            Padding = new Thickness(16, 10, 16, 24),
            Content = new ScrollView
            {
                Content = _pilha = new VerticalStackLayout { Spacing = 14, Children = { pega, _miolo } },
            },
        };
        _folha.SetDynamicResource(BackgroundColorProperty, "sup-1");
        Content = new Grid { Children = { veu, _folha } };
    }

    public bool Aberta { get => (bool)GetValue(AbertaProperty); set => SetValue(AbertaProperty, value); }

    public ICommand? Fechar { get => (ICommand?)GetValue(FecharProperty); set => SetValue(FecharProperty, value); }

    public View? Corpo { get => (View?)GetValue(CorpoProperty); set => SetValue(CorpoProperty, value); }

    /// <summary>O visual da folha do BGS Sound (história 13g).</summary>
    public bool Bgs { get => (bool)GetValue(BgsProperty); set => SetValue(BgsProperty, value); }

    private void AplicarVisual(bool bgs)
    {
        if (!bgs)
            return;
        _veu.SetDynamicResource(BoxView.ColorProperty, "scrim-sheet");
        _pega.WidthRequest = 36;
        _pega.SetDynamicResource(BoxView.ColorProperty, "sheet-grab");
        _folha.SetDynamicResource(BackgroundColorProperty, "surface-sheet");
        _folha.Stroke = new SolidColorBrush(Recursos.Cores.De("border-sheet"));
        _folha.StrokeThickness = 1;
        _folha.StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(22, 22, 0, 0) };
        _folha.Padding = new Thickness(16, 14, 16, 18);
        _folha.Shadow = new Shadow { Brush = new SolidColorBrush(Recursos.Cores.De("bg")), Offset = new Point(0, -20), Radius = 40, Opacity = 1 };
        _pilha.Spacing = 9;
    }

    private async void AoAbrir(bool aberta)
    {
        IsVisible = aberta;
        if (!aberta)
            return;
        // Sobe de baixo (o único movimento da folha).
        _folha.TranslationY = 400;
        await _folha.TranslateToAsync(0, 0, 180, Easing.CubicOut);
    }
}
