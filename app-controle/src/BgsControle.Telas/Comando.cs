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

/// <summary>
/// Um comando da tela com parâmetro (a opção escolhida, o valor do fader). Não roda duas vezes em
/// paralelo: um toque que chega enquanto o anterior ainda corre é ignorado (o duplo toque). Uma exceção
/// não sai pelo <see cref="Execute"/> (que é <c>async void</c> e derrubaria o app): fica em
/// <see cref="UltimoErro"/> e vai para o <see cref="Falhou"/>.
/// </summary>
public sealed class ComandoCom<T> : ICommand
{
    private readonly Func<T, Task> _acao;
    private int _rodando;

    public ComandoCom(Func<T, Task> acao) => _acao = acao;

    public ComandoCom(Action<T> acao) : this(v =>
    {
        acao(v);
        return Task.CompletedTask;
    })
    {
    }

    /// <summary>Uma exceção da ação (o app pode registrar).</summary>
    public static event Action<Exception>? Falhou;

    /// <summary>A última exceção da ação, ou null.</summary>
    public Exception? UltimoErro { get; private set; }

    /// <summary>A ação está rodando (um toque agora é ignorado).</summary>
    public bool Rodando => Volatile.Read(ref _rodando) == 1;

    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter) => true;

    public async void Execute(object? parameter)
    {
        if (Converter(parameter, out var v))
            await ExecutarAsync(v);
    }

    /// <summary>Executa e espera o fim (testes). Com a ação já rodando, não faz nada.</summary>
    public async Task ExecutarAsync(T valor)
    {
        if (Interlocked.Exchange(ref _rodando, 1) == 1)
            return;
        try
        {
            await _acao(valor);
        }
        catch (Exception e)
        {
            UltimoErro = e;
            Falhou?.Invoke(e);
        }
        finally
        {
            Volatile.Write(ref _rodando, 0);
        }
    }

    private static bool Converter(object? parameter, out T valor)
    {
        switch (parameter)
        {
            case T v:
                valor = v;
                return true;
            case IConvertible c when typeof(T).IsPrimitive || typeof(T) == typeof(decimal):
                try
                {
                    valor = (T)Convert.ChangeType(c, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
                    return true;
                }
                catch (Exception e) when (e is FormatException or InvalidCastException or OverflowException)
                {
                    valor = default!;
                    return false;
                }
            default:
                valor = default!;
                return false;
        }
    }
}
