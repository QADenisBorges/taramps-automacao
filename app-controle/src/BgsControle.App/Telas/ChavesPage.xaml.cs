using BgsControle.Telas;

namespace BgsControle.App.Telas;

/// <summary>Navegação: ‹ e o Voltar do Android (que fecha a folha do risco antes, sem ligar).</summary>
public partial class ChavesPage : ContentPage
{
    private readonly ChavesViewModel _vm;

    public ChavesPage(ChavesViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    private void AoVoltar(object? sender, EventArgs e) => _ = Navigation.PopAsync();

    protected override bool OnBackButtonPressed()
    {
        if (_vm.FolhaAberta)
        {
            _vm.DesistirFolha.Execute(null);
            return true;
        }
        return base.OnBackButtonPressed();
    }
}
