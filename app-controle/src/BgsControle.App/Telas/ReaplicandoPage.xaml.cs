using BgsControle.Telas;

namespace BgsControle.App.Telas;

/// <summary>O Voltar do Android não fecha o "REAPLICANDO": a tela sai sozinha ao terminar.</summary>
public partial class ReaplicandoPage : ContentPage
{
    public ReaplicandoPage(ReaplicandoViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override bool OnBackButtonPressed() => true;
}
