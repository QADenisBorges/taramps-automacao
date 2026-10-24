using BgsControle.Telas;

namespace BgsControle.App.Telas;

/// <summary>Navegação: o Voltar do Android fecha a folha aberta antes de sair.</summary>
public partial class SetupsPage : ContentPage
{
    private readonly SetupsViewModel _vm;

    public SetupsPage(SetupsViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    protected override bool OnBackButtonPressed()
    {
        if (_vm.RenomearAberto)
        {
            _vm.CancelarRenomear.Execute(null);
            return true;
        }
        if (_vm.ApagarAberto)
        {
            _vm.CancelarApagar.Execute(null);
            return true;
        }
        return base.OnBackButtonPressed();
    }
}
