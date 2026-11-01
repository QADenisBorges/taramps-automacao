using System.Text.Json;
using System.Text.Json.Nodes;

namespace Agente.Porta;

/// <summary>A entrada da ferramenta veio mal formada (a saída é <c>ok:false</c> com o motivo).</summary>
public sealed class EntradaInvalidaException(string mensagem) : Exception(mensagem);

/// <summary>
/// A entrada da ferramenta da porta: <c>{"acao":"ler|pedir|parear","metodo":"GET|POST|DELETE","alvo":"/estado","corpo":{...}}</c>.
/// <list type="bullet">
/// <item><c>ler</c>: um GET no <c>alvo</c> (o <c>metodo</c>, se vier, é GET).</item>
/// <item><c>pedir</c>: o <c>metodo</c> (POST se faltar) no <c>alvo</c>, com o <c>corpo</c> (um objeto) se houver.</item>
/// <item><c>parear</c>: <c>{"acao":"parear","endereco":"ip:porta","codigo":"123456"}</c>, com o <c>nome</c> opcional.</item>
/// </list>
/// O <c>alvo</c> é sempre um caminho da porta (começa com uma barra): a chave nunca vai para outro endereço.
/// </summary>
public sealed record PedidoFerramenta(string Acao, string Metodo, string? Alvo, JsonNode? Corpo, string? Endereco, string? Codigo, string? Nome)
{
    public const string Ler = "ler";
    public const string Pedir = "pedir";
    public const string Parear = "parear";

    /// <summary>O nome com que o notebook se apresenta ao parear, se a entrada não der outro.</summary>
    public const string NomePadrao = "Notebook";

    private static readonly string[] Metodos = ["GET", "POST", "DELETE"];

    public static PedidoFerramenta DeJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new EntradaInvalidaException("Entrada vazia: mande um JSON com a \"acao\" (ler, pedir ou parear).");
        JsonObject o;
        try
        {
            o = JsonNode.Parse(json) as JsonObject ?? throw new EntradaInvalidaException("A entrada precisa ser um objeto JSON.");
        }
        catch (JsonException e)
        {
            throw new EntradaInvalidaException($"A entrada não é um JSON válido ({e.Message}).");
        }

        var acao = Texto(o, "acao")?.Trim().ToLowerInvariant();
        switch (acao)
        {
            case Parear:
            {
                var endereco = Texto(o, "endereco")?.Trim();
                if (string.IsNullOrEmpty(endereco) || !EnderecoValido(endereco))
                    throw new EntradaInvalidaException("O \"endereco\" é o ip:porta mostrado no celular (por exemplo, <ip-do-celular>:8787).");
                var codigo = Texto(o, "codigo")?.Trim();
                if (string.IsNullOrEmpty(codigo))
                    throw new EntradaInvalidaException("Falta o \"codigo\" de 6 dígitos mostrado no celular.");
                var nome = Texto(o, "nome")?.Trim();
                return new PedidoFerramenta(Parear, "POST", "/parear", null, endereco, codigo, string.IsNullOrEmpty(nome) ? NomePadrao : nome);
            }
            case Ler:
            case Pedir:
            {
                var metodo = Texto(o, "metodo")?.Trim().ToUpperInvariant();
                metodo ??= acao == Ler ? "GET" : "POST";
                if (!Metodos.Contains(metodo))
                    throw new EntradaInvalidaException("O \"metodo\" é GET, POST ou DELETE.");
                if (acao == Ler && metodo != "GET")
                    throw new EntradaInvalidaException("O \"ler\" é sempre um GET; para POST ou DELETE use \"pedir\".");
                var alvo = Texto(o, "alvo")?.Trim();
                if (string.IsNullOrEmpty(alvo) || !AlvoValido(alvo))
                    throw new EntradaInvalidaException("O \"alvo\" é um caminho da porta, como /estado ou /eventos?desde=0.");
                var corpo = o["corpo"];
                if (corpo is not null and not JsonObject)
                    throw new EntradaInvalidaException("O \"corpo\" é um objeto JSON.");
                if (corpo is not null && metodo == "GET")
                    throw new EntradaInvalidaException("Um GET não leva \"corpo\".");
                return new PedidoFerramenta(acao, metodo, alvo, corpo?.DeepClone(), null, null, null);
            }
            default:
                throw new EntradaInvalidaException("A \"acao\" é ler, pedir ou parear.");
        }
    }

    private static string? Texto(JsonObject o, string nome)
    {
        var no = o[nome];
        if (no is null)
            return null;
        if (no is JsonValue v && v.TryGetValue<string>(out var s))
            return s;
        // Um número perde os zeros à esquerda (o código 012345 viraria 12345): só texto.
        if (nome == "codigo")
            throw new EntradaInvalidaException("O \"codigo\" vai entre aspas, como texto (os zeros à esquerda contam).");
        throw new EntradaInvalidaException($"O campo \"{nome}\" é um texto, entre aspas.");
    }

    /// <summary>Só um caminho (com a consulta), nunca um endereço: nada de "//host" nem "http://".</summary>
    public static bool AlvoValido(string alvo) =>
        alvo.StartsWith('/') && !alvo.StartsWith("//", StringComparison.Ordinal) && !alvo.Contains("://", StringComparison.Ordinal)
        && !alvo.Any(char.IsWhiteSpace) && !alvo.Contains('\\') && !alvo.Contains('#');

    /// <summary>"host:porta", sem esquema, caminho, consulta nem fragmento: a chave só vai para este host e esta porta.</summary>
    public static bool EnderecoValido(string endereco)
    {
        var dois = endereco.LastIndexOf(':');
        return dois > 0
               && int.TryParse(endereco[(dois + 1)..], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var porta)
               && porta is > 0 and < 65536
               && !endereco.Contains('/') && !endereco.Contains('@') && !endereco.Contains('?') && !endereco.Contains('#')
               && !endereco.Contains('\\') && !endereco.Any(char.IsWhiteSpace)
               && Uri.TryCreate("http://" + endereco + "/", UriKind.Absolute, out var uri)
               && uri.AbsolutePath == "/";
    }
}
