using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Agente.Porta;

/// <summary>
/// O cliente HTTP da porta de comandos do app. Cada pedido espera até <see cref="PrazoPadrao"/> (70 s: cabe a folha de
/// risco de 60 s). A resposta do app vira a da ferramenta: 2xx com <c>ok:true</c> é sucesso, com o JSON inteiro no
/// <c>corpo</c>; o resto é recusa, com o <c>motivo</c> do app.
/// </summary>
public sealed class ClientePorta : IDisposable
{
    public static TimeSpan PrazoPadrao { get; } = TimeSpan.FromSeconds(70);

    private readonly HttpClient _http;
    private readonly TimeSpan _prazo;

    public ClientePorta(TimeSpan? prazo = null, HttpMessageHandler? manipulador = null)
    {
        _prazo = prazo ?? PrazoPadrao;
        _http = manipulador is null ? new HttpClient() : new HttpClient(manipulador);
        _http.Timeout = Timeout.InfiniteTimeSpan; // o prazo é o deste cliente, por pedido
    }

    public static string MsgSemResposta(string endereco) => $"A porta não respondeu em {endereco}.";

    public static string MsgPrazo(TimeSpan prazo) => $"A porta não respondeu em {prazo.TotalSeconds:0} s.";

    /// <summary>Um pedido com a chave do cliente pareado.</summary>
    public Task<RespostaFerramenta> PedirAsync(ClientePareado cliente, string metodo, string alvo, JsonNode? corpo, CancellationToken cancelar = default) =>
        EnviarAsync(cliente.Endereco, cliente.Chave, metodo, alvo, corpo, cancelar);

    /// <summary>O pareamento: devolve a resposta (sem a chave) e a chave, se o app a deu.</summary>
    public async Task<(RespostaFerramenta Resposta, string? Chave)> ParearAsync(string endereco, string codigo, string nome, CancellationToken cancelar = default)
    {
        var corpo = new JsonObject { ["codigo"] = codigo, ["nome"] = nome };
        var r = await EnviarAsync(endereco, null, "POST", "/parear", corpo, cancelar);
        if (!r.Ok)
            return (r, null);
        var chave = (r.Corpo as JsonObject)?["chave"] is JsonValue c && c.TryGetValue<string>(out var t) ? t : null;
        if (string.IsNullOrWhiteSpace(chave))
            return (RespostaFerramenta.Recusa("A porta não devolveu a chave.", r.Status), null);
        return (RespostaFerramenta.Feito(r.Status ?? 200, null), chave);
    }

    private async Task<RespostaFerramenta> EnviarAsync(string endereco, string? chave, string metodo, string alvo, JsonNode? corpo, CancellationToken cancelar)
    {
        using var pedido = new HttpRequestMessage(new HttpMethod(metodo), new Uri($"http://{endereco}{alvo}"));
        if (chave is not null)
            pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", chave);
        if (corpo is not null)
            pedido.Content = new StringContent(corpo.ToJsonString(), Encoding.UTF8, "application/json");

        using var prazo = CancellationTokenSource.CreateLinkedTokenSource(cancelar);
        prazo.CancelAfter(_prazo);
        HttpResponseMessage? resposta = null;
        try
        {
            resposta = await _http.SendAsync(pedido, prazo.Token);
            var texto = await resposta.Content.ReadAsStringAsync(prazo.Token);
            return Interpretar((int)resposta.StatusCode, resposta.IsSuccessStatusCode, texto);
        }
        catch (OperationCanceledException) when (!cancelar.IsCancellationRequested)
        {
            return RespostaFerramenta.Recusa(MsgPrazo(_prazo));
        }
        catch (HttpRequestException)
        {
            return RespostaFerramenta.Recusa(MsgSemResposta(endereco));
        }
        finally
        {
            resposta?.Dispose();
        }
    }

    private static RespostaFerramenta Interpretar(int status, bool sucesso, string texto)
    {
        JsonNode? json = null;
        try
        {
            json = string.IsNullOrWhiteSpace(texto) ? null : JsonNode.Parse(texto);
        }
        catch (JsonException)
        {
            // não é JSON: fica o status
        }
        var ok = json is JsonObject o && o["ok"] is JsonValue v && v.TryGetValue<bool>(out var b) && b;
        if (sucesso && ok)
            return RespostaFerramenta.Feito(status, json);
        var motivo = (json as JsonObject)?["motivo"] is JsonValue m && m.TryGetValue<string>(out var s) ? s : null;
        return RespostaFerramenta.Recusa(motivo ?? $"A porta respondeu {status}.", status);
    }

    public void Dispose() => _http.Dispose();
}
