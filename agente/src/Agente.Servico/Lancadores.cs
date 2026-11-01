using System.Diagnostics;
using System.Text;
using Agente.Porta;

namespace Agente.Servico;

/// <summary>Quem roda o Agente numa tarefa: o Claude Code (<see cref="LancadorClaude"/>) ou o modelo falso.</summary>
public interface ILancadorAgente
{
    /// <summary>
    /// Roda o Agente na <paramref name="tarefa"/> até ele terminar e devolve o código de saída. O cancelamento (o prazo, ou o
    /// serviço parando) encerra o processo e lança <see cref="OperationCanceledException"/>.
    /// </summary>
    Task<int> RodarAsync(string tarefa, CancellationToken cancelar);
}

/// <summary>
/// Um Agente que é um processo: a saída dele (stdout e stderr) vai para um arquivo de <c>.local/</c>; no cancelamento, a
/// árvore inteira do processo é encerrada.
/// </summary>
public abstract class LancadorProcesso(string? arquivoSaida) : ILancadorAgente
{
    private static readonly TimeSpan EsperaFim = TimeSpan.FromSeconds(10);
    private readonly object _trava = new();

    /// <summary>O id do último processo lançado (para conferir que o prazo o encerrou mesmo).</summary>
    public int? UltimoPid { get; private set; }

    /// <summary>Monta o processo da tarefa (sem o redirecionamento, que é desta classe).</summary>
    protected abstract ProcessStartInfo Montar(string tarefa);

    public async Task<int> RodarAsync(string tarefa, CancellationToken cancelar)
    {
        cancelar.ThrowIfCancellationRequested();
        var inicio = Montar(tarefa);
        inicio.UseShellExecute = false;
        inicio.RedirectStandardOutput = true;
        inicio.RedirectStandardError = true;
        inicio.StandardOutputEncoding = Encoding.UTF8;
        inicio.StandardErrorEncoding = Encoding.UTF8;

        using var processo = new Process { StartInfo = inicio };
        processo.OutputDataReceived += (_, e) => Anotar(e.Data);
        processo.ErrorDataReceived += (_, e) => Anotar(e.Data);
        if (!processo.Start())
            throw new InvalidOperationException($"Não consegui iniciar {inicio.FileName}.");
        UltimoPid = processo.Id;
        processo.BeginOutputReadLine();
        processo.BeginErrorReadLine();
        try
        {
            await processo.WaitForExitAsync(cancelar);
        }
        catch (OperationCanceledException)
        {
            Encerrar(processo);
            throw;
        }
        await processo.WaitForExitAsync(CancellationToken.None); // a saída termina de chegar
        return processo.ExitCode;
    }

    private static void Encerrar(Process processo)
    {
        try
        {
            if (!processo.HasExited)
                processo.Kill(entireProcessTree: true);
        }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
        {
            // já terminou
        }
        try
        {
            processo.WaitForExit(EsperaFim);
        }
        catch (InvalidOperationException)
        {
            // sem processo
        }
    }

    private void Anotar(string? linha)
    {
        if (linha is null || arquivoSaida is null)
            return;
        lock (_trava)
            ArquivoLog.Anexar(arquivoSaida, $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss} {linha}");
    }
}

/// <summary>Como rodar um executável .NET desta solução: o apphost, se houver, ou <c>dotnet arquivo.dll</c>.</summary>
public static class Executavel
{
    public static ProcessStartInfo De(string dll)
    {
        var apphost = Path.ChangeExtension(dll, OperatingSystem.IsWindows() ? ".exe" : null);
        if (File.Exists(apphost))
            return new ProcessStartInfo(apphost);
        var inicio = new ProcessStartInfo(Dotnet());
        inicio.ArgumentList.Add(dll);
        return inicio;
    }

    /// <summary>O <c>dotnet</c> que roda este processo (ou o do PATH).</summary>
    public static string Dotnet()
    {
        var host = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
        if (!string.IsNullOrEmpty(host) && File.Exists(host))
            return host;
        var atual = Environment.ProcessPath;
        if (atual is not null && Path.GetFileNameWithoutExtension(atual).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            return atual;
        return "dotnet";
    }
}

/// <summary>
/// O lançador de verdade: o comando do <c>servico.config.json</c> (o Claude Code em modo não interativo), com o prompt de
/// <c>tarefas/&lt;tarefa&gt;.md</c> e a ferramenta <c>porta</c> no PATH.
/// <para>
/// O Agente começa zerado: ele roda numa pasta de trabalho fora do repositório (<see cref="PastaTrabalho"/>, dentro da pasta
/// local), para não carregar o CLAUDE.md nem a memória do projeto. Antes de cada lançamento, o serviço copia
/// <c>agente/tarefas/</c> para lá. Uma pasta de trabalho dentro do repositório é recusada.
/// </para>
/// </summary>
public sealed class LancadorClaude(ConfigLancador config, string pastaAgente, string pastaLocal)
    : LancadorProcesso(Path.Combine(pastaLocal, "agente.log"))
{
    public const string MarcaPrompt = "{prompt}";
    public const string MarcaTarefa = "{tarefa}";
    public const string Trabalho = "trabalho";

    /// <summary>A pasta onde o Agente roda: <c>&lt;pasta local&gt;/trabalho</c>.</summary>
    public string PastaTrabalho { get; } = Path.Combine(Path.GetFullPath(pastaLocal), Trabalho);

    /// <summary>O texto da tarefa, de <c>&lt;base&gt;/tarefas/&lt;tarefa&gt;.md</c> (o esqueleto da 14a; as skills são da 14d).</summary>
    public static string Prompt(string pastaBase, string tarefa)
    {
        if (tarefa.Length == 0 || tarefa.Any(c => !(char.IsAsciiLetterOrDigit(c) || c == '-')))
            throw new ArgumentException($"Tarefa inválida: {tarefa}.", nameof(tarefa));
        var caminho = Path.Combine(pastaBase, "tarefas", tarefa + ".md");
        return File.Exists(caminho) ? File.ReadAllText(caminho) : throw new FileNotFoundException($"Falta a tarefa {caminho}.", caminho);
    }

    /// <summary>O processo que seria lançado para a tarefa (já com as tarefas copiadas para a pasta de trabalho).</summary>
    public ProcessStartInfo InicioPara(string tarefa) => Montar(tarefa);

    protected override ProcessStartInfo Montar(string tarefa)
    {
        if (RaizDoRepositorio(pastaAgente) is { } repositorio && Dentro(PastaTrabalho, repositorio))
            throw new InvalidOperationException(
                $"A pasta de trabalho do Agente ({PastaTrabalho}) fica dentro do repositório: ele carregaria os arquivos do projeto. Use outra pasta local.");
        CopiarTarefas();
        var prompt = Prompt(PastaTrabalho, tarefa);
        var inicio = new ProcessStartInfo(config.Comando) { WorkingDirectory = PastaTrabalho };
        foreach (var a in config.Argumentos)
            inicio.ArgumentList.Add(a.Replace(MarcaPrompt, prompt, StringComparison.Ordinal).Replace(MarcaTarefa, tarefa, StringComparison.Ordinal));
        if (!string.IsNullOrWhiteSpace(config.PastaPorta))
        {
            // Relativa à pasta local (o padrão "bin": onde o README manda publicar a porta).
            var pastaPorta = Path.GetFullPath(Path.Combine(pastaLocal, config.PastaPorta));
            inicio.Environment["PATH"] = pastaPorta + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH");
        }
        inicio.Environment[PastaLocal.Variavel] = Path.GetFullPath(pastaLocal);
        return inicio;
    }

    /// <summary>A cópia nova de <c>agente/tarefas/</c> na pasta de trabalho (a antiga sai).</summary>
    private void CopiarTarefas()
    {
        var origem = Path.Combine(pastaAgente, "tarefas");
        var destino = Path.Combine(PastaTrabalho, "tarefas");
        if (Directory.Exists(destino))
            Directory.Delete(destino, recursive: true);
        Directory.CreateDirectory(destino);
        foreach (var arquivo in Directory.EnumerateFiles(origem, "*.md"))
            File.Copy(arquivo, Path.Combine(destino, Path.GetFileName(arquivo)));
    }

    /// <summary>A raiz do repositório git que contém a pasta (a que tem <c>.git</c>), ou null.</summary>
    public static string? RaizDoRepositorio(string pasta)
    {
        for (var d = new DirectoryInfo(Path.GetFullPath(pasta)); d is not null; d = d.Parent)
        {
            if (Directory.Exists(Path.Combine(d.FullName, ".git")) || File.Exists(Path.Combine(d.FullName, ".git")))
                return d.FullName;
        }
        return null;
    }

    private static bool Dentro(string pasta, string raiz)
    {
        var comparacao = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var p = Path.TrimEndingDirectorySeparator(Path.GetFullPath(pasta)) + Path.DirectorySeparatorChar;
        var r = Path.TrimEndingDirectorySeparator(Path.GetFullPath(raiz)) + Path.DirectorySeparatorChar;
        return p.StartsWith(r, comparacao);
    }
}
