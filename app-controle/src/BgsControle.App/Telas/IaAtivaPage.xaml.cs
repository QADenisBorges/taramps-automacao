using BgsControle.Telas;

namespace BgsControle.App.Telas;

/// <summary>
/// Sem ‹: o Voltar do Android não fecha a IA ativa (ela fecha ao parar). O painel ao vivo usa os mesmos
/// controles da Mesa (o MesaViewModel), que mostram cada mudança da IA pelo espelho.
/// </summary>
public partial class IaAtivaPage : ContentPage
{
    public IaAtivaPage(IaAtivaViewModel vm, MesaViewModel mesa)
    {
        InitializeComponent();
        PainelMesa.BindingContext = mesa;
        BindingContext = vm;
    }

    protected override bool OnBackButtonPressed() => true;
}
