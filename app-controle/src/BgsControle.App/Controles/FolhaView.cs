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
        var toque = new TapGestureRecognizer();
        toque.Tapped += (_, _) =>
        {
            if (Fechar?.CanExecute(null) == true)
                Fechar.Execute(null);
        };
        veu.GestureRecognizers.Add(toque);

        var pega = _pega = new BoxView { HeightRequest = 4, CornerRadius = 2, HorizontalOptions = LayoutOptions.Center };

        _folha = new Border
        {
            VerticalOptions = LayoutOptions.End,
            Content = new ScrollView
            {
                Content = _pilha = new VerticalStackLayout { Children = { pega, _miolo } },
            },
        };
        Content = new Grid { Children = { veu, _folha } };
        AplicarVisual(false);
    }

    public bool Aberta { get => (bool)GetValue(AbertaProperty); set => SetValue(AbertaProperty, value); }

    public ICommand? Fechar { get => (ICommand?)GetValue(FecharProperty); set => SetValue(FecharProperty, value); }

    public View? Corpo { get => (View?)GetValue(CorpoProperty); set => SetValue(CorpoProperty, value); }

    /// <summary>O visual da folha do BGS Sound (história 13g).</summary>
    public bool Bgs { get => (bool)GetValue(BgsProperty); set => SetValue(BgsProperty, value); }

    /// <summary>
    /// O visual: o padrão (véu, sup-1, cantos 26, pega 40 × 4) ou o do BGS Sound. Reversível; todas as cores por recurso
    /// dinâmico (os pincéis do fio e da sombra também vêm do dicionário).
    /// </summary>
    private void AplicarVisual(bool bgs)
    {
        _veu.SetDynamicResource(BoxView.ColorProperty, bgs ? "scrim-sheet" : "veu");
        _pega.WidthRequest = bgs ? 36 : 40;
        _pega.SetDynamicResource(BoxView.ColorProperty, bgs ? "sheet-grab" : "sup-2");
        _folha.SetDynamicResource(BackgroundColorProperty, bgs ? "surface-sheet" : "sup-1");
        _folha.SetDynamicResource(Border.StrokeProperty, bgs ? "pincel-border-sheet" : "pincel-transparente");
        _folha.StrokeThickness = bgs ? 1 : 0;
        _folha.StrokeShape = new RoundRectangle { CornerRadius = bgs ? new CornerRadius(22, 22, 0, 0) : new CornerRadius(26, 26, 0, 0) };
        _folha.Padding = bgs ? new Thickness(16, 14, 16, 18) : new Thickness(16, 10, 16, 24);
        // A sombra não fica na árvore de elementos (um recurso dinâmico nela não resolve): o pincel vem do dicionário pela chave.
        _folha.Shadow = bgs && Application.Current?.Resources.TryGetValue("pincel-bg", out var pincel) == true && pincel is Brush b
            ? new Shadow { Brush = b, Offset = new Point(0, -20), Radius = 40, Opacity = 1 }
            : null!;
        _pilha.Spacing = bgs ? 9 : 14;
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
