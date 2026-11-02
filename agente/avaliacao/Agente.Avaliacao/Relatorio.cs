using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Agente.Avaliacao;

/// <summary>
/// O relatório de uma avaliação: o passou geral, a data, o tipo (<c>falso</c> ou <c>real</c>), o hash das camadas e o
/// resultado de cada perfil. Vai em <c>relatorio.json</c> e <c>relatorio.txt</c>.
/// </summary>
public sealed record Relatorio(bool Passou, DateTimeOffset Data, string Tipo, string HashCamadas, IReadOnlyList<ResultadoPerfil> Perfis)
{
    public const string TipoFalso = "falso";
    public const string TipoReal = "real";
    public const string ArquivoJson = "relatorio.json";
    public const string ArquivoTexto = "relatorio.txt";

    public static JsonSerializerOptions Formato { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>O conjunto passa se todos os perfis passam (e há algum).</summary>
    public static Relatorio Montar(IReadOnlyList<ResultadoPerfil> perfis, string tipo, string hashCamadas, DateTimeOffset? data = null) =>
        new(perfis.Count > 0 && perfis.All(p => p.Passou), data ?? DateTimeOffset.Now, tipo, hashCamadas, perfis);

    public string ParaJson() => JsonSerializer.Serialize(this, Formato);

    public static Relatorio DeJson(string json) =>
        JsonSerializer.Deserialize<Relatorio>(json, Formato) ?? throw new JsonException("O relatório está vazio.");

    /// <summary>O relatório para ler: o topo e, por perfil, as perguntas, a distância, as violações, a rota e o motivo.</summary>
    public string ParaTexto()
    {
        var t = new StringBuilder();
        t.AppendLine($"Avaliação {Tipo}: {(Passou ? "PASSOU" : "NÃO PASSOU")}");
        t.AppendLine($"Data: {Data.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture)}");
        t.AppendLine($"Hash das camadas: {HashCamadas}");
        foreach (var p in Perfis)
        {
            t.AppendLine();
            t.AppendLine($"== {p.Perfil}: {(p.Passou ? "passou" : "não passou")} — {p.Motivo}");
            t.AppendLine($"Perguntas ({p.NumeroPerguntas}):");
            foreach (var q in p.Perguntas)
                t.AppendLine($"  {q.Numero}. {q.Texto} [{string.Join(" | ", q.Respostas)}] → {(q.Resposta is null ? "(sem resposta)" : (q.DoOutro ? "Outro: " : "") + q.Resposta)}");
            t.AppendLine($"Distância: {p.Distancia.ToString("0.0", CultureInfo.InvariantCulture)} dB");
            foreach (var a in p.Alvos)
                t.AppendLine($"  {a.Alvo}: alvo {a.Esperado.ToString(CultureInfo.InvariantCulture)} {a.Unidade}, final {a.Final ?? "—"}{(a.Atingido ? "" : " (fora)")}");
            t.AppendLine("Rota: " + string.Join(", ", p.Rota.Select(r => $"{Metricas.CodigoRota}[{r.Canal}] {r.Final ?? "—"} (esperada {r.Esperada})")));
            if (p.Escutas.Count > 0)
                t.AppendLine("Escutas: " + string.Join("; ", p.Escutas.Select(e => $"{e.Variavel} → {e.Escolha}")));
            t.AppendLine(p.Violacoes.Count == 0 ? "Violações: nenhuma" : "Violações:");
            foreach (var v in p.Violacoes)
                t.AppendLine($"  [{v.Tipo}] {v.Detalhe}");
            t.AppendLine($"Salva: {(p.Salva ? "sim" : "não")}");
        }
        return t.ToString();
    }

    /// <summary>Grava o <c>relatorio.json</c> e o <c>relatorio.txt</c> na pasta (criada se faltar).</summary>
    public void Gravar(string pasta)
    {
        Directory.CreateDirectory(pasta);
        var utf8 = new UTF8Encoding(false);
        File.WriteAllText(Path.Combine(pasta, ArquivoJson), ParaJson(), utf8);
        File.WriteAllText(Path.Combine(pasta, ArquivoTexto), ParaTexto(), utf8);
    }
}

/// <summary>
/// O hash das camadas do Agente: o SHA-256 dos arquivos <c>.md</c> de <c>instrucoes/</c>, <c>skills/</c>, <c>conhecimento/</c>
/// e <c>tarefas/</c> (os que o serviço copia para a pasta de trabalho), pelos caminhos (relativos a <c>agente/</c>, com
/// <c>/</c>) e pelo conteúdo de cada arquivo, em ordem de caminho. Outros arquivos (do editor, do sistema) não contam. As quebras
/// de linha CRLF contam como LF (o mesmo hash no Windows e fora dele).
/// </summary>
public static class HashCamadas
{
    public static IReadOnlyList<string> Pastas { get; } = ["instrucoes", "skills", "conhecimento", "tarefas"];

    public static string Calcular(string pastaAgente)
    {
        var arquivos = Pastas
            .Select(p => Path.Combine(pastaAgente, p))
            .Where(Directory.Exists)
            .SelectMany(p => Directory.EnumerateFiles(p, "*.md", SearchOption.AllDirectories))
            .Select(f => (Caminho: f, Relativo: Path.GetRelativePath(pastaAgente, f).Replace('\\', '/')))
            .OrderBy(f => f.Relativo, StringComparer.Ordinal)
            .ToArray();
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var (caminho, relativo) in arquivos)
        {
            sha.AppendData(Encoding.UTF8.GetBytes(relativo));
            sha.AppendData([0]);
            sha.AppendData(SemCr(File.ReadAllBytes(caminho)));
            sha.AppendData([0]);
        }
        return Convert.ToHexStringLower(sha.GetHashAndReset());
    }

    private static byte[] SemCr(byte[] bytes)
    {
        var saida = new List<byte>(bytes.Length);
        for (var i = 0; i < bytes.Length; i++)
        {
            if (bytes[i] == (byte)'\r' && i + 1 < bytes.Length && bytes[i + 1] == (byte)'\n')
                continue;
            saida.Add(bytes[i]);
        }
        return saida.ToArray();
    }
}

/// <summary>
/// A trava das camadas (<c>conferir</c>): decide pelo relatório real mais recente com o hash das camadas de agora; passa só
/// se ele passou.
/// </summary>
public static class Conferencia
{
    public const string MsgMudaram = "As camadas mudaram desde a última avaliação real que passou.";
    public const string MsgNenhuma = "Nenhuma avaliação real passou ainda.";
    public const string MsgFalhou = "A última avaliação real com estas camadas não passou.";

    /// <summary>A pasta dos relatórios versionados, dentro de <c>agente/</c>.</summary>
    public static string PastaRelatorios(string pastaAgente) => Path.Combine(pastaAgente, "avaliacao", "relatorios");

    /// <summary>O código (0 ou 1) e a mensagem.</summary>
    public static (int Codigo, string Mensagem) Conferir(string pastaAgente, string pastaRelatorios)
    {
        var agora = HashCamadas.Calcular(pastaAgente);
        var reais = Reais(pastaRelatorios);
        var comEstas = reais.Where(r => string.Equals(r.HashCamadas, agora, StringComparison.OrdinalIgnoreCase)).MaxBy(r => r.Data);
        if (comEstas is null)
            return (1, reais.Any(r => r.Passou) ? MsgMudaram : MsgNenhuma);
        if (!comEstas.Passou)
            return (1, MsgFalhou);
        return (0, $"As camadas são as da avaliação real que passou em {comEstas.Data.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)}.");
    }

    /// <summary>Os relatórios reais da pasta. Um relatório ilegível (estragado ou sem permissão) é ignorado.</summary>
    public static IReadOnlyList<Relatorio> Reais(string pastaRelatorios)
    {
        if (!Directory.Exists(pastaRelatorios))
            return [];
        var reais = new List<Relatorio>();
        foreach (var arquivo in Directory.EnumerateFiles(pastaRelatorios, Relatorio.ArquivoJson, SearchOption.AllDirectories))
        {
            try
            {
                var r = Relatorio.DeJson(File.ReadAllText(arquivo));
                if (r.Tipo == Relatorio.TipoReal)
                    reais.Add(r);
            }
            catch (Exception e) when (e is JsonException or NotSupportedException or IOException or UnauthorizedAccessException)
            {
                // ilegível: fica de fora
            }
        }
        return reais;
    }
}
