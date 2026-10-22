using System.Text;
using System.Text.Json;

namespace BgsControle.Nucleo.Porta;

/// <summary>Os JSON da porta (UTF-8, acentos legíveis): <c>{"ok":true,...}</c> ou <c>{"ok":false,"motivo"}</c>.</summary>
internal static class JsonPorta
{
    private static readonly JsonWriterOptions Opcoes = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string Recusa(string motivo) => Objeto(w =>
    {
        w.WriteBoolean("ok", false);
        w.WriteString("motivo", motivo);
    });

    public static string Ok(Action<Utf8JsonWriter>? campos = null) => Objeto(w =>
    {
        w.WriteBoolean("ok", true);
        campos?.Invoke(w);
    });

    private static string Objeto(Action<Utf8JsonWriter> corpo)
    {
        using var fluxo = new MemoryStream();
        using (var w = new Utf8JsonWriter(fluxo, Opcoes))
        {
            w.WriteStartObject();
            corpo(w);
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(fluxo.ToArray());
    }
}

/// <summary>Um campo do corpo que falta ou está errado: vira 400.</summary>
internal sealed class PedidoInvalidoException(string motivo) : Exception(motivo);

/// <summary>O corpo JSON de um pedido, com a leitura de cada campo (um erro vira 400).</summary>
internal sealed class CorpoPedido
{
    private readonly JsonElement? _raiz;

    private CorpoPedido(JsonElement? raiz) => _raiz = raiz;

    /// <summary>Lê o corpo: vazio = sem campos; senão, tem de ser um objeto JSON.</summary>
    public static CorpoPedido Ler(byte[] bytes)
    {
        // Um corpo com a marca UTF-8 (EF BB BF) no começo vale como sem ela.
        ReadOnlyMemory<byte> corpo = bytes.AsSpan().StartsWith((ReadOnlySpan<byte>)[0xEF, 0xBB, 0xBF]) ? bytes.AsMemory(3) : bytes;
        if (corpo.Length == 0 || corpo.ToArray().All(b => b is (byte)' ' or (byte)'\r' or (byte)'\n' or (byte)'\t'))
            return new CorpoPedido(null);
        try
        {
            using var doc = JsonDocument.Parse(corpo);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                throw new PedidoInvalidoException("O corpo tem de ser um objeto JSON.");
            return new CorpoPedido(doc.RootElement.Clone());
        }
        catch (JsonException)
        {
            throw new PedidoInvalidoException("O corpo não é um JSON válido.");
        }
    }

    /// <summary>O campo, ou null se falta (ou é null).</summary>
    public JsonElement? Campo(string nome) =>
        _raiz is { } r && r.TryGetProperty(nome, out var v) && v.ValueKind != JsonValueKind.Null ? v : null;

    public int Inteiro(string nome) =>
        InteiroOpcional(nome) ?? throw new PedidoInvalidoException($"Falta o campo \"{nome}\" (número inteiro).");

    public int? InteiroOpcional(string nome)
    {
        if (Campo(nome) is not { } v)
            return null;
        if (v.ValueKind != JsonValueKind.Number || !v.TryGetInt32(out var n))
            throw new PedidoInvalidoException($"O campo \"{nome}\" tem de ser um número inteiro.");
        return n;
    }

    public decimal Numero(string nome)
    {
        if (Campo(nome) is not { } v)
            throw new PedidoInvalidoException($"Falta o campo \"{nome}\" (número).");
        if (v.ValueKind != JsonValueKind.Number || !v.TryGetDecimal(out var n))
            throw new PedidoInvalidoException($"O campo \"{nome}\" tem de ser um número.");
        return n;
    }

    public string Texto(string nome) =>
        TextoOpcional(nome) ?? throw new PedidoInvalidoException($"Falta o campo \"{nome}\" (texto).");

    public string? TextoOpcional(string nome)
    {
        if (Campo(nome) is not { } v)
            return null;
        if (v.ValueKind != JsonValueKind.String)
            throw new PedidoInvalidoException($"O campo \"{nome}\" tem de ser um texto.");
        return v.GetString();
    }

    public bool Booleano(string nome)
    {
        if (Campo(nome) is not { } v)
            throw new PedidoInvalidoException($"Falta o campo \"{nome}\" (true ou false).");
        if (v.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            throw new PedidoInvalidoException($"O campo \"{nome}\" tem de ser true ou false.");
        return v.GetBoolean();
    }
}
