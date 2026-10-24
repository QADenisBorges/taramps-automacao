using BgsControle.Telas;

namespace BgsControle.App.Telas;

/// <summary>Só o arrastar da alça ≡ (modo Editar): soltar sobre outra faixa move a arrastada para a posição dela.</summary>
public partial class PlayerView : ContentView
{
    private const string Chave = "posicao";

    public PlayerView() => InitializeComponent();

    private void AoArrastar(object? sender, DragStartingEventArgs e)
    {
        if (Linha(sender) is { } linha)
            e.Data.Properties[Chave] = linha.Posicao;
    }

    /// <summary>A linha do gesto: pelo reconhecedor (ou pela view dele).</summary>
    private static LinhaPlaylistViewModel? Linha(object? sender) => sender switch
    {
        GestureRecognizer { BindingContext: LinhaPlaylistViewModel l } => l,
        GestureRecognizer { Parent.BindingContext: LinhaPlaylistViewModel l } => l,
        Element { BindingContext: LinhaPlaylistViewModel l } => l,
        _ => null,
    };

    private async void AoSoltar(object? sender, DropEventArgs e)
    {
        if (BindingContext is not PlayerViewModel tela
            || Linha(sender) is not { } alvo
            || !e.Data.Properties.TryGetValue(Chave, out var valor) || valor is not int de)
            return;
        e.Handled = true;
        try
        {
            await tela.MoverAsync(de, alvo.Posicao);
        }
        catch (Exception)
        {
            // Mover nunca derruba o app; a lista fica como estava.
        }
    }
}
