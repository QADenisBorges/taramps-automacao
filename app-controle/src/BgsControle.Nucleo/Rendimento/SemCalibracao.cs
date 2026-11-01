using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Setups;

namespace BgsControle.Nucleo.Rendimento;

/// <summary>
/// A saída sem calibração (spec-rendimento CAP-3, história 13b): uma saída com alto-falante passivo cujo
/// módulo não tem o ganho medido (o <c>Ganho</c>, ou na ponte o <c>GanhoPonte</c>, sem a origem "medido").
/// O app não sabe a tensão dela. Pela porta, as escritas que mexem no nível dela são recusadas:
/// <list type="bullet">
/// <item>o ganho, o limitador e o EQ de saída dos canais dela (<c>081</c>, <c>070</c>–<c>073</c> e <c>090</c>–<c>092</c>);</item>
/// <item>o MASTER, o EQ de entrada, as 15 bandas, o preset de EQ e a rota (<c>080</c>, <c>030</c>–<c>032</c>, <c>020</c>,
/// <c>220</c> e <c>010</c>), se alguma saída
/// do Setup em uso está sem calibração.</item>
/// </list>
/// Sem Setup em uso, todos esses códigos são recusados. A tela passa, com o aviso <see cref="MsgAviso"/>. A
/// volta à cópia e o desfazer nunca são recusados por esta regra.
/// </summary>
public static class SemCalibracao
{
    public const string MsgAviso = "Sem calibração: o app não sabe a tensão.";
    public const string MsgSemSetup = "Sem Setup em uso: o app não sabe a tensão das saídas.";
    public const string MsgFalhou = "Não consegui conferir a calibração da saída. Nada foi enviado.";

    /// <summary>"Sem calibração: JBL L. O app não sabe a tensão."</summary>
    public static string Msg(string saida) => $"Sem calibração: {saida}. O app não sabe a tensão.";

    /// <summary>Os códigos de um canal que mexem no nível da saída: o ganho, o limitador e o EQ de saída.</summary>
    public static IReadOnlySet<string> CodigosDoCanal { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "081", "070", "071", "072", "073", "090", "091", "092",
    };

    /// <summary>
    /// Os códigos que mexem no nível de todas as saídas: o MASTER, o EQ de entrada, as 15 bandas, o preset de EQ (que
    /// reescreve as bandas) e a rota (a+b soma as duas entradas, até +6 dB).
    /// </summary>
    public static IReadOnlySet<string> CodigosGlobais { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "080", "030", "031", "032", "020", "220", "010",
    };

    /// <summary>A regra vale para este código.</summary>
    public static bool Vigia(string codigo) => CodigosDoCanal.Contains(codigo) || CodigosGlobais.Contains(codigo);

    /// <summary>
    /// A saída tem algum alto-falante passivo cujo módulo não tem o ganho (no bridge, o da ponte) com a origem
    /// "medido". Sem alto-falante passivo (só uma caixa ativa, ou nada), não: o app não precisa da tensão.
    /// </summary>
    public static bool Sem(IReadOnlyList<Equipamento> equipamentos, SaidaApp saida) =>
        saida.Canais.SelectMany(c => Ligacoes.Caminhos(equipamentos, c)).Any(c => !Medido(c));

    /// <summary>O ganho do módulo no caminho (o da ponte, no bridge) tem a origem "medido".</summary>
    public static bool Medido(CaminhoAltoFalante caminho) =>
        (caminho.Ponte ? caminho.Modulo.Dados.GanhoPonte : caminho.Modulo.Dados.Ganho) is { Origem: OrigemDado.Medido };

    /// <summary>
    /// A recusa para as escritas, ou null. Sem Setup em uso (<paramref name="equipamentos"/> null), todo código
    /// vigiado é recusado. Um código de canal é recusado se a saída do canal está sem calibração; um global, se
    /// alguma saída está.
    /// </summary>
    /// <param name="equipamentos">Os equipamentos do Setup em uso, ou null sem Setup em uso.</param>
    /// <param name="saidas">As saídas do app (o link que vale).</param>
    /// <param name="escritas">Os endereços que vão ser escritos.</param>
    public static string? Conferir(IReadOnlyList<Equipamento>? equipamentos, IReadOnlyList<SaidaApp> saidas, IEnumerable<Endereco> escritas)
    {
        var vigiadas = escritas.Where(e => Vigia(e.Codigo)).ToArray();
        if (vigiadas.Length == 0)
            return null;
        if (equipamentos is null)
            return MsgSemSetup;
        var nomes = Ligacoes.NomesNaTela(saidas);
        var sem = saidas.Select((s, i) => (Saida: s, Nome: nomes[i])).Where(x => Sem(equipamentos, x.Saida)).ToArray();
        if (sem.Length == 0)
            return null;
        foreach (var e in vigiadas)
        {
            if (CodigosGlobais.Contains(e.Codigo))
                return Msg(sem[0].Nome);
            if (e.Indice is { } canal && sem.FirstOrDefault(x => x.Saida.Canais.Contains(canal)) is { Nome: { } nome })
                return Msg(nome);
        }
        return null;
    }
}
