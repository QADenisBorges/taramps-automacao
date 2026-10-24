using BgsControle.Telas;

namespace BgsControle.App.Telas;

/// <summary>O Voltar do Android não fecha a faixa: ela sai sozinha quando o gerador é lido desligado.</summary>
public partial class AlarmeGeradorPage : ContentPage
{
    public AlarmeGeradorPage(CascaViewModel casca)
    {
        InitializeComponent();
        BindingContext = casca;
    }

    protected override bool OnBackButtonPressed() => true;
}
