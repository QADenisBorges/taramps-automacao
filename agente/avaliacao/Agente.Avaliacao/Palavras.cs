using System.Globalization;
using System.Text;

namespace Agente.Avaliacao;

/// <summary>
/// As palavras de um texto para o ouvinte simulado comparar: sem acento, sem maiúsculas, só letras e dígitos. Ficam de fora
/// as palavras de menos de 3 letras e as ligações comuns ("que", "com", "para"…), que casariam com qualquer frase.
/// </summary>
public static class Palavras
{
    private static readonly HashSet<string> Ligacoes =
    [
        "que", "com", "para", "por", "pra", "uma", "uns", "umas", "das", "dos", "nas", "nos", "num", "numa",
        "pela", "pelo", "pelas", "pelos", "como", "seu", "sua", "seus", "suas", "esta", "este", "isso", "isto",
        "voce", "quem", "qual", "quais", "muito", "bem", "the", "and",
    ];

    /// <summary>O texto sem acento e em minúsculas.</summary>
    public static string Normalizar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return "";
        var sem = new StringBuilder();
        foreach (var c in texto.ToLowerInvariant().Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sem.Append(c);
        }
        return sem.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>As palavras que contam (sem repetir).</summary>
    public static IReadOnlySet<string> De(string? texto)
    {
        var palavras = new HashSet<string>(StringComparer.Ordinal);
        var atual = new StringBuilder();
        foreach (var c in Normalizar(texto) + " ")
        {
            if (char.IsLetterOrDigit(c))
            {
                atual.Append(c);
                continue;
            }
            if (atual.Length >= 3 && !Ligacoes.Contains(atual.ToString()))
                palavras.Add(atual.ToString());
            atual.Clear();
        }
        return palavras;
    }

    /// <summary>Quantas palavras de <paramref name="texto"/> estão em <paramref name="conhecidas"/>.</summary>
    public static int EmComum(string? texto, IReadOnlySet<string> conhecidas) => De(texto).Count(conhecidas.Contains);

    /// <summary>
    /// A opção com mais palavras em comum com <paramref name="conhecidas"/> (a primeira, no empate), ou null quando
    /// nenhuma tem palavra em comum.
    /// </summary>
    public static int? MaisPerto(IReadOnlyList<string> opcoes, IReadOnlySet<string> conhecidas)
    {
        int? melhor = null;
        var maior = 0;
        for (var i = 0; i < opcoes.Count; i++)
        {
            var n = EmComum(opcoes[i], conhecidas);
            if (n > maior)
            {
                maior = n;
                melhor = i;
            }
        }
        return melhor;
    }
}
