using BgsControle.Nucleo;
using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Setups;

namespace BgsControle.Telas;

/// <summary>
/// Uma saída do app na tela (história 10): a saída do Setup em uso (<see cref="GerenteSetups.SaidasEmUso"/>,
/// história 8e), com o nome que a tela mostra. Um canal, ou um par com link que escreve no mestre e o
/// aparelho repete no outro. A cor é a do canal mestre (tokens <c>canal-1</c> a <c>canal-4</c>).
/// </summary>
/// <param name="App">A saída do núcleo: o mestre, os canais, o nome e o aviso do bridge.</param>
/// <param name="Nome">O nome na tela ("JBL L", "Sub", "CH3 + CH4"; "Sub · CH3" quando dois se chamam igual).</param>
public sealed record SaidaTela(SaidaApp App, string Nome)
{
    /// <summary>O canal mestre (0 a 3): onde a tela escreve, e a cor.</summary>
    public int Canal => App.Mestre;

    /// <summary>"CH1", "CH3 + CH4".</summary>
    public string Rotulo => App.Rotulo;

    /// <summary>O aviso do bridge ("CH3 e CH4 alimentam o mesmo bridge: ligue o Link 3–4…"), ou null.</summary>
    public string? Aviso => App.Aviso;

    /// <summary>É um par com link.</summary>
    public bool Par => App.Par;

    /// <summary>As saídas do núcleo com os nomes da tela, na ordem dos canais.</summary>
    public static IReadOnlyList<SaidaTela> De(IReadOnlyList<SaidaApp> saidas)
    {
        var nomes = Ligacoes.NomesNaTela(saidas);
        return saidas.Select((s, i) => new SaidaTela(s, nomes[i])).ToArray();
    }

    /// <summary>As saídas em uso agora.</summary>
    public static IReadOnlyList<SaidaTela> EmUso(GerenteSetups setups) => De(setups.SaidasEmUso);

    /// <summary>A mesma saída para a tela: mestre, canais, nome e aviso.</summary>
    public bool Igual(SaidaTela outra) =>
        Canal == outra.Canal && Nome == outra.Nome && Aviso == outra.Aviso && App.Canais.SequenceEqual(outra.App.Canais);

    /// <summary>As duas listas mostram as mesmas saídas.</summary>
    public static bool Iguais(IReadOnlyList<SaidaTela> a, IReadOnlyList<SaidaTela> b) =>
        a.Count == b.Count && a.Zip(b).All(p => p.First.Igual(p.Second));
}

/// <summary>Os textos curtos das saídas e os títulos dos parâmetros por extenso (EXPERIENCE, Voice and Tone).</summary>
public static class TextosParametro
{
    /// <summary>A rota lida para a tela: "A", "B", "A+B" ou "—".</summary>
    public static string Rota(EstadoAparelho estado, int canal) =>
        estado.Valor("010", canal) is { Rotulo: { } r } ? r : "—";

    /// <summary>O nome por extenso de um parâmetro ("Passa-alta", "Atraso"), para o título do controle.</summary>
    public static string Titulo(string codigo, int? indice = null) => codigo switch
    {
        "080" => "MASTER",
        "081" => "Ganho",
        "100" => "Mute",
        "010" => "Rota",
        "060" => "Fase",
        "040" => "Passa-alta",
        "041" => "Passa-baixa",
        "042" => "Tipo do filtro",
        "043" => "Tipo do filtro",
        "050" => "Atraso",
        "070" => "Auto do limitador",
        "071" => "Limitador",
        "072" => "Ataque",
        "073" => "Liberação",
        "090" => "Frequência",
        "091" => "Ganho",
        "092" => "Q",
        "030" => "Ganho",
        "031" => "Frequência",
        "032" => "Q",
        "020" => indice is { } b ? Catalogo.RotuloBanda(b) : "Banda",
        "220" => "Preset de EQ do aparelho",
        _ => Catalogo.ParametroDe(codigo)?.Nome ?? codigo,
    };

    /// <summary>Um número curto para os resumos ("+10,0", "80", "OFF"), ou "—" sem leitura.</summary>
    public static string Curto(EstadoAparelho estado, string codigo, int? indice)
    {
        var p = Catalogo.ParametroDe(codigo);
        if (p is null || estado.Valor(codigo, indice) is not { } v)
            return "—";
        if (v.Rotulo is not null)
            return v.Rotulo;
        if (v.Numero is not { } n)
            return v.Exibicao;
        if (p.Tipo == TipoValor.Hertz && n >= 1000)
            return $"{(n / 1000m).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',')}k";
        return FormatoValor.Numero(p, n);
    }

    /// <summary>
    /// A frequência de um filtro para um resumo: "OFF" com o tipo em OFF, "—" com o tipo ainda não lido
    /// (não se mostra uma frequência de um filtro que talvez esteja desligado), senão o número curto.
    /// </summary>
    /// <param name="frequencia">040 (passa-alta) ou 041 (passa-baixa).</param>
    /// <param name="tipo">042 ou 043.</param>
    public static string Filtro(EstadoAparelho estado, string frequencia, string tipo, int canal) =>
        estado.Valor(tipo, canal) switch
        {
            null => "—",
            { Numero: 0 } => "OFF",
            _ => Curto(estado, frequencia, canal),
        };
}

/// <summary>Um pedaço de um resumo, com o canal mestre da saída dona (a cor dela) ou null (texto de apoio).</summary>
public sealed record TrechoResumo(string Texto, int? Canal = null);
