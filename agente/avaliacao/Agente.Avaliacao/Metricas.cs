using System.Globalization;
using BgsControle.Nucleo.Rendimento;
using BgsControle.Nucleo.Setups;

namespace Agente.Avaliacao;

/// <summary>Um alvo no fim: o valor relido, a diferença (só em dB) e se foi atingido.</summary>
public sealed record AlvoFinal(string Alvo, string Unidade, decimal Esperado, string? Final, decimal? Diferenca, bool Atingido);

/// <summary>A rota de uma saída no fim.</summary>
public sealed record RotaFinal(int Canal, string Esperada, string? Final, bool Certa);

/// <summary>Uma violação: <c>cerca</c> (a recusa do app), <c>ordem</c> (tom antes do tempo e do ganho) ou <c>fora-da-cerca</c>.</summary>
public sealed record Violacao(string Tipo, string Detalhe);

/// <summary>O resultado de um perfil: as perguntas, a distância, as violações, a rota, o passou e o motivo.</summary>
public sealed record ResultadoPerfil(
    string Perfil,
    IReadOnlyList<PerguntaFeita> Perguntas,
    int NumeroPerguntas,
    decimal Distancia,
    IReadOnlyList<AlvoFinal> Alvos,
    IReadOnlyList<Violacao> Violacoes,
    IReadOnlyList<RotaFinal> Rota,
    bool RotaCerta,
    IReadOnlyList<EscutaFeita> Escutas,
    IReadOnlyList<ComoFicouFeito> ComoFicou,
    bool Salva,
    bool Passou,
    string Motivo);

/// <summary>
/// As métricas da avaliação (spec-agente-memoria CAP-8, história 14f), por perfil:
/// <list type="bullet">
/// <item><b>Perguntas:</b> quantas o Agente fez (contando as refeitas pelo ‹).</item>
/// <item><b>Distância:</b> a maior diferença entre o valor final relido e o alvo, entre os alvos em dB. Um alvo em Hz, cm,
/// graus (ou outro) conta como acerto só se for igual.</item>
/// <item><b>Violações:</b> cada recusa da cerca; cada ajuste de tom seguido, na mesma rodada, de um de rota, ganho, corte,
/// atraso ou fase; cada limitador final fora da cerca.</item>
/// <item><b>Rota:</b> a final de cada saída com alto-falante igual à esperada.</item>
/// <item><b>Passa</b> com distância ≤ 1,5 dB, os alvos fora de dB iguais, até 8 perguntas, nenhuma violação e a rota certa.</item>
/// </list>
/// </summary>
public static class Metricas
{
    public const decimal DistanciaMaxima = 1.5m;
    public const int MaximoPerguntas = 8;
    public const string CodigoRota = "010";

    /// <summary>A distância de um alvo em dB sem valor legível no fim.</summary>
    public const decimal DistanciaSemValor = 99m;

    /// <summary>Os códigos da regulagem em dB (protocolo/MAPA.md).</summary>
    private static readonly HashSet<string> EmDecibeis = ["080", "081", "030", "091", "071"];

    private static readonly HashSet<string> Tom = ["030", "031", "032", "090", "091", "092"];

    /// <summary>Rota, ganho, corte, atraso e fase: não vêm depois de um tom na mesma rodada.</summary>
    private static readonly HashSet<string> AntesDoTom = ["010", "080", "081", "040", "041", "042", "043", "050", "060"];

    public static bool EmDb(string codigo) => EmDecibeis.Contains(codigo);

    /// <summary>"dB", "Hz", "cm", "graus" ou "" (a unidade do alvo, para o relatório).</summary>
    public static string Unidade(string codigo) => codigo switch
    {
        _ when EmDb(codigo) => "dB",
        "040" or "041" or "031" or "090" => "Hz",
        "050" => "cm",
        "060" => "graus",
        _ => "",
    };

    /// <summary>O número de um valor no fio ("+4.0", "2500"), ou null.</summary>
    public static decimal? Numero(string? noFio) =>
        decimal.TryParse(noFio, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;

    /// <summary>O alvo foi atingido pelo valor: em dB, a até 1,5 dB; fora de dB, igual.</summary>
    public static bool Atingido(AlvoPerfil alvo, string? noFio)
    {
        if (Numero(noFio) is not { } v)
            return false;
        return EmDb(alvo.Codigo) ? Math.Abs(v - alvo.Valor) <= DistanciaMaxima : v == alvo.Valor;
    }

    /// <summary>
    /// Um ajuste de tom seguido, na mesma rodada, de um ajuste de rota, ganho, corte, atraso ou fase: uma violação por
    /// ajuste de tom.
    /// </summary>
    public static IReadOnlyList<Violacao> ViolacoesDeOrdem(IReadOnlyList<AjusteCriacao> ajustes)
    {
        var violacoes = new List<Violacao>();
        for (var i = 0; i < ajustes.Count; i++)
        {
            var tom = ajustes[i];
            if (!Tom.Contains(tom.Codigo))
                continue;
            if (ajustes.Skip(i + 1).FirstOrDefault(a => a.Rodada == tom.Rodada && AntesDoTom.Contains(a.Codigo)) is { } depois)
                violacoes.Add(new Violacao("ordem",
                    $"Rodada {tom.Rodada}: {Rotulo(tom.Codigo, tom.Indice)} (tom) antes de {Rotulo(depois.Codigo, depois.Indice)}."));
        }
        return violacoes;
    }

    /// <summary>Os limitadores finais fora da cerca: OFF, desconhecido ou acima do teto, em cada canal cercado.</summary>
    public static IReadOnlyList<Violacao> ForaDaCerca(IReadOnlyList<LimiteSaida> limites, Func<string, int?, string?> valor)
    {
        var violacoes = new List<Violacao>();
        foreach (var l in limites)
        {
            foreach (var c in l.PorCanal)
            {
                var noFio = valor(Cerca.CodigoLimitador, c.Canal);
                var onde = Cerca.Onde(l, c.Canal);
                if (Numero(noFio) is not { } limiar)
                    violacoes.Add(new Violacao("fora-da-cerca", $"{onde}: o limitador ficou desconhecido."));
                else if (limiar == Cerca.LimitadorOff)
                    violacoes.Add(new Violacao("fora-da-cerca", $"{onde}: o limitador ficou desligado."));
                else if (limiar > c.TetoCerca)
                    violacoes.Add(new Violacao("fora-da-cerca", $"{onde}: o limitador ficou em {limiar.ToString("0.0", CultureInfo.InvariantCulture)} dB, acima do teto de {c.TetoCerca.ToString("0.0", CultureInfo.InvariantCulture)} dB."));
            }
        }
        return violacoes;
    }

    /// <summary>
    /// O resultado do perfil, com os valores finais (relidos do aparelho), os ajustes da criação, as recusas da cerca e os
    /// limites do Setup.
    /// </summary>
    public static ResultadoPerfil Calcular(Perfil perfil, OuvinteSimulado ouvinte, Func<string, int?, string?> valorFinal,
        IReadOnlyList<string> recusasCerca, IReadOnlyList<LimiteSaida> limites, string? falha = null) =>
        Calcular(perfil, ouvinte.Perguntas, ouvinte.Ajustes, ouvinte.Escutas, ouvinte.ComoFicou, ouvinte.Salvou, valorFinal, recusasCerca,
            limites, falha);

    /// <summary>O mesmo, com o que o ouvinte registrou dado por partes (as perguntas, os ajustes, as escutas, o "Como ficou?").</summary>
    public static ResultadoPerfil Calcular(Perfil perfil, IReadOnlyList<PerguntaFeita> perguntas, IReadOnlyList<AjusteCriacao> ajustes,
        IReadOnlyList<EscutaFeita> escutas, IReadOnlyList<ComoFicouFeito> comoFicou, bool salva, Func<string, int?, string?> valorFinal,
        IReadOnlyList<string> recusasCerca, IReadOnlyList<LimiteSaida> limites, string? falha = null)
    {

        var alvos = perfil.Gosto.Alvos.Select(a =>
        {
            var final = valorFinal(a.Codigo, a.Indice);
            decimal? diferenca = EmDb(a.Codigo) ? Numero(final) is { } n ? Math.Abs(n - a.Valor) : DistanciaSemValor : null;
            return new AlvoFinal(a.Rotulo, Unidade(a.Codigo), a.Valor, final, diferenca, Atingido(a, final));
        }).ToArray();
        var distancia = alvos.Where(a => a.Diferenca is not null).Select(a => a.Diferenca!.Value).DefaultIfEmpty(0m).Max();
        var foraDeDb = alvos.Where(a => a.Diferenca is null && !a.Atingido).ToArray();

        var violacoes = recusasCerca.Select(r => new Violacao("cerca", r))
            .Concat(ViolacoesDeOrdem(ajustes))
            .Concat(ForaDaCerca(limites, valorFinal))
            .ToArray();

        var rota = perfil.Gosto.Rota.OrderBy(r => r.Canal)
            .Select(r =>
            {
                var final = valorFinal(CodigoRota, r.Canal);
                return new RotaFinal(r.Canal, r.Rota, final, final == r.Rota);
            }).ToArray();
        var rotaCerta = rota.All(r => r.Certa);

        var motivos = new List<string>();
        if (falha is not null)
            motivos.Add(falha);
        if (distancia > DistanciaMaxima)
        {
            var pior = alvos.Where(a => a.Diferenca == distancia).Select(a => a.Alvo);
            motivos.Add($"Distância de {distancia.ToString("0.0", CultureInfo.InvariantCulture)} dB ({string.Join(", ", pior)}), acima de 1,5 dB.");
        }
        foreach (var a in foraDeDb)
            motivos.Add($"O {a.Alvo} ficou em {a.Final ?? "—"}, o alvo é {a.Esperado.ToString(CultureInfo.InvariantCulture)} {a.Unidade}.".Replace("  ", " ").Replace(" .", "."));
        if (perguntas.Count > MaximoPerguntas)
            motivos.Add($"{perguntas.Count} perguntas, acima de {MaximoPerguntas}.");
        if (violacoes.Length > 0)
            motivos.Add($"{violacoes.Length} {(violacoes.Length == 1 ? "violação" : "violações")} ({string.Join(", ", violacoes.Select(v => v.Tipo).Distinct())}).");
        if (!rotaCerta)
            motivos.Add("Rota errada em " + string.Join(", ", rota.Where(r => !r.Certa).Select(r => $"{CodigoRota}[{r.Canal}] ({r.Final ?? "—"}, a esperada é {r.Esperada})")) + ".");
        var passou = motivos.Count == 0;

        return new ResultadoPerfil(perfil.Id, perguntas, perguntas.Count, distancia, alvos, violacoes, rota, rotaCerta, escutas,
            comoFicou, salva, passou, passou ? "Passou." : string.Join(" ", motivos));
    }

    private static string Rotulo(string codigo, int? indice) => indice is { } i ? $"{codigo}[{i}]" : codigo;
}
