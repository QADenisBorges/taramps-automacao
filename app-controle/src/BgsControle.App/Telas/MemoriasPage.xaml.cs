using BgsControle.Telas;

namespace BgsControle.App.Telas;

/// <summary>
/// Navegação: ‹ e o Voltar do Android (que fecha a folha aberta antes). O aviso rápido vem da casca (história 10d):
/// a criação fecha com esta tela aberta, e o aviso precisa aparecer aqui.
/// </summary>
public partial class MemoriasPage : ContentPage
{
    private readonly MemoriasViewModel _vm;
    private readonly SalvarViewModel _salvar;

    public MemoriasPage(MemoriasViewModel vm, SalvarViewModel salvar, CascaViewModel casca)
    {
        InitializeComponent();
        _vm = vm;
        _salvar = salvar;
        // Antes da página: a folha do Salvar não herda a tela Memórias nem por um instante.
        FolhaSalvar.BindingContext = salvar;
        BindingContext = vm;
        Controles.EspelhoAvisoRapido.Ligar(this, AvisoRapido, casca);
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
