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
        var inicio = Executavel.De(caminhoPorta);
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
            return RespostaFerramenta.Recusa($"Não consegui rodar a porta ({caminhoPorta}): {e.Message}");
        }
        if (iniciado is null)
            return RespostaFerramenta.Recusa($"Não consegui rodar a porta ({caminhoPorta}).");
        using var processo = iniciado;
        var saida = processo.StandardOutput.ReadToEndAsync(cancelar);
        var erro = processo.StandardError.ReadToEndAsync(cancelar);
        try
        {
            await processo.WaitForExitAsync(cancelar);
            var texto = (await saida).Trim();
            var textoErro = (await erro).Trim();
            try
            {
                return RespostaFerramenta.DeJson(texto);
            }
            catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException)
            {
                var detalhe = textoErro.Length > 500 ? textoErro[..500] + "…" : textoErro;
                return RespostaFerramenta.Recusa($"A porta devolveu uma saída que não é JSON (código {processo.ExitCode})."
                                                 + (detalhe.Length > 0 ? $" Erro: {detalhe}" : ""));
            }
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
