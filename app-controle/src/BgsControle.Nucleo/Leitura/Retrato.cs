using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BgsControle.Nucleo.Leitura;

/// <summary>Uma consulta respondida: o valor decodificado ao lado do texto cru.</summary>
public sealed record ItemLido(
    string Codigo,
    string Nome,
    int? Indice,
    string Onde,
    bool Extra,
    string Consulta,
    string Cru,
    Valor Valor,
    int Tentativas)
{
    /// <summary>Uma linha da lista da tela: código, nome, saída e valor.</summary>
    public string Linha => $"{Codigo} {Nome} · {Onde} · {Valor.Exibicao}";
}

/// <summary>Uma consulta sem valor: sem resposta, ou resposta ilegível (com o cru).</summary>
public sealed record Falha(
    string Codigo,
    string Nome,
    int? Indice,
    string Onde,
    bool Extra,
    string Consulta,
    string Motivo,
    string? Cru,
    int Tentativas)
{
    public string Linha => $"{Codigo} {Nome} · {Onde} · {Motivo}{(Cru is null ? "" : $" ({Cru})")}";
}

public sealed record Placar(int Lidos, int Total)
{
    public string Texto => $"{Lidos} de {Total}";
}

/// <summary>
/// O retrato do aparelho numa leitura completa: os itens lidos (valor e cru), as falhas com
/// o motivo, os órfãos, os placares do Marco 1 e dos extras, início, fim e se ficou completo.
/// Imutável: o leitor entrega um retrato novo a cada consulta.
/// </summary>
public sealed record Retrato
{
    public const int Formato = 1;
    public const string Aparelho = "PRO 2.4BT + AUDIO";

    public int VersaoDoFormato => Formato;
    public string NomeDoAparelho => Aparelho;

    public string? Firmware { get; init; }
    public DateTimeOffset Inicio { get; init; }

    /// <summary>Hora em que a última consulta do Marco 1 terminou.</summary>
    public DateTimeOffset? FimMarco1 { get; init; }

    public DateTimeOffset? Fim { get; init; }

    /// <summary>Todas as consultas foram feitas (com ou sem falhas). False = parou no meio.</summary>
    public bool Completo { get; init; }

    /// <summary>Por que parou ("A conexão caiu."). Null se completou ou ainda lê.</summary>
    public string? Motivo { get; init; }

    public IReadOnlyList<ItemLido> Lidos { get; init; } = [];
    public IReadOnlyList<Falha> Falhas { get; init; } = [];
    public IReadOnlyList<string> Orfaos { get; init; } = [];

    /// <summary>"lendo", "completo" ou "incompleto".</summary>
    public string Status => Fim is null ? "lendo" : Completo ? "completo" : "incompleto";

    public Placar PlacarMarco1 => new(Lidos.Count(l => !l.Extra), Catalogo.TotalMarco1);
    public Placar PlacarExtras => new(Lidos.Count(l => l.Extra), Catalogo.TotalExtras);

    /// <summary>Consultas feitas até agora (lidas ou com falha).</summary>
    public int Feitas => Lidos.Count + Falhas.Count;

    public TimeSpan? DuracaoMarco1 => FimMarco1 - Inicio;
    public TimeSpan? Duracao => Fim - Inicio;

    public ItemLido? Item(string codigo, int? indice = null) =>
        Lidos.FirstOrDefault(l => l.Codigo == codigo && l.Indice == indice);

    public Falha? FalhaDe(string codigo, int? indice = null) =>
        Falhas.FirstOrDefault(f => f.Codigo == codigo && f.Indice == indice);

    /// <summary>O retrato em JSON, para guardar nos arquivos privados do app.</summary>
    public string ParaJson() => JsonSerializer.Serialize(this, RetratoJson.Contexto.Retrato);
}

[JsonSourceGenerationOptions(
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(Retrato))]
internal sealed partial class RetratoJson : JsonSerializerContext
{
    // Acentos e "°" legíveis no arquivo (o JSON nunca vai para uma página web).
    public static RetratoJson Contexto { get; } = new(new JsonSerializerOptions
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    });
}
