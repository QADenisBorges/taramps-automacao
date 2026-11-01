using BgsControle.App.Plataforma;
using BgsControle.Telas;

namespace BgsControle.App.Telas;

/// <summary>
/// A folha da proposta de ficha (história 14c), uma camada global da casca. O Voltar do Android fecha sem gravar, como o
/// véu. Com o movimento reduzido, o anel do Salvar gira devagar.
/// </summary>
public partial class PropostaFichaPage : ContentPage
{
    private readonly PropostaFichaViewModel _vm;

    public PropostaFichaPage(PropostaFichaViewModel vm, CascaViewModel? casca = null)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
        Controles.EspelhoAvisoRapido.Ligar(this, AvisoRapido, casca);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        AnelSalvar.Lento = Movimento.Reduzido();
    }

    protected override bool OnBackButtonPressed()
    {
        _vm.Fechar.Execute(null);
        return true;
    }
}
