using BgsControle.Telas;

namespace BgsControle.App.Telas;

/// <summary>Navegação: o Voltar do Android fecha a folha aberta antes de sair da tela.</summary>
public partial class FuncaoPage : ContentPage
{
    private readonly FuncaoViewModel _vm;

    public FuncaoPage(FuncaoViewModel vm)
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
        if (_vm.Link.FolhaAberta)
        {
            _vm.Link.CancelarFolha.Execute(null);
            return true;
        }
        return base.OnBackButtonPressed();
    }
}
