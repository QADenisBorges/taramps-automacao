using BgsControle.Telas;

namespace BgsControle.App.Telas;

/// <summary>
/// Navegação (‹ e Voltar) e o Avançado recolhido (estado só da tela). Sair da tela não desliga o gerador: a
/// contagem de 30 s segue, e ele desliga sozinho no prazo, no Parar ou ao fechar o app.
/// </summary>
public partial class GeradorPage : ContentPage
{
    public GeradorPage(GeradorViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    private void AoVoltar(object? sender, EventArgs e) => _ = Navigation.PopAsync();

    private void AoAlternarAvancado(object? sender, TappedEventArgs e)
    {
        Avancado.IsVisible = !Avancado.IsVisible;
        SetaAvancado.Text = Avancado.IsVisible ? "⌃" : "⌄";
    }
}
