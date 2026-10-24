using BgsControle.Telas;

namespace BgsControle.App.Telas;

/// <summary>Navegação: o Voltar do Android não fecha; sai-se por Terminar ou Voltar à cópia.</summary>
public partial class PelaMetadePage : ContentPage
{
    public PelaMetadePage(MemoriasViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override bool OnBackButtonPressed() => true;
}
