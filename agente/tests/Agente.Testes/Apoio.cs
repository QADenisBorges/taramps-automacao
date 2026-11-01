using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using Agente.Porta;
using Agente.Servico;

namespace Agente.Testes;

internal static class Caminhos
{
    /// <summary>A pasta <c>agente/</c> do repositório (a do <c>servico.config.json</c>).</summary>
    public static string Agente { get; } = PastaLocal.AcharRaiz(AppContext.BaseDirectory)
                                           ?? throw new InvalidOperationException("Não achei a pasta agente/.");

    public static string Roteiro(string nome) => Path.Combine(Agente, "testes", "roteiros", nome + ".json");

    /// <summary>A ferramenta <c>porta</c> do build (vem na pasta dos testes).</summary>
    public static string PortaDll => Path.Combine(AppContext.BaseDirectory, "porta.dll");

    public static ConfigServico Config => ConfigServico.Carregar(Path.Combine(Agente, ConfigServico.Arquivo));
}

/// <summary>Uma pasta <c>.local</c> só do teste (o pareamento, o estado e os logs não tocam a do notebook).</summary>
internal sealed class LocalTemporario : IDisposable
{
    public LocalTemporario()
    {
        Raiz = Path.Combine(Path.GetTempPath(), "agente-testes-" + Guid.NewGuid().ToString("N"));
        Local = Path.Combine(Raiz, ".local");
        Directory.CreateDirectory(Local);
    }

    public string Raiz { get; }

    public string Local { get; }

    public string? Ler(string arquivo)
    {
        var caminho = Path.Combine(Local, arquivo);
        return File.Exists(caminho) ? File.ReadAllText(caminho) : null;
    }

    public void Dispose()
    {
        for (var tentativa = 0; tentativa < 5; tentativa++)
        {
            try
            {
                Directory.Delete(Raiz, recursive: true);
                return;
            }
            catch (IOException)
            {
                Thread.Sleep(200); // um processo filho acabando de soltar o arquivo
            }
            catch (UnauthorizedAccessException)
            {
                Thread.Sleep(200);
            }
        }
    }
}

/// <summary>A ferramenta <c>porta</c> como processo, como o Agente a chama: o código de saída e a linha do stdout.</summary>
internal static class PortaCli
{
    private static readonly TimeSpan Prazo = TimeSpan.FromSeconds(90);

    public static async Task<(int Codigo, string Saida, JsonObject Json)> RodarAsync(string entrada, string pastaLocal)
    {
        var (codigo, cru) = await RodarCruAsync(["--json", entrada], null, pastaLocal);
        var linhas = cru.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Assert.Single(linhas); // sempre um JSON numa linha
        return (codigo, linhas[0], (JsonObject)JsonNode.Parse(linhas[0])!);
    }

    /// <summary>Os argumentos e o stdin dados: o código de saída e o stdout inteiro, sem mexer.</summary>
    public static async Task<(int Codigo, string Saida)> RodarCruAsync(IEnumerable<string> argumentos, string? stdin, string pastaLocal,
        string executavel = "porta.dll")
    {
        var inicio = Executavel.De(Path.Combine(AppContext.BaseDirectory, executavel));
        foreach (var a in argumentos)
            inicio.ArgumentList.Add(a);
        inicio.UseShellExecute = false;
        inicio.RedirectStandardInput = stdin is not null;
        if (stdin is not null)
            inicio.StandardInputEncoding = new UTF8Encoding(false);
        inicio.RedirectStandardOutput = true;
        inicio.RedirectStandardError = true;
        inicio.StandardOutputEncoding = Encoding.UTF8;
        inicio.Environment[PastaLocal.Variavel] = pastaLocal;
        using var p = Process.Start(inicio)!;
        if (stdin is not null)
        {
            await p.StandardInput.WriteAsync(stdin);
            p.StandardInput.Close();
        }
        var saida = await p.StandardOutput.ReadToEndAsync().WaitAsync(Prazo);
        await p.StandardError.ReadToEndAsync().WaitAsync(Prazo);
        await p.WaitForExitAsync().WaitAsync(Prazo);
        return (p.ExitCode, saida);
    }
}

/// <summary>O serviço rodando em segundo plano, com o log em memória e no arquivo.</summary>
internal sealed class ServicoRodando : IAsyncDisposable
{
    private readonly CancellationTokenSource _parar = new();
    private readonly Task _ciclo;

    public ServicoRodando(ConfigServico config, ILancadorAgente lancador, string pastaLocal)
    {
        Log = new LogServico(Path.Combine(pastaLocal, ServicoAgente.ArquivoLog));
        Servico = new ServicoAgente(config, lancador, Log, pastaLocal);
        _ciclo = Task.Run(() => Servico.RodarAsync(_parar.Token));
    }

    public ServicoAgente Servico { get; }

    public LogServico Log { get; }

    public bool Tem(string trecho) => Log.Linhas.Any(l => l.Contains(trecho, StringComparison.Ordinal));

    public int Quantas(string trecho) => Log.Linhas.Count(l => l.Contains(trecho, StringComparison.Ordinal));

    public Task EsperarLinha(string trecho, TimeSpan? prazo = null) =>
        Esperar.Ate(() => Tem(trecho), prazo ?? TimeSpan.FromSeconds(30), $"a linha \"{trecho}\" no log:\n{string.Join("\n", Log.Linhas)}");

    /// <summary>Para o serviço (e o Agente, se rodando) e espera o fim.</summary>
    public async Task PararAsync()
    {
        if (!_parar.IsCancellationRequested)
            await _parar.CancelAsync();
        await _ciclo.WaitAsync(TimeSpan.FromSeconds(120));
    }

    public async ValueTask DisposeAsync()
    {
        await PararAsync();
        Servico.Dispose();
        _parar.Dispose();
    }
}

/// <summary>Um lançador que só conta (o Agente termina na hora, com 0): as tarefas e os eventos que as acordaram.</summary>
internal sealed class LancadorQueConta : ILancadorAgente
{
    private readonly List<(string Tarefa, JsonObject? Evento)> _lancados = [];

    public IReadOnlyList<string> Tarefas
    {
        get
        {
            lock (_lancados)
                return _lancados.Select(l => l.Tarefa).ToArray();
        }
    }

    public IReadOnlyList<JsonObject?> Eventos
    {
        get
        {
            lock (_lancados)
                return _lancados.Select(l => l.Evento).ToArray();
        }
    }

    public Task<int> RodarAsync(string tarefa, JsonObject? evento, CancellationToken cancelar)
    {
        lock (_lancados)
            _lancados.Add((tarefa, evento));
        return Task.FromResult(0);
    }
}
