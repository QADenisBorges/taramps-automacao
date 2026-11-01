using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Setups;

namespace BgsControle.Nucleo.Rendimento;

/// <summary>
/// Um limitador que a volta à cópia (ou o desfazer) escreve no teto em vez do valor da cópia (história 13a,
/// decisão do Denis de 31/10/2026): a cópia tinha o limitador OFF, acima do teto ou desconhecido.
/// </summary>
/// <param name="Endereco">O <c>071</c> do canal.</param>
/// <param name="Teto">O teto da cerca no canal, em dB.</param>
/// <param name="Saida">A saída, como a mensagem a nomeia ("Sub (CH3)").</param>
public sealed record TetoNaVolta(Endereco Endereco, decimal Teto, string Saida);

/// <summary>
/// A cerca do limitador (spec-rendimento CAP-2, história 13a): uma escrita que deixe um canal calibrado e
/// com limite com o limitador OFF, desconhecido ou com o limiar acima do teto é recusada antes de qualquer
/// envio. Ela confere o estado como ficaria depois da escrita (o espelho com a escrita, ou o lote inteiro,
/// por cima) em toda saída que a escrita toca: as escritas do limitador (<c>071</c>) e do passa-alta
/// (<c>040</c> e <c>042</c>, que mudam o X). O teto segue o nível da memória em uso (história 13c): no Máximo,
/// min(X, C); sem nível, ou do Baixo ao Alto, min(T, X, C); nunca abaixo de −48 dB (o mínimo do limitador). Os lotes que voltam (a volta à cópia e o
/// desfazer) não são recusados: o limitador de cada canal cercado volta em min(cópia, teto)
/// (<see cref="NaVolta"/>). Não tem chave nem exceção. É ligada no <see cref="Escritor"/> pelo
/// <see cref="GerenteSetups"/>, como o bloqueio da cópia antes.
/// </summary>
public static class Cerca
{
    public const string CodigoLimitador = "071";
    public const string CodigoPassaAlta = "040";
    public const string CodigoFiltroPassaAlta = "042";

    /// <summary>O OFF do limitador no fio (<c>071</c> = 0,5).</summary>
    public const decimal LimitadorOff = 0.5m;

    public const string MsgFalhou = "Não consegui conferir o limite da saída. Nada foi enviado.";

    /// <summary>O que a recusa de um corte (<c>040</c>/<c>042</c>) acrescenta.</summary>
    public const string MsgBaixeAntes = "Baixe o limitador antes de mudar o corte.";

    /// <summary>"Limite: JBL L, −6,8 dB, pelo térmico." (a saída como <see cref="Onde"/> a nomeia).</summary>
    public static string MsgLimite(string saida, decimal teto, TipoLimite manda) =>
        $"Limite: {saida}, {LimiteFisico.Db(teto)} dB, pelo {LimiteFisico.NomeLimite(manda)}.";

    /// <summary>"Limite: JBL L, o limitador não pode ficar desligado."</summary>
    public static string MsgOff(string saida) => $"Limite: {saida}, o limitador não pode ficar desligado.";

    /// <summary>"Limite: JBL L, o app não sabe o valor do limitador; releia o aparelho."</summary>
    public static string MsgDesconhecido(string saida) => $"Limite: {saida}, o app não sabe o valor do limitador; releia o aparelho.";

    /// <summary>"Limite: JBL L, o app não sabe o valor do corte; releia o aparelho."</summary>
    public static string MsgCorteDesconhecido(string saida) => $"Limite: {saida}, o app não sabe o valor do corte; releia o aparelho.";

    /// <summary>A saída na mensagem: o nome, e o canal culpado entre parênteses quando ela tem dois ("Sub (CH4)").</summary>
    public static string Onde(LimiteSaida limite, int canal) =>
        limite.Saida.Par ? $"{limite.Nome} ({Ligacoes.RotuloCanais([canal])})" : limite.Nome;

    /// <summary>A escrita deste código toca o limite de uma saída: o limitador, o passa-alta e o tipo dele.</summary>
    public static bool Toca(string codigo) => codigo is CodigoLimitador or CodigoPassaAlta or CodigoFiltroPassaAlta;

    /// <summary>
    /// Confere as escritas (uma, com a cópia do link, ou um lote inteiro) contra o estado como ficaria
    /// depois delas. Devolve a mensagem da primeira saída recusada, ou null. Sem equipamentos (sem Setup em
    /// uso), ou sem escrita que toque um limite, passa. Um valor null é uma escrita que não decodifica (o
    /// valor fica desconhecido).
    /// </summary>
    /// <param name="equipamentos">Os equipamentos do Setup, ou null.</param>
    /// <param name="saidas">As saídas do app (o link que vale).</param>
    /// <param name="espelho">O estado do aparelho agora.</param>
    /// <param name="escritas">O que vai ser escrito, na ordem (o último valor de um endereço vale).</param>
    /// <param name="nivel">O nível da memória em uso (ou da que vai ser aplicada): no Máximo, o teto é min(X, C).</param>
    public static string? Conferir(IReadOnlyList<Equipamento>? equipamentos, IReadOnlyList<SaidaApp> saidas,
        IReadOnlyDictionary<Endereco, Valor> espelho, IEnumerable<(Endereco Endereco, Valor? Valor)> escritas,
        NivelRendimento? nivel = null)
    {
        var lista = escritas.Where(e => Toca(e.Endereco.Codigo) && e.Endereco.Indice is not null).ToArray();
        if (lista.Length == 0 || equipamentos is null || equipamentos.Count == 0)
            return null;
        var canais = lista.Select(e => e.Endereco.Indice!.Value).ToHashSet();

        var depois = new Dictionary<Endereco, Valor>(espelho);
        // Os endereços que a escrita deixa desconhecidos (o último valor de cada um vale).
        var desconhecidos = new HashSet<Endereco>();
        foreach (var (endereco, valor) in lista)
        {
            if (valor is null)
            {
                depois.Remove(endereco);
                desconhecidos.Add(endereco);
            }
            else
            {
                depois[endereco] = valor;
                desconhecidos.Remove(endereco);
            }
        }
        Valor? Ler(Endereco e) => depois.GetValueOrDefault(e);

        var nomes = Ligacoes.NomesNaTela(saidas);
        for (var i = 0; i < saidas.Count; i++)
        {
            var saida = saidas[i];
            if (!saida.Canais.Any(canais.Contains))
                continue;
            var limite = LimiteFisico.CalcularSaida(equipamentos, saida, nomes[i], Ler, Niveis.SemTermico(nivel));
            foreach (var c in limite.PorCanal)
            {
                if (desconhecidos.Contains(new Endereco(CodigoLimitador, c.Canal)))
                    return MsgDesconhecido(Onde(limite, c.Canal));
                if (desconhecidos.Contains(new Endereco(CodigoPassaAlta, c.Canal))
                    || desconhecidos.Contains(new Endereco(CodigoFiltroPassaAlta, c.Canal)))
                    return MsgCorteDesconhecido(Onde(limite, c.Canal));
            }
            // A recusa de um corte: o canal culpado teve o passa-alta mudado, e não o limitador.
            bool Cortou(int canal) =>
                lista.Any(e => e.Endereco.Indice == canal && e.Endereco.Codigo != CodigoLimitador)
                && !lista.Any(e => e.Endereco.Indice == canal && e.Endereco.Codigo == CodigoLimitador);
            if (Recusa(limite, Ler, Cortou) is { } motivo)
                return motivo;
        }
        return null;
    }

    /// <summary>
    /// A recusa da saída no estado dado, ou null: em algum canal cercado, o limitador desconhecido, OFF ou
    /// acima do teto da cerca. Com <paramref name="cortou"/> verdadeiro para o canal culpado, a mensagem do
    /// teto diz para baixar o limitador antes.
    /// </summary>
    public static string? Recusa(LimiteSaida limite, Func<Endereco, Valor?> ler, Func<int, bool>? cortou = null)
    {
        foreach (var c in limite.PorCanal)
        {
            var onde = Onde(limite, c.Canal);
            if (ler(new Endereco(CodigoLimitador, c.Canal))?.Numero is not { } limiar)
                return MsgDesconhecido(onde);
            if (limiar == LimitadorOff)
                return MsgOff(onde);
            if (limiar > c.TetoCerca)
                return MsgLimite(onde, c.TetoCerca, c.Manda) + (cortou?.Invoke(c.Canal) == true ? " " + MsgBaixeAntes : "");
        }
        return null;
    }

    /// <summary>
    /// Os lotes que voltam (a volta à cópia e o desfazer, inclusive o cancelar da criação): com o estado
    /// como ficaria depois do lote (o corte da própria cópia), o limitador de cada canal cercado que ficaria
    /// OFF, desconhecido ou acima do teto vai para o teto. O resto da cópia fica igual. Sem equipamentos,
    /// nenhum.
    /// </summary>
    /// <param name="nivel">O nível da memória em uso: no Máximo, o teto é min(X, C).</param>
    public static IReadOnlyList<TetoNaVolta> NaVolta(IReadOnlyList<Equipamento>? equipamentos, IReadOnlyList<SaidaApp> saidas,
        IReadOnlyDictionary<Endereco, Valor> espelho, IEnumerable<(Endereco Endereco, Valor? Valor)> escritas,
        NivelRendimento? nivel = null)
    {
        if (equipamentos is null || equipamentos.Count == 0)
            return [];
        var depois = new Dictionary<Endereco, Valor>(espelho);
        foreach (var (endereco, valor) in escritas)
        {
            if (valor is null)
                depois.Remove(endereco);
            else
                depois[endereco] = valor;
        }
        Valor? Ler(Endereco e) => depois.GetValueOrDefault(e);

        var tetos = new List<TetoNaVolta>();
        var nomes = Ligacoes.NomesNaTela(saidas);
        for (var i = 0; i < saidas.Count; i++)
        {
            var limite = LimiteFisico.CalcularSaida(equipamentos, saidas[i], nomes[i], Ler, Niveis.SemTermico(nivel));
            foreach (var c in limite.PorCanal)
            {
                var endereco = new Endereco(CodigoLimitador, c.Canal);
                if (Ler(endereco)?.Numero is not { } limiar || limiar == LimitadorOff || limiar > c.TetoCerca)
                    tetos.Add(new TetoNaVolta(endereco, c.TetoCerca, Onde(limite, c.Canal)));
            }
        }
        return tetos;
    }
}
