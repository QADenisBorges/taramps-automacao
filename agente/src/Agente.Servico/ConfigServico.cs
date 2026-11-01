using System.Text.Json;
using System.Text.Json.Serialization;

namespace Agente.Servico;

/// <summary>
/// A configuração do serviço (<c>agente/servico.config.json</c>): os tempos, os gatilhos (o tipo do evento da porta →
/// a tarefa do Agente) e o lançador. Sem endereço nem chave: o pareamento fica na pasta local, fora do repositório.
/// </summary>
public sealed record ConfigServico
{
    public const string Arquivo = "servico.config.json";

    /// <summary>De quanto em quanto tempo o <c>/eventos</c> é lido (1 s).</summary>
    public double IntervaloS { get; init; } = 1;

    /// <summary>Com a porta sem resposta, a nova tentativa (5 s).</summary>
    public double EsperaQuedaS { get; init; } = 5;

    /// <summary>O prazo de um Agente; depois disso o processo é encerrado (30 min, [ASSUMPTION] da 14a).</summary>
    public double PrazoAgenteMin { get; init; } = 30;

    /// <summary>O tipo do evento → a tarefa (<c>tarefas/&lt;tarefa&gt;.md</c>).</summary>
    public Dictionary<string, string> Gatilhos { get; init; } = new()
    {
        ["criacao-pedida"] = "criar-memoria",
        ["ficha-pedida"] = "ficha-tecnica",
    };

    public ConfigLancador Lancador { get; init; } = new();

    [JsonIgnore]
    public TimeSpan Intervalo => TimeSpan.FromSeconds(IntervaloS);

    [JsonIgnore]
    public TimeSpan EsperaQueda => TimeSpan.FromSeconds(EsperaQuedaS);

    [JsonIgnore]
    public TimeSpan PrazoAgente => TimeSpan.FromMinutes(PrazoAgenteMin);

    public static JsonSerializerOptions Formato { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>Lê e confere (<see cref="Validar"/>). Um JSON estragado lança <see cref="JsonException"/>.</summary>
    public static ConfigServico DeJson(string json) =>
        (JsonSerializer.Deserialize<ConfigServico>(json, Formato) ?? throw new JsonException("A configuração do serviço está vazia.")).Validar();

    /// <summary>O maior intervalo e a maior espera aceitos (1 h).</summary>
    public const double MaximoEsperaS = 3600;

    /// <summary>O maior prazo de um Agente (24 h).</summary>
    public const double MaximoPrazoMin = 24 * 60;

    /// <summary>
    /// Confere os valores: os tempos positivos (e o prazo até 24 h), os gatilhos e o lançador. Só "claude" e "modelo-falso"
    /// valem como tipo: um tipo desconhecido nunca vira o Claude (que custa) sem querer.
    /// </summary>
    public ConfigServico Validar()
    {
        if (!(IntervaloS > 0 && IntervaloS <= MaximoEsperaS))
            throw new ConfigInvalidaException($"O \"intervaloS\" vai de mais de 0 até {MaximoEsperaS} s.");
        if (!(EsperaQuedaS > 0 && EsperaQuedaS <= MaximoEsperaS))
            throw new ConfigInvalidaException($"O \"esperaQuedaS\" vai de mais de 0 até {MaximoEsperaS} s.");
        if (!(PrazoAgenteMin > 0 && PrazoAgenteMin <= MaximoPrazoMin))
            throw new ConfigInvalidaException($"O \"prazoAgenteMin\" vai de mais de 0 até {MaximoPrazoMin} (24 h).");
        if (Gatilhos is null || Gatilhos.Any(g => string.IsNullOrWhiteSpace(g.Key) || string.IsNullOrWhiteSpace(g.Value)))
            throw new ConfigInvalidaException("Os \"gatilhos\" são pares evento → tarefa, sem vazios.");
        if (Lancador is null)
            throw new ConfigInvalidaException("Falta o \"lancador\".");
        switch (Lancador.Tipo)
        {
            case ConfigLancador.TipoClaude:
                if (string.IsNullOrWhiteSpace(Lancador.Comando))
                    throw new ConfigInvalidaException("O lançador \"claude\" precisa do \"comando\".");
                if (Lancador.Argumentos is null || Lancador.Argumentos.Any(a => a is null))
                    throw new ConfigInvalidaException("Os \"argumentos\" do lançador são uma lista de textos.");
                break;
            case ConfigLancador.TipoModeloFalso:
                if (Lancador.Roteiros is null || Lancador.Roteiros.Any(r => string.IsNullOrWhiteSpace(r.Value)))
                    throw new ConfigInvalidaException("Os \"roteiros\" do modelo falso são pares tarefa → arquivo.");
                break;
            default:
                throw new ConfigInvalidaException(
                    $"O \"lancador.tipo\" é \"{ConfigLancador.TipoClaude}\" ou \"{ConfigLancador.TipoModeloFalso}\" (veio \"{Lancador.Tipo}\").");
        }
        return this;
    }

    public static ConfigServico Carregar(string caminho) => DeJson(File.ReadAllText(caminho));
}

/// <summary>A configuração do serviço tem um valor que não vale.</summary>
public sealed class ConfigInvalidaException(string mensagem) : Exception(mensagem);

/// <summary>
/// O lançador do Agente. <c>tipo</c> "claude" roda o <c>comando</c> com os <c>argumentos</c> (o <c>{prompt}</c> vira o texto de
/// <c>tarefas/&lt;tarefa&gt;.md</c> e o <c>{tarefa}</c> o nome dela), numa pasta de trabalho fora do repositório, com a
/// <c>pastaPorta</c> no PATH.
/// <c>tipo</c> "modelo-falso" roda o roteiro de <c>roteiros</c> da tarefa, sem modelo.
/// </summary>
public sealed record ConfigLancador
{
    public const string TipoClaude = "claude";
    public const string TipoModeloFalso = "modelo-falso";

    public string Tipo { get; init; } = TipoClaude;

    public string Comando { get; init; } = "claude";

    public List<string> Argumentos { get; init; } = ["-p", "{prompt}", "--output-format", "json", "--allowedTools", "Bash(porta:*)"];

    /// <summary>A pasta (relativa à pasta local) com o executável <c>porta</c>, posta no PATH do Agente.</summary>
    public string? PastaPorta { get; init; } = "bin";

    /// <summary>Só no modelo falso: a tarefa → o roteiro (relativo a <c>agente/</c>).</summary>
    public Dictionary<string, string> Roteiros { get; init; } = [];
}
