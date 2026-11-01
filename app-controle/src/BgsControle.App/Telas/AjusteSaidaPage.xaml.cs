using BgsControle.Telas;

namespace BgsControle.App.Telas;

/// <summary>
/// Navegação: o Voltar do Android fecha a folha do Salvar (e a da conta do limite) antes de sair da tela. História 13g: o
/// aviso rápido vem da casca (a recusa da cerca acontece nesta tela).
/// </summary>
public partial class AjusteSaidaPage : ContentPage
{
    private readonly AjusteSaidaViewModel _vm;

    public AjusteSaidaPage(AjusteSaidaViewModel vm, CascaViewModel? casca = null)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
        Controles.EspelhoAvisoRapido.Ligar(this, AvisoRapido, casca);
    }

    protected override bool OnBackButtonPressed()
    {
        if (_vm.Limite.ContaAberta)
        {
            _vm.Limite.FecharConta.Execute(null);
            return true;
        }
        if (_vm.Salvar.Aberta)
        {
            _vm.Salvar.Cancelar.Execute(null);
            return true;
        }
        return base.OnBackButtonPressed();
    }
}
