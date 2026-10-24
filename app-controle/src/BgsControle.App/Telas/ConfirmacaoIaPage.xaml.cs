using BgsControle.Telas;

namespace BgsControle.App.Telas;

/// <summary>O Voltar do Android desiste: a IA recebe a recusa (como "Não deixar").</summary>
public partial class ConfirmacaoIaPage : ContentPage
{
    private readonly ConfirmacaoIaViewModel _vm;

    public ConfirmacaoIaPage(ConfirmacaoIaViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    protected override bool OnBackButtonPressed()
    {
        _vm.Recusar.Execute(null);
        return true;
    }
}
