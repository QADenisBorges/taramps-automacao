using System.Windows.Input;

namespace BgsControle.Telas;

/// <summary>
/// Um comando da tela sem MAUI. Sempre pode executar, como o <c>Command</c> do MAUI sem
/// condição. <see cref="Execute"/> dispara e não espera (uma exceção derruba o app, como no
/// MAUI); os testes chamam <see cref="ExecutarAsync"/> e esperam o fim.
/// </summary>
public sealed class Comando : ICommand
{
    private readonly Func<Task> _acao;

    public Comando(Func<Task> acao) => _acao = acao;

    public Comando(Action acao) : this(() =>
    {
        acao();
        return Task.CompletedTask;
    })
    {
    }

    /// <summary>Nunca muda: o comando sempre pode executar.</summary>
    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter) => true;

    public async void Execute(object? parameter) => await _acao();

    /// <summary>Executa e espera o fim.</summary>
    public Task ExecutarAsync() => _acao();
}
