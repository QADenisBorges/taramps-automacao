using BgsControle.Telas;

namespace BgsControle.App.Telas;

/// <summary>O Voltar do Android faz o "Voltar ao player do app" (história 13g).</summary>
public partial class OutroPlayerPage : ContentPage
{
    private readonly PlayerViewModel _vm;

    public OutroPlayerPage(PlayerViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    protected override bool OnBackButtonPressed()
    {
        _vm.VoltarAoPlayer.Execute(null);
        return true;
    }
}
