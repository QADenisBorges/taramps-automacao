using BgsControle.Telas;

namespace BgsControle.App.Telas;

/// <summary>Navegação: ‹ e o Voltar do Android (que fecha o campo ou a folha aberta antes).</summary>
public partial class PresetsPage : ContentPage
{
    private readonly PresetsViewModel _vm;

    public PresetsPage(PresetsViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    private void AoVoltar(object? sender, EventArgs e) => _ = Navigation.PopAsync();

    protected override bool OnBackButtonPressed()
    {
        if (_vm.ApagarAberto)
            _vm.CancelarApagar.Execute(null);
        else if (_vm.RenomearAberto)
            _vm.CancelarRenomear.Execute(null);
        else
            return base.OnBackButtonPressed();
        return true;
    }
}
