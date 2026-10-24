using BgsControle.App.Controles;
using BgsControle.Telas;

namespace BgsControle.App.Telas;

/// <summary>
/// Navegação: ‹ volta; o Voltar do Android fecha antes a confirmação do Tirar e a folha aberta. O esquema
/// do Setup (história 10b) vai no lugar reservado, ligado ao mesmo Setup mostrado pela lista.
/// </summary>
public partial class SetupPage : ContentPage
{
    private readonly EquipamentosViewModel _vm;

    public SetupPage(EquipamentosViewModel vm, EsquemaViewModel esquema)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
        LugarDoEsquema.Content = new EsquemaView(esquema);
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
