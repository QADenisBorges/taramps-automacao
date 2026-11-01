using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Agente.Porta;

/// <summary>
/// A saída da ferramenta da porta, sempre um JSON numa linha: <c>{"ok":true,"status":200,"corpo":{...}}</c> ou
/// <c>{"ok":false,"status":409,"motivo":"..."}</c>. Sem resposta da porta (ou sem pareamento), não há <c>status</c>.
/// </summary>
public sealed record RespostaFerramenta(bool Ok, int? Status, JsonNode? Corpo, string? Motivo)
{
    /// <summary>Os acentos saem como texto (a saída é UTF-8), numa linha só.</summary>
    public static JsonSerializerOptions Formato { get; } = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false,
    };

    public static RespostaFerramenta Feito(int status, JsonNode? corpo) => new(true, status, corpo, null);

    public static RespostaFerramenta Recusa(string motivo, int? status = null) => new(false, status, null, motivo);

    /// <summary>O código de saída do executável: 0 no sucesso, 1 na recusa ou erro.</summary>
    public int CodigoSaida => Ok ? 0 : 1;

    public JsonObject ParaObjeto()
    {
        var o = new JsonObject { ["ok"] = Ok };
        if (Status is { } status)
            o["status"] = status;
        if (Ok)
        {
            if (Corpo is not null)
                o["corpo"] = Corpo.DeepClone();
        }
        else
        {
            o["motivo"] = Motivo;
        }
        return o;
    }

    public string ParaJson() => ParaObjeto().ToJsonString(Formato);

    /// <summary>Lê a saída da ferramenta (para quem a chama como processo).</summary>
    public static RespostaFerramenta DeJson(string json)
    {
        var o = JsonNode.Parse(json) as JsonObject ?? throw new JsonException("A saída da porta não é um objeto JSON.");
        var ok = o["ok"] is JsonValue v && v.TryGetValue<bool>(out var b) && b;
        int? status = o["status"] is JsonValue s && s.TryGetValue<int>(out var n) ? n : null;
        var motivo = o["motivo"] is JsonValue m && m.TryGetValue<string>(out var t) ? t : null;
        return new RespostaFerramenta(ok, status, o["corpo"]?.DeepClone(), motivo);
    }
}
