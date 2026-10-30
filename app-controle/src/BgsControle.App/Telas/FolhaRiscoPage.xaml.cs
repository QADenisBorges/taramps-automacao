using BgsControle.Telas;

namespace BgsControle.App.Telas;

/// <summary>O Voltar do Android cancela: o pedido é recusado (como "Cancelar").</summary>
public partial class FolhaRiscoPage : ContentPage
{
    private readonly FolhaRiscoViewModel _vm;

    public FolhaRiscoPage(FolhaRiscoViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    protected override bool OnBackButtonPressed()
    {
        _vm.Cancelar.Execute(null);
        return true;
    }
}
