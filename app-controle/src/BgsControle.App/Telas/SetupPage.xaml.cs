using BgsControle.Telas;

namespace BgsControle.App.Telas;

/// <summary>Navegação: ‹ volta; o Voltar do Android fecha antes a confirmação do Tirar e a folha aberta.</summary>
public partial class SetupPage : ContentPage
{
    private readonly EquipamentosViewModel _vm;

    public SetupPage(EquipamentosViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    private async void AoVoltar(object? sender, EventArgs e)
    {
        if (Navigation.NavigationStack.Count > 1)
            await Navigation.PopAsync();
    }

    protected override bool OnBackButtonPressed()
    {
        if (_vm.TirarAberto)
        {
            _vm.CancelarTirar.Execute(null);
            return true;
        }
        if (_vm.FolhaAberta)
        {
            _vm.Cancelar.Execute(null);
            return true;
        }
        return base.OnBackButtonPressed();
    }
}
