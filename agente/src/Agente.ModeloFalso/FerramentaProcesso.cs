using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Agente.Porta;
using Agente.Servico;

namespace Agente.ModeloFalso;

/// <summary>
/// A ferramenta da porta como processo: o executável <c>porta</c> com <c>--json '&lt;entrada&gt;'</c>, como o Claude Code o
/// chama. A saída é a linha JSON do stdout.
/// </summary>
public sealed class FerramentaProcesso(string caminhoPorta, string? pastaLocal = null) : IFerramenta
{
    /// <summary>O <c>porta.dll</c> que vem na mesma pasta deste executável (o projeto referencia o Porta.Cli).</summary>
    public static string PortaAoLado => Path.Combine(AppContext.BaseDirectory, "porta.dll");

    public async Task<RespostaFerramenta> RodarAsync(JsonObject entrada, CancellationToken cancelar)
    {
        var (saida, falha) = await ProcessoJson.RodarAsync(caminhoPorta, "porta", entrada, pastaLocal, cancelar);
        if (falha is not null)
            return RespostaFerramenta.Recusa(falha);
        try
        {
            return RespostaFerramenta.DeJson(saida!.Texto);
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException)
        {
            return RespostaFerramenta.Recusa(ProcessoJson.MsgNaoJson("porta", saida!));
        }
    }
}

/// <summary>
/// A ferramenta do aprendizado como processo (história 14e): o executável <c>aprendizado</c> com
/// <c>--json '&lt;entrada&gt;'</c>, como o Claude Code o chama. A saída é a linha JSON do stdout, com o <c>ok</c>.
/// </summary>
public sealed class AprendizadoProcesso(string caminhoAprendizado, string? pastaLocal = null) : IFerramentaAprendizado
{
    /// <summary>O <c>aprendizado.dll</c> que vem na mesma pasta deste executável (o projeto referencia o Aprendizado.Cli).</summary>
    public static string AoLado => Path.Combine(AppContext.BaseDirectory, "aprendizado.dll");

    public async Task<JsonObject> RodarAsync(JsonObject entrada, CancellationToken cancelar)
    {
        var (saida, falha) = await ProcessoJson.RodarAsync(caminhoAprendizado, "aprendizado", entrada, pastaLocal, cancelar);
        if (falha is not null)
            return Recusa(falha);
        try
        {
            if (JsonNode.Parse(saida!.Texto) is JsonObject o && o["ok"] is JsonValue v && v.GetValueKind() is JsonValueKind.True or JsonValueKind.False)
                return o;
        }
        catch (JsonException)
        {
            // cai na recusa abaixo
        }
        return Recusa(ProcessoJson.MsgNaoJson("aprendizado", saida!));

        static JsonObject Recusa(string motivo) => new() { ["ok"] = false, ["motivo"] = motivo };
    }
}

/// <summary>Roda um executável desta solução com <c>--json '&lt;entrada&gt;'</c> e devolve o stdout, o stderr e o código.</summary>
internal static class ProcessoJson
{
    public sealed record Saida(string Texto, string Erro, int Codigo);

    public static string MsgNaoJson(string nome, Saida saida)
    {
        var detalhe = saida.Erro.Length > 500 ? saida.Erro[..500] + "…" : saida.Erro;
        return $"A {nome} devolveu uma saída que não é JSON (código {saida.Codigo})." + (detalhe.Length > 0 ? $" Erro: {detalhe}" : "");
    }

    /// <summary>A saída, ou a falha ao iniciar o processo. O cancelamento encerra o processo e lança.</summary>
    public static async Task<(Saida? Saida, string? Falha)> RodarAsync(string dll, string nome, JsonObject entrada, string? pastaLocal,
        CancellationToken cancelar)
    {
        var inicio = Executavel.De(dll);
        inicio.ArgumentList.Add("--json");
        inicio.ArgumentList.Add(entrada.ToJsonString(RespostaFerramenta.Formato));
        inicio.UseShellExecute = false;
        inicio.RedirectStandardOutput = true;
        inicio.RedirectStandardError = true;
        inicio.StandardOutputEncoding = Encoding.UTF8;
        inicio.StandardErrorEncoding = Encoding.UTF8;
        if (pastaLocal is not null)
            inicio.Environment[PastaLocal.Variavel] = pastaLocal;

        Process? iniciado;
        try
        {
            iniciado = Process.Start(inicio);
        }
        catch (Exception e) when (e is Win32Exception or InvalidOperationException or IOException)
        {
            return (null, $"Não consegui rodar a {nome} ({dll}): {e.Message}");
        }
        if (iniciado is null)
            return (null, $"Não consegui rodar a {nome} ({dll}).");
        using var processo = iniciado;
        var saida = processo.StandardOutput.ReadToEndAsync(cancelar);
        var erro = processo.StandardError.ReadToEndAsync(cancelar);
        try
        {
            await processo.WaitForExitAsync(cancelar);
            return (new Saida((await saida).Trim(), (await erro).Trim(), processo.ExitCode), null);
        }
        catch (OperationCanceledException)
        {
            try
            {
                processo.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // já terminou
            }
            throw;
        }
    }
}
