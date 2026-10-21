using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BgsControle.Telas;

/// <summary>
/// A base dos ViewModels: o aviso de propriedade mudada, a thread da tela e o relógio dos
/// prazos (o <see cref="TimeProvider"/> deixa os testes usarem um tempo falso).
/// </summary>
public abstract class TelaBase : INotifyPropertyChanged
{
    protected TelaBase(IDespachante despachante, TimeProvider relogio)
    {
        Despachante = despachante;
        Relogio = relogio;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected IDespachante Despachante { get; }

    /// <summary>O relógio dos prazos da tela.</summary>
    protected TimeProvider Relogio { get; }

    /// <summary>Põe a ação na thread da tela.</summary>
    protected void NaTela(Action acao) => Despachante.Executar(acao);

    protected void Avisar([CallerMemberName] string? nome = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nome));
}
