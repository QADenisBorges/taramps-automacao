using BgsControle.Telas;

namespace BgsControle.App.Telas;

/// <summary>Navegação: ‹ e o Voltar do Android (que fecha a folha aberta antes).</summary>
public partial class MemoriasPage : ContentPage
{
    private readonly MemoriasViewModel _vm;
    private readonly SalvarViewModel _salvar;

    public MemoriasPage(MemoriasViewModel vm, SalvarViewModel salvar)
    {
        InitializeComponent();
        _vm = vm;
        _salvar = salvar;
        // Antes da página: a folha do Salvar não herda a tela Memórias nem por um instante.
        FolhaSalvar.BindingContext = salvar;
        BindingContext = vm;
    }

    private void AoVoltar(object? sender, EventArgs e) => _ = Navigation.PopAsync();

    protected override bool OnBackButtonPressed()
    {
        if (_salvar.Aberta)
            _salvar.Cancelar.Execute(null);
        else if (_vm.CriarAberto)
            _vm.CancelarCriar.Execute(null);
        else if (_vm.RenomearAberto)
            _vm.CancelarRenomear.Execute(null);
        else if (_vm.ApagarAberto)
            _vm.CancelarApagar.Execute(null);
        else
            return base.OnBackButtonPressed();
        return true;
    }
}
