using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using Agente.Porta;

namespace Agente.Servico;

/// <summary>Quem roda o Agente numa tarefa: o Claude Code (<see cref="LancadorClaude"/>) ou o modelo falso.</summary>
public interface ILancadorAgente
{
    /// <summary>
    /// Roda o Agente na <paramref name="tarefa"/> até ele terminar e devolve o código de saída. O <paramref name="evento"/> é o
    /// evento do <c>/eventos</c> que acordou o Agente (null sem evento). O cancelamento (o prazo, ou o serviço parando)
    /// encerra o processo e lança <see cref="OperationCanceledException"/>.
    /// </summary>
    Task<int> RodarAsync(string tarefa, JsonObject? evento, CancellationToken cancelar);
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

    /// <summary>Monta o processo da tarefa, com o evento que acordou o Agente (sem o redirecionamento, que é desta classe).</summary>
    protected abstract ProcessStartInfo Montar(string tarefa, JsonObject? evento);

    public async Task<int> RodarAsync(string tarefa, JsonObject? evento, CancellationToken cancelar)
    {
        cancelar.ThrowIfCancellationRequested();
        var inicio = Montar(tarefa, evento);
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
/// <c>tarefas/&lt;tarefa&gt;.md</c> (e o evento que acordou o Agente), as ferramentas da tarefa e a ferramenta <c>porta</c>
/// no PATH.
/// <para>
/// O Agente começa zerado: ele roda numa pasta de trabalho fora do repositório (<see cref="PastaTrabalho"/>, dentro da pasta
/// local), para não carregar o CLAUDE.md nem a memória do projeto. Antes de cada lançamento, o serviço recria lá as
/// instruções em camadas de <c>agente/</c> (<see cref="CopiarCamadas"/>). Uma pasta de trabalho dentro do repositório é
/// recusada.
/// </para>
/// </summary>
public sealed class LancadorClaude(ConfigLancador config, string pastaAgente, string pastaLocal)
    : LancadorProcesso(Path.Combine(pastaLocal, "agente.log"))
{
    public const string MarcaPrompt = "{prompt}";
    public const string MarcaTarefa = "{tarefa}";
    public const string MarcaFerramentas = "{ferramentas}";
    public const string Trabalho = "trabalho";

    /// <summary>O título da seção do prompt com o JSON do evento que acordou o Agente.</summary>
    public const string TituloEvento = "O evento que acordou você";

    /// <summary>As pastas e o arquivo das camadas, em <c>agente/</c> (história 14d).</summary>
    public const string PastaTarefas = "tarefas";

    public const string PastaInstrucoes = "instrucoes";
    public const string ArquivoBase = "base.md";
    public const string PastaSkills = "skills";
    public const string PastaConhecimento = "conhecimento";

    /// <summary>Na pasta de trabalho, a base vira o <c>CLAUDE.md</c>, que o Claude Code carrega sozinho.</summary>
    public const string ClaudeMd = "CLAUDE.md";

    /// <summary>Na pasta de trabalho, as skills ficam em <c>.claude/skills</c>, onde carregam pela descrição.</summary>
    public static string SkillsNoTrabalho { get; } = Path.Combine(".claude", "skills");

    /// <summary>A pasta onde o Agente roda: <c>&lt;pasta local&gt;/trabalho</c>.</summary>
    public string PastaTrabalho { get; } = Path.Combine(Path.GetFullPath(pastaLocal), Trabalho);

    /// <summary>O texto da tarefa, de <c>&lt;base&gt;/tarefas/&lt;tarefa&gt;.md</c>.</summary>
    public static string Prompt(string pastaBase, string tarefa)
    {
        if (tarefa.Length == 0 || tarefa.Any(c => !(char.IsAsciiLetterOrDigit(c) || c == '-')))
            throw new ArgumentException($"Tarefa inválida: {tarefa}.", nameof(tarefa));
        var caminho = Path.Combine(pastaBase, PastaTarefas, tarefa + ".md");
        return File.Exists(caminho) ? File.ReadAllText(caminho) : throw new FileNotFoundException($"Falta a tarefa {caminho}.", caminho);
    }

    /// <summary>
    /// O prompt: o texto da tarefa e, com um evento, a seção <see cref="TituloEvento"/> com o JSON dele (a tarefa da ficha
    /// precisa do modelo e do <c>idEquipamento</c>, e o <c>/eventos</c> "desde o último" já não traz o evento que acordou o
    /// Agente). Sem evento, só a tarefa.
    /// </summary>
    public static string Prompt(string pastaBase, string tarefa, JsonObject? evento)
    {
        var texto = Prompt(pastaBase, tarefa);
        if (evento is null)
            return texto;
        return $"{texto.TrimEnd()}\n\n## {TituloEvento}\n\n```json\n{evento.ToJsonString(RespostaFerramenta.Formato)}\n```\n";
    }

    /// <summary>O processo que seria lançado para a tarefa (já com as camadas copiadas para a pasta de trabalho).</summary>
    public ProcessStartInfo InicioPara(string tarefa, JsonObject? evento = null) => Montar(tarefa, evento);

    protected override ProcessStartInfo Montar(string tarefa, JsonObject? evento)
    {
        if (RaizDoRepositorio(pastaAgente) is { } repositorio && Dentro(PastaTrabalho, repositorio))
            throw new InvalidOperationException(
                $"A pasta de trabalho do Agente ({PastaTrabalho}) fica dentro do repositório: ele carregaria os arquivos do projeto. Use outra pasta local.");
        CopiarCamadas();
        var prompt = Prompt(PastaTrabalho, tarefa, evento);
        var ferramentas = string.Join(' ', config.FerramentasDe(tarefa));
        var inicio = new ProcessStartInfo(config.Comando) { WorkingDirectory = PastaTrabalho };
        foreach (var a in config.Argumentos)
        {
            // O prompt entra por último: um "{tarefa}" ou "{ferramentas}" escrito pelo usuário (no modelo do evento) fica como está.
            inicio.ArgumentList.Add(a.Replace(MarcaTarefa, tarefa, StringComparison.Ordinal)
                .Replace(MarcaFerramentas, ferramentas, StringComparison.Ordinal)
                .Replace(MarcaPrompt, prompt, StringComparison.Ordinal));
        }
        if (!string.IsNullOrWhiteSpace(config.PastaPorta))
        {
            // Relativa à pasta local (o padrão "bin": onde o README manda publicar a porta).
            var pastaPorta = Path.GetFullPath(Path.Combine(pastaLocal, config.PastaPorta));
            inicio.Environment["PATH"] = pastaPorta + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH");
        }
        inicio.Environment[PastaLocal.Variavel] = Path.GetFullPath(pastaLocal);
        return inicio;
    }

    /// <summary>
    /// A cópia nova das camadas de <c>agente/</c> na pasta de trabalho (o que sobrou de antes nelas sai): as <c>tarefas/</c>, a
    /// base (<c>instrucoes/base.md</c>) como <c>CLAUDE.md</c>, as <c>skills/</c> em <c>.claude/skills/</c> e o
    /// <c>conhecimento/</c>.
    /// </summary>
    private void CopiarCamadas()
    {
        var baseMd = Path.Combine(pastaAgente, PastaInstrucoes, ArquivoBase);
        if (!File.Exists(baseMd))
            throw new FileNotFoundException($"Falta a base {baseMd}.", baseMd);
        Directory.CreateDirectory(PastaTrabalho);
        CopiarPasta(Path.Combine(pastaAgente, PastaTarefas), Path.Combine(PastaTrabalho, PastaTarefas));
        CopiarPasta(Path.Combine(pastaAgente, PastaSkills), Path.Combine(PastaTrabalho, SkillsNoTrabalho));
        CopiarPasta(Path.Combine(pastaAgente, PastaConhecimento), Path.Combine(PastaTrabalho, PastaConhecimento));
        File.Copy(baseMd, Path.Combine(PastaTrabalho, ClaudeMd), overwrite: true);
    }

    /// <summary>Recria <paramref name="destino"/> com os arquivos <c>.md</c> de <paramref name="origem"/> (e as subpastas).</summary>
    private static void CopiarPasta(string origem, string destino)
    {
        if (!Directory.Exists(origem))
            throw new DirectoryNotFoundException($"Falta a pasta {origem}.");
        if (Directory.Exists(destino))
            Directory.Delete(destino, recursive: true);
        Directory.CreateDirectory(destino);
        foreach (var arquivo in Directory.EnumerateFiles(origem, "*.md", SearchOption.AllDirectories))
        {
            var alvo = Path.Combine(destino, Path.GetRelativePath(origem, arquivo));
            Directory.CreateDirectory(Path.GetDirectoryName(alvo)!);
            File.Copy(arquivo, alvo);
        }
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
