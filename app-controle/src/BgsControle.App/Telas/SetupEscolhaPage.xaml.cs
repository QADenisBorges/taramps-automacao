using BgsControle.Telas;

namespace BgsControle.App.Telas;

/// <summary>
/// Navegação: na dúvida, o Voltar do Android não fecha (a escolha é obrigatória); na troca, desiste. Uma
/// folha aberta fecha antes.
/// </summary>
public partial class SetupEscolhaPage : ContentPage
{
    private readonly SetupViewModel _vm;

    public SetupEscolhaPage(SetupViewModel vm, CascaViewModel casca)
    {
        InitializeComponent();
        _vm = vm;
        // A pílula de status é da casca (posta antes da página, para não herdar a tela).
        Pilula.BindingContext = casca;
        BindingContext = vm;
    }

    protected override bool OnBackButtonPressed()
    {
        if (_vm.ListaAberta)
            _vm.FecharLista.Execute(null);
        else if (_vm.CriarAberto)
            _vm.CancelarCriar.Execute(null);
        else if (_vm.PodeDesistir)
            _vm.Desistir.Execute(null);
        return true;
    }
}
