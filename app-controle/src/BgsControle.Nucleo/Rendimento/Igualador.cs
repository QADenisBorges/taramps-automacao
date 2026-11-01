using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Setups;

namespace BgsControle.Nucleo.Rendimento;

/// <summary>
/// O volume igualado da escuta "1 ou 2" (história 14b; spec-agente-memoria CAP-4): o nível médio de cada versão,
/// ponderado por um ruído rosa, e o master que deixa as duas no mesmo nível.
/// <list type="bullet">
/// <item><b>A resposta da cadeia</b> é a do pior caso (<see cref="PiorCaso.Resposta"/>, história 13c): o EQ de entrada, as
/// 15 bandas, a rota, o ganho, o EQ de saída e os cortes, sem o master.</item>
/// <item><b>O peso rosa:</b> o ruído rosa tem a mesma energia por oitava; numa grade logarítmica, cada ponto pesa igual. O
/// nível de um canal é 10·log10 da média de |H(f)|² nos pontos da grade de 20 Hz a 20 kHz.</item>
/// <item><b>As saídas com alto-falante:</b> o nível de uma saída é a média de |H|² dos canais dela; o da versão, a média
/// das saídas que alimentam algo que toca (sem nenhuma, as quatro saídas do processador).</item>
/// <item><b>O master:</b> a versão mais forte recebe o master baixado pela diferença, no passo de 0,1 dB para baixo (o
/// lado seguro); a mais fraca fica como está. Só baixa, nunca sobe.</item>
/// </list>
/// Puro: a cadeia entra por um leitor de endereço.
/// </summary>
public static class Igualador
{
    public const double FrequenciaMinima = 20;
    public const double FrequenciaMaxima = 20000;

    /// <summary>O passo do master (<c>080</c>), em dB.</summary>
    public const decimal Passo = 0.1m;

    /// <summary>
    /// Os canais de cada saída que alimenta algo que toca (um equipamento do grupo 1 ou um alto-falante passivo), na
    /// ordem das saídas. Sem nenhuma (sem equipamentos), cada canal do processador é uma saída.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<int>> SaidasComAltoFalante(IReadOnlyList<Equipamento>? equipamentos, IReadOnlyList<SaidaApp> saidas)
    {
        var lista = equipamentos ?? [];
        var com = saidas.Where(s => Ligacoes.Alimentados(lista, s.Canais.ToArray()).Any(e => e.Toca))
            .Select(s => (IReadOnlyList<int>)s.Canais.ToArray())
            .ToArray();
        return com.Length > 0 ? com : Enumerable.Range(0, Ligacoes.CanaisProcessador).Select(c => (IReadOnlyList<int>)[c]).ToArray();
    }

    /// <summary>
    /// O nível médio da cadeia, sem o master, em dB (a referência do limitador; só a diferença entre duas versões
    /// importa), ou o dado que falta.
    /// </summary>
    /// <param name="ler">A cadeia (o espelho com a versão por cima).</param>
    /// <param name="saidas">Os canais de cada saída com alto-falante (<see cref="SaidasComAltoFalante"/>).</param>
    public static (double? Db, string? Falta) NivelMedio(Func<Endereco, Valor?> ler, IReadOnlyList<IReadOnlyList<int>> saidas)
    {
        var grade = PiorCaso.Grade;
        var pontos = Enumerable.Range(0, grade.Count).Where(i => grade[i] >= FrequenciaMinima && grade[i] <= FrequenciaMaxima).ToArray();
        var porSaida = new List<double>();
        foreach (var canais in saidas)
        {
            var energias = new List<double>();
            foreach (var canal in canais)
            {
                var (db, falta) = PiorCaso.Resposta(ler, canal, 0m);
                if (db is null)
                    return (null, falta);
                energias.Add(pontos.Average(i => Math.Pow(10, db[i] / 10)));
            }
            if (energias.Count > 0)
                porSaida.Add(energias.Average());
        }
        if (porSaida.Count == 0)
            return (null, Niveis.MsgSemLimite);
        return (10 * Math.Log10(porSaida.Average()), null);
    }

    /// <summary>
    /// Os masters que igualam as duas versões: a mais forte (nível + master) baixa pela diferença, arredondada para baixo
    /// no passo de 0,1 dB; a outra fica igual. Null quando a mais forte precisaria do master abaixo de −80 dB (o mínimo do
    /// aparelho): não dá para igualar.
    /// </summary>
    public static (decimal Master1, decimal Master2)? Igualar(double nivel1, decimal master1, double nivel2, decimal master2)
    {
        var diferenca = nivel1 + (double)master1 - (nivel2 + (double)master2);
        if (Math.Abs(diferenca) < 1e-9)
            return (master1, master2);
        if (diferenca > 0)
            return Baixar(master1, diferenca) is { } m1 ? (m1, master2) : null;
        return Baixar(master2, -diferenca) is { } m2 ? (master1, m2) : null;
    }

    private static decimal? Baixar(decimal master, double db)
    {
        // O arredondamento a 6 casas tira o ruído do double antes do piso (como no PiorCaso.Conta).
        var alvo = (decimal)Math.Floor(Math.Round(((double)master - db) * 10, 6)) / 10m;
        return alvo < PiorCaso.MinimoMaster ? null : Math.Min(alvo, master);
    }
}
