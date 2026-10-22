using BgsControle.Telas;

namespace BgsControle.App;

/// <summary>A thread da tela do MAUI.</summary>
public sealed class DespachanteMaui : IDespachante
{
    public void Executar(Action acao) => MainThread.BeginInvokeOnMainThread(acao);
}
