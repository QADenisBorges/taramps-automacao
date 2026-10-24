using BgsControle.Telas;

namespace BgsControle.App.Telas;

/// <summary>Navegação: o Voltar do Android fecha a folha do Salvar antes de sair da tela.</summary>
public partial class AjusteSaidaPage : ContentPage
{
    private readonly AjusteSaidaViewModel _vm;

    public AjusteSaidaPage(AjusteSaidaViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
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
