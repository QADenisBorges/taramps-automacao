using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Agente.ModeloFalso;

/// <summary>O roteiro veio mal formado.</summary>
public sealed class RoteiroInvalidoException(string mensagem) : Exception(mensagem);

/// <summary>
/// Um roteiro gravado do modelo falso (<c>agente/testes/roteiros/*.json</c>): respostas fixas no lugar do modelo, ditas
/// pela mesma ferramenta <c>porta</c>. Só dados genéricos.
/// <code>
/// {
///   "descricao": "...",
///   "passos": [
///     { "id": "abrir", "pedir": { "metodo": "POST", "alvo": "/criacao/abrir" } },
///     { "esperar": { "tipos": ["criacao-resposta"], "prazoS": 600 },
///       "se": [ { "campo": "texto", "igual": "Outra resposta", "ir": "outro-caminho" } ] },
///     { "id": "fim", "fim": true }
///   ]
/// }
/// </code>
/// Cada passo é um <c>pedir</c> (um pedido à porta; a saída da ferramenta é o resultado), um <c>esperar</c> (um evento do
/// <c>/eventos</c> de um dos <c>tipos</c>, com um prazo; o evento é o resultado), um <c>aprender</c> (o JSON vai à ferramenta
/// <c>aprendizado</c>, história 14e; a saída dela é o resultado, e o <c>ok:false</c> conta como recusa) ou um <c>fim</c>.
/// Depois do passo, o primeiro
/// ramo do <c>se</c> cujo <c>campo</c> (um caminho com pontos no resultado, como <c>corpo.etapa</c>) for <c>igual</c> manda
/// para o passo <c>ir</c>; senão vale o <c>ir</c> do passo, ou o passo seguinte. Um <c>pedir</c> recusado vai ao
/// <c>seFalhar</c> (sem ele, o roteiro falha) e um <c>esperar</c> vencido ao <c>seVencer</c> (idem). Um esperar que vê a
/// criação cancelada ou interrompida (sem esperar por elas) termina o roteiro: o usuário saiu.
/// </summary>
public sealed record Roteiro
{
    public string? Descricao { get; init; }

    public List<Passo> Passos { get; init; } = [];

    /// <summary>O maior prazo de uma espera (1 dia).</summary>
    public const double MaximoPrazoS = 86400;

    private static readonly string[] Metodos = ["GET", "POST", "DELETE"];

    public static JsonSerializerOptions Formato { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public static Roteiro Carregar(string caminho) => DeJson(File.ReadAllText(caminho));

    public static Roteiro DeJson(string json)
    {
        Roteiro? roteiro;
        try
        {
            roteiro = JsonSerializer.Deserialize<Roteiro>(json, Formato);
        }
        catch (JsonException e)
        {
            throw new RoteiroInvalidoException($"O roteiro não é um JSON válido ({e.Message}).");
        }
        if (roteiro?.Passos is null || roteiro.Passos.Count == 0)
            throw new RoteiroInvalidoException("O roteiro não tem passos.");
        roteiro.Validar();
        return roteiro;
    }

    /// <summary>O índice do passo com o id.</summary>
    public int Indice(string id)
    {
        var i = Passos.FindIndex(p => p.Id == id);
        return i >= 0 ? i : throw new RoteiroInvalidoException($"Não há passo \"{id}\".");
    }

    private void Validar()
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        if (Passos.Any(p => p is null))
            throw new RoteiroInvalidoException("Um passo do roteiro está vazio.");
        foreach (var p in Passos)
        {
            if (p.Id is not null && !ids.Add(p.Id))
                throw new RoteiroInvalidoException($"Dois passos com o id \"{p.Id}\".");
        }
        for (var i = 0; i < Passos.Count; i++)
        {
            var p = Passos[i];
            var nome = p.Id ?? $"#{i + 1}";
            var tipos = (p.Pedir is null ? 0 : 1) + (p.Esperar is null ? 0 : 1) + (p.Aprender is null ? 0 : 1) + (p.Fim ? 1 : 0);
            if (tipos != 1)
                throw new RoteiroInvalidoException($"O passo {nome} precisa de um, e só um, entre pedir, esperar, aprender e fim.");
            if (p.Aprender is { } aprender
                && !(aprender["acao"] is JsonValue acao && acao.TryGetValue<string>(out var texto) && !string.IsNullOrWhiteSpace(texto)))
                throw new RoteiroInvalidoException($"O passo {nome} manda à ferramenta aprendizado um JSON com a \"acao\".");
            if (p.Pedir is { } pedir)
            {
                if (string.IsNullOrWhiteSpace(pedir.Alvo) || !pedir.Alvo.StartsWith('/'))
                    throw new RoteiroInvalidoException($"O passo {nome} precisa de um alvo da porta (/...).");
                if (pedir.Metodo is null || !Metodos.Contains(pedir.Metodo.ToUpperInvariant()))
                    throw new RoteiroInvalidoException($"O passo {nome} tem o metodo GET, POST ou DELETE.");
            }
            if (p.Esperar is { } esperar)
            {
                if (esperar.Tipos is null || esperar.Tipos.Count == 0 || esperar.Tipos.Any(string.IsNullOrWhiteSpace))
                    throw new RoteiroInvalidoException($"O passo {nome} espera pelo menos um tipo de evento.");
                if (!(esperar.PrazoS > 0 && esperar.PrazoS <= MaximoPrazoS))
                    throw new RoteiroInvalidoException($"O prazo do passo {nome} vai de mais de 0 até {MaximoPrazoS} s (1 dia).");
            }
            if (p.Se is null || p.Se.Any(r => r is null || string.IsNullOrWhiteSpace(r.Campo) || r.Igual is null || string.IsNullOrWhiteSpace(r.Ir)))
                throw new RoteiroInvalidoException($"O passo {nome} tem um ramo sem campo, valor ou destino.");
            foreach (var destino in new[] { p.Ir, p.SeFalhar, p.SeVencer }.Concat(p.Se.Select(r => r.Ir)))
            {
                if (destino is not null && !ids.Contains(destino))
                    throw new RoteiroInvalidoException($"O passo {nome} vai para \"{destino}\", que não existe.");
            }
        }
    }
}

public sealed record Passo
{
    public string? Id { get; init; }

    public PedidoPasso? Pedir { get; init; }

    public EsperaPasso? Esperar { get; init; }

    /// <summary>A entrada da ferramenta <c>aprendizado</c> (história 14e), como <c>{"acao":"situacao","nome":"música"}</c>.</summary>
    public JsonObject? Aprender { get; init; }

    public bool Fim { get; init; }

    public List<Ramo> Se { get; init; } = [];

    public string? Ir { get; init; }

    public string? SeFalhar { get; init; }

    public string? SeVencer { get; init; }
}

/// <summary>Um pedido à porta: vira <c>{"acao":"pedir"|"ler","metodo":...,"alvo":...,"corpo":...}</c> para a ferramenta.</summary>
public sealed record PedidoPasso
{
    public string Metodo { get; init; } = "POST";

    public string Alvo { get; init; } = "";

    public JsonObject? Corpo { get; init; }
}

public sealed record EsperaPasso
{
    public List<string> Tipos { get; init; } = [];

    public double PrazoS { get; init; } = 600;
}

public sealed record Ramo
{
    public string Campo { get; init; } = "";

    public string Igual { get; init; } = "";

    public string Ir { get; init; } = "";
}
