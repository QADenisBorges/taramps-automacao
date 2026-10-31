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

    /// <summary>Põe um arquivo como se já estivesse no celular (não conta como gravação).</summary>
    public void Semear(string nome, string conteudo) => _arquivos[nome] = conteudo;

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

/// <summary>A navegação falsa: guarda cada pedido, na ordem (história 10).</summary>
internal sealed class NavegacaoFalsa : INavegacao
{
    public List<string> Pedidos { get; } = [];

    /// <summary>O último pedido de "por cima": (escolha do Setup, pela metade).</summary>
    public (bool Setup, bool Metade)? PorCima { get; private set; }

    public void AbrirAjuste(int canal) => Pedidos.Add($"ajuste:CH{canal + 1}");
    public void AbrirSetup(string idSetup) => Pedidos.Add($"setup:{idSetup}");
    public void AbrirFuncao(FuncaoAudio funcao) => Pedidos.Add($"funcao:{funcao}");
    public void AbrirConfiguracoes() => Pedidos.Add("configuracoes");
    public void AbrirSetups() => Pedidos.Add("setups");
    public void AbrirMemorias() => Pedidos.Add("memorias");
    public void AbrirConexao() => Pedidos.Add("conexao");
    public void AbrirGerador() => Pedidos.Add("gerador");
    public void AbrirPresets() => Pedidos.Add("presets");
    public void AbrirSenha() => Pedidos.Add("senha");
    public void AbrirMonitoracao() => Pedidos.Add("monitoracao");
    public void AbrirChaves() => Pedidos.Add("chaves");

    /// <summary>O último pedido das camadas: (criação da memória, folha de risco, "REAPLICANDO", alarme do gerador).</summary>
    public (bool Criacao, bool FolhaRisco, bool Reaplicando, bool Alarme)? Camadas { get; private set; }

    public void MostrarCamadas(bool criacao, bool folhaRisco, bool reaplicando, bool alarmeGerador)
    {
        Camadas = (criacao, folhaRisco, reaplicando, alarmeGerador);
        Pedidos.Add($"camadas:{criacao}:{folhaRisco}:{reaplicando}:{alarmeGerador}");
    }

    public void MostrarPorCima(bool escolhaSetup, bool pelaMetade)
    {
        PorCima = (escolhaSetup, pelaMetade);
        Pedidos.Add($"por-cima:{escolhaSetup}:{pelaMetade}");
    }

    public void Voltar() => Pedidos.Add("voltar");
    public void VoltarAoInicio() => Pedidos.Add("inicio");
}

/// <summary>O som do celular falso (história 11): a saída do som e o acesso às músicas, mudados pelo teste.</summary>
internal sealed class AudioDoCelularFalso : IAudioDoCelular
{
    private readonly HashSet<string> _semAcesso = new(StringComparer.Ordinal);
    private bool? _bluetooth = true;

    public bool? SaindoPeloBluetooth
    {
        get => _bluetooth;
        set
        {
            _bluetooth = value;
            Mudou?.Invoke();
        }
    }

    public bool SemAcesso(string uri)
    {
        lock (_semAcesso)
            return _semAcesso.Contains(uri);
    }

    /// <summary>O reprodutor de verdade marca; aqui, o teste.</summary>
    public void MarcarSemAcesso(string uri)
    {
        lock (_semAcesso)
            _semAcesso.Add(uri);
        Mudou?.Invoke();
    }

    public void LimparSemAcesso()
    {
        lock (_semAcesso)
            _semAcesso.Clear();
        Limpezas++;
        Mudou?.Invoke();
    }

    public int Limpezas { get; private set; }

    public int AberturasBluetooth { get; private set; }

    public event Action? Mudou;

    public void AbrirBluetooth() => AberturasBluetooth++;
}

/// <summary>O exportar falso: guarda o nome e o conteúdo; <see cref="Falha"/> faz recusar.</summary>
internal sealed class ExportarFalso : IExportarArquivo
{
    public List<(string Nome, string Conteudo)> Exportados { get; } = [];

    public string? Falha { get; set; }

    public Task<string?> ExportarAsync(string nomeArquivo, string conteudo)
    {
        if (Falha is null)
            Exportados.Add((nomeArquivo, conteudo));
        return Task.FromResult(Falha);
    }
}
