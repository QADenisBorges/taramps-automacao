using BgsControle.Telas;

namespace BgsControle.App.Telas;

/// <summary>Navegação: o Voltar do Android fecha a folha aberta antes de sair da tela.</summary>
public partial class FuncaoPage : ContentPage
{
    private readonly FuncaoViewModel _vm;

    public FuncaoPage(FuncaoViewModel vm, CascaViewModel? casca = null)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
        // História 13g: o aviso rápido vem da casca (a recusa da cerca acontece nesta tela).
        Controles.EspelhoAvisoRapido.Ligar(this, AvisoRapido, casca);
    }

    protected override bool OnBackButtonPressed()
    {
        if (_vm.Salvar.Aberta)
        {
            _vm.Salvar.Cancelar.Execute(null);
            return true;
        }
        return base.OnBackButtonPressed();
    }
}
