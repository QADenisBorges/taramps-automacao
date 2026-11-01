using BgsControle.Telas;

namespace BgsControle.App.Telas;

/// <summary>O Voltar do Android fecha a folha de risco do Máximo antes; depois, a folha do nível (história 13g).</summary>
public partial class NivelFolhaPage : ContentPage
{
    private readonly NivelViewModel _vm;

    public NivelFolhaPage(NivelViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    protected override bool OnBackButtonPressed()
    {
        if (_vm.RiscoAberto)
            _vm.Cancelar.Execute(null);
        else
            _vm.Fechar.Execute(null);
        return true;
    }
}
