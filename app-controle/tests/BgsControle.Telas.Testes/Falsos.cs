using System.Collections.Concurrent;
using System.Reflection;
using System.Threading.Channels;
using BgsControle.Nucleo;
using BgsControle.Nucleo.Seguranca;

namespace BgsControle.Telas.Testes;

/// <summary>
/// A thread da tela falsa: uma fila com um único consumidor, na ordem, como o MainThread. Quem
/// põe na fila volta logo (nunca bloqueia, para não travar com as travas da sessão).
/// </summary>
internal sealed class DespachanteFila : IDespachante
{
    private readonly Channel<Action> _fila = Channel.CreateUnbounded<Action>(new UnboundedChannelOptions { SingleReader = true });

    public DespachanteFila() => _ = Task.Run(RodarAsync);

    public ConcurrentQueue<Exception> Erros { get; } = new();

    public void Executar(Action acao) => _fila.Writer.TryWrite(acao);

    /// <summary>Espera a fila andar até aqui.</summary>
    public Task EsvaziarAsync()
    {
        var feito = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Executar(feito.SetResult);
        return feito.Task;
    }

    private async Task RodarAsync()
    {
        await foreach (var acao in _fila.Reader.ReadAllAsync())
        {
            try
            {
                acao();
            }
            catch (Exception e)
            {
                Erros.Enqueue(e);
            }
        }
    }
}

/// <summary>Os arquivos privados em memória.</summary>
internal sealed class ArquivosFalso : IArquivosApp
{
    private readonly ConcurrentDictionary<string, string> _arquivos = new();
    private int _gravacoes;

    public bool Falhar { get; set; }

    public int Gravacoes => Volatile.Read(ref _gravacoes);

    public string? Conteudo(string nome) => _arquivos.GetValueOrDefault(nome);

    public Task GravarTextoAsync(string nome, string conteudo)
    {
        if (Falhar)
            throw new IOException("disco cheio");
        _arquivos[nome] = conteudo;
        Interlocked.Increment(ref _gravacoes);
        return Task.CompletedTask;
    }

    public Task<string?> LerTextoAsync(string nome) => Task.FromResult(_arquivos.GetValueOrDefault(nome));
}

/// <summary>O preparo do Bluetooth: pronto (null) ou a mensagem do que falta.</summary>
internal sealed class PreparoFalso : IPreparoBluetooth
{
    public string? Falta { get; set; }
    public int Pedidos { get; private set; }

    public Task<string?> PrepararAsync()
    {
        Pedidos++;
        return Task.FromResult(Falta);
    }
}

internal sealed class CofreMemoria : ICofreSenha
{
    public string? Senha { get; set; }

    public Task<string?> LerAsync() => Task.FromResult(Senha);

    public Task GuardarAsync(string senha)
    {
        Senha = senha;
        return Task.CompletedTask;
    }

    public Task ApagarAsync()
    {
        Senha = null;
        return Task.CompletedTask;
    }
}

internal static class Apoio
{
    /// <summary>Espera a condição ficar verdadeira (até ~10 s).</summary>
    public static async Task Esperar(Func<bool> condicao, Func<string>? explicar = null)
    {
        for (var i = 0; i < 1000 && !condicao(); i++)
            await Task.Delay(10);
        Assert.True(condicao(), explicar?.Invoke() ?? "a condição não ficou verdadeira");
    }

    /// <summary>
    /// Dispara o evento <see cref="Sessao.Mudou"/> sem mudar o estado (a sessão só o dispara
    /// por dentro). Pelo campo do evento, por reflexão: só nos testes.
    /// </summary>
    public static void DispararMudou(Sessao sessao)
    {
        var campo = typeof(Sessao).GetField(nameof(Sessao.Mudou), BindingFlags.Instance | BindingFlags.NonPublic)
                    ?? throw new InvalidOperationException("O campo do evento Sessao.Mudou mudou de nome.");
        ((Action?)campo.GetValue(sessao))?.Invoke();
    }
}
