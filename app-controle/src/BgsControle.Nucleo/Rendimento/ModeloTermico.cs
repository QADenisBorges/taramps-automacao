using System.Globalization;
using BgsControle.Nucleo.Setups;

namespace BgsControle.Nucleo.Rendimento;

/// <summary>Os cinco dados térmicos de um alto-falante (história 13f), em double.</summary>
/// <param name="TMax">A temperatura máxima da bobina, em °C.</param>
/// <param name="RthBobinaIma">A resistência térmica da bobina ao ímã, em K/W.</param>
/// <param name="RthImaAr">A resistência térmica do ímã ao ar, em K/W.</param>
/// <param name="TauBobina">A constante de tempo da bobina, em s.</param>
/// <param name="TauIma">A constante de tempo do ímã, em s.</param>
public sealed record DadosTermicos(double TMax, double RthBobinaIma, double RthImaAr, double TauBobina, double TauIma)
{
    /// <summary>Os cinco dados da ficha, ou null se falta algum.</summary>
    public static DadosTermicos? De(DadosTecnicos d) =>
        d is { TemperaturaMaximaBobina: { } t, RthBobinaIma: { } rv, RthImaAr: { } rm, TauBobina: { } tv, TauIma: { } tm }
            ? new DadosTermicos((double)t.Valor, (double)rv.Valor, (double)rm.Valor, (double)tv.Valor, (double)tm.Valor)
            : null;

    /// <summary>Algum dos cinco não é oficial nem medido (o selo "estimado").</summary>
    public static bool Estimados(DadosTecnicos d) =>
        new[] { d.TemperaturaMaximaBobina, d.RthBobinaIma, d.RthImaAr, d.TauBobina, d.TauIma }.Any(x => x is { Verificado: false });
}

/// <summary>O aquecimento de uma bobina acima do ambiente, nas duas partes do modelo (história 13f), em K.</summary>
/// <param name="Bobina">ΔTv: a parte rápida (a bobina sobre o ímã).</param>
/// <param name="Ima">ΔTm: a parte lenta (o ímã sobre o ar).</param>
public readonly record struct EstadoTermico(double Bobina, double Ima)
{
    /// <summary>ΔT da bobina = ΔTv + ΔTm.</summary>
    public double Total => Bobina + Ima;
}

/// <summary>
/// Um alto-falante passivo das saídas do Setup no modelo térmico (história 13f): o equipamento, os dados (null = falta
/// algum dos cinco), o selo, por canal do processador que o alimenta o ganho medido do módulo (o da ponte, no bridge), e
/// por que o modelo não roda nele (<see cref="Motivo"/>).
/// </summary>
/// <param name="Impedancia">A impedância nominal, em Ω, ou null.</param>
/// <param name="Canais">Cada canal do processador (0 a 3) que o alimenta, com o ganho medido do módulo nesse caminho.</param>
/// <param name="Motivo">"Calibre a saída JBL L.", "Falta a impedância do Sub.", "Falta o modelo térmico do Sub." ou "A temperatura
/// máxima do Sub precisa passar de 26 °C.", ou null (o modelo roda).</param>
public sealed record AltoFalanteTermico(Equipamento AltoFalante, DadosTermicos? Dados, bool Estimado, double? Impedancia,
    IReadOnlyList<(int Canal, double Ganho)> Canais, string? Motivo = null)
{
    /// <summary>O modelo roda: os cinco dados, a impedância, todo caminho com o ganho medido e a máxima acima de ambiente + 1 °C.</summary>
    public bool Completo => Motivo is null && Dados is not null && Impedancia is > 0 && Canais.Count > 0;

    /// <summary>"estimado" ou "verificado".</summary>
    public string Selo => Estimado ? LimiteFisico.SeloEstimado : LimiteFisico.SeloVerificado;
}

/// <summary>
/// O "pode o Extra" pelo térmico (história 13f): todo alto-falante passivo das saídas do Setup com os cinco dados (e a
/// impedância). Senão, o motivo ("Falta o modelo térmico do Sub."). <see cref="Estimado"/>: algum dado térmico só digitado
/// (nem oficial nem medido).
/// </summary>
public sealed record AvaliacaoTermica(bool Pode, string? Motivo, bool Estimado, IReadOnlyList<AltoFalanteTermico> AltoFalantes)
{
    /// <summary>O selo "estimado" da linha do Extra, ou null.</summary>
    public string? Selo => Pode && Estimado ? LimiteFisico.SeloEstimado : null;
}

/// <summary>
/// O modelo térmico de duas constantes de tempo da bobina (história 13f, spec-rendimento CAP-7, <c>limites-e-niveis.md</c>
/// "Extra térmico"). Puro.
/// <list type="bullet">
/// <item><b>Potência na bobina:</b> P = V²/Z, com V = 2,0 × 10^((rms + master)/20) × ganho do módulo (a mesma base do
/// térmico da 13a, V = √(P·Z)).</item>
/// <item><b>ΔT da bobina</b> = ΔTv + ΔTm, em duas partes desacopladas de 1ª ordem: ΔTv → P·RthBobinaIma com τ = TauBobina,
/// e ΔTm → P·RthImaAr com τ = TauIma.</item>
/// <item><b>Passo exato</b> com a potência constante no intervalo: x ← x·e^(−dt/τ) + P·R·(1 − e^(−dt/τ)).</item>
/// <item><b>Temperatura da bobina</b> = ambiente + ΔTv + ΔTm.</item>
/// </list>
/// O tempo até a máxima e o descanso saem da solução fechada (a soma de duas exponenciais), resolvida numericamente.
/// </summary>
public static class ModeloTermico
{
    /// <summary>A temperatura ambiente sem o dado do Setup, em °C.</summary>
    public const double AmbientePadrao = 25;

    /// <summary>
    /// O descanso acaba com toda bobina até ambiente + 1 °C [ASSUMPTION: "voltar à temperatura ambiente" com 1 °C de
    /// tolerância, porque a exponencial não zera].
    /// </summary>
    public const double FolgaDescanso = 1;

    /// <summary>A precisão das contas de tempo, em s.</summary>
    private const double Precisao = 0.01;

    private static readonly CultureInfo Fio = CultureInfo.InvariantCulture;

    /// <summary>"Falta o modelo térmico do Sub."</summary>
    public static string MsgFalta(string altoFalante) => $"Falta o modelo térmico do {altoFalante}.";

    /// <summary>"Falta a impedância do Sub."</summary>
    public static string MsgFaltaImpedancia(string altoFalante) => $"Falta a impedância do {altoFalante}.";

    /// <summary>
    /// "A temperatura máxima do Sub precisa passar de 26 °C." (com a máxima até ambiente + 1 °C, o corte e o fim do descanso
    /// alternariam a cada segundo).
    /// </summary>
    public static string MsgMaximaBaixa(string altoFalante, double ambiente) =>
        $"A temperatura máxima do {altoFalante} precisa passar de {(ambiente + FolgaDescanso).ToString("0.#", Fio).Replace('.', ',')} °C.";

    /// <summary>A bobina na máxima, repartida entre as duas partes como no regime (quem começa quente sem se saber de onde).</summary>
    public static EstadoTermico NaMaxima(DadosTermicos d, double ambiente)
    {
        var delta = Math.Max(0, d.TMax - ambiente);
        var soma = d.RthBobinaIma + d.RthImaAr;
        return new EstadoTermico(delta * d.RthBobinaIma / soma, delta * d.RthImaAr / soma);
    }

    /// <summary>A temperatura ambiente do Setup, em °C (sem o dado, 25 °C).</summary>
    public static double Ambiente(Setup? setup) => setup?.TemperaturaAmbiente is { } t ? (double)t.Valor : AmbientePadrao;

    /// <summary>"35 °C", "22,5 °C" ou, sem o dado, "25 °C (padrão)".</summary>
    public static string TextoAmbiente(DadoTecnico? temperatura) => temperatura is { } t
        ? $"{t.Valor.ToString("0.#", Fio).Replace('.', ',')} °C"
        : $"{AmbientePadrao.ToString("0", Fio)} °C (padrão)";

    // ------------------------------------------------------------------ a conta

    /// <summary>P = V²/Z, com V = 2,0 × 10^((rms + master)/20) × ganho (W).</summary>
    public static double Potencia(double rmsDb, double masterDb, double ganho, double impedancia)
    {
        if (impedancia <= 0 || ganho <= 0 || rmsDb <= MedidorFaixa.PisoDb)
            return 0;
        var v = LimiteFisico.ReferenciaLimitador * Math.Pow(10, (rmsDb + masterDb) / 20) * ganho;
        return v * v / impedancia;
    }

    /// <summary>O passo exato de <paramref name="dt"/> s com a potência <paramref name="potencia"/> constante.</summary>
    public static EstadoTermico Passo(EstadoTermico x, DadosTermicos d, double potencia, double dt)
    {
        if (dt <= 0)
            return x;
        var ev = Math.Exp(-dt / d.TauBobina);
        var em = Math.Exp(-dt / d.TauIma);
        return new EstadoTermico(
            x.Bobina * ev + potencia * d.RthBobinaIma * (1 - ev),
            x.Ima * em + potencia * d.RthImaAr * (1 - em));
    }

    /// <summary>ΔT da bobina depois de <paramref name="t"/> s com a potência constante (a solução fechada).</summary>
    public static double DeltaEm(EstadoTermico x, DadosTermicos d, double potencia, double t) => Passo(x, d, potencia, t).Total;

    /// <summary>
    /// O tempo, em s, até a bobina chegar à temperatura máxima com a potência constante (0 se já chegou), ou null se ela
    /// nunca chega (o regime fica abaixo). A soma das duas exponenciais tem no máximo um ponto de virada: em cada trecho
    /// monotônico, a primeira passagem é achada por bissecção.
    /// </summary>
    public static double? TempoAteMaxima(EstadoTermico x, DadosTermicos d, double potencia, double ambiente)
    {
        var limite = d.TMax - ambiente;
        double F(double t) => DeltaEm(x, d, potencia, t);
        if (F(0) >= limite)
            return 0;
        var sv = potencia * d.RthBobinaIma;
        var sm = potencia * d.RthImaAr;
        var av = x.Bobina - sv;
        var am = x.Ima - sm;

        // f'(t) = −av/τv·e^(−t/τv) − am/τm·e^(−t/τm): zero em t* (se existe e é positivo).
        var inicio = 0.0;
        var a = -av / d.TauBobina;
        var b = am / d.TauIma;
        var k = 1 / d.TauIma - 1 / d.TauBobina;
        if (a != 0 && b != 0 && Math.Sign(a) == Math.Sign(b) && Math.Abs(k) > 1e-15)
        {
            var virada = Math.Log(b / a) / k;
            if (virada > 0 && double.IsFinite(virada))
            {
                if (F(virada) >= limite)
                    return Bissecao(F, limite, 0, virada, subindo: true);
                inicio = virada;
            }
        }

        // Do início ao infinito, monotônico: chega se o regime passa do limite.
        if (sv + sm <= limite)
            return null;
        var fim = Math.Max(inicio, 1);
        for (var i = 0; i < 200 && F(fim) < limite; i++)
            fim *= 2;
        if (F(fim) < limite)
            return null;
        return Bissecao(F, limite, inicio, fim, subindo: true);
    }

    /// <summary>O tempo, em s, até a bobina voltar a ambiente + <see cref="FolgaDescanso"/> sem potência (0 se já voltou).</summary>
    public static double TempoDescanso(EstadoTermico x, DadosTermicos d)
    {
        double G(double t) => DeltaEm(x, d, 0, t);
        if (G(0) <= FolgaDescanso)
            return 0;
        var fim = Math.Max(d.TauBobina, d.TauIma);
        for (var i = 0; i < 200 && G(fim) > FolgaDescanso; i++)
            fim *= 2;
        return Bissecao(G, FolgaDescanso, 0, fim, subindo: false);
    }

    /// <summary>
    /// A primeira passagem pelo alvo num trecho monotônico [lo, hi] (subindo: f(lo) &lt; alvo ≤ f(hi); descendo: f(lo) &gt;
    /// alvo ≥ f(hi)): o menor t do trecho, a <see cref="Precisao"/>, arredondado para cima.
    /// </summary>
    private static double Bissecao(Func<double, double> f, double alvo, double lo, double hi, bool subindo)
    {
        for (var i = 0; i < 200 && hi - lo > Precisao; i++)
        {
            var meio = (lo + hi) / 2;
            var passou = subindo ? f(meio) >= alvo : f(meio) <= alvo;
            if (passou)
                hi = meio;
            else
                lo = meio;
        }
        return hi;
    }

    // ------------------------------------------------------------------ o "pode o Extra"

    /// <summary>
    /// Os alto-falantes passivos das saídas do Setup (cada um uma vez, na ordem de criação), com os dados térmicos, a
    /// impedância, os caminhos e o motivo; o Extra pode só com todos completos. Um caminho sem o ganho medido deixa o
    /// alto-falante de fora ("Calibre a saída JBL L.": a potência de só uma parte dos canais ficaria abaixo da real). Sem
    /// nenhum alto-falante passivo, não pode (<see cref="Niveis.MsgFaltaTermico"/>).
    /// </summary>
    public static AvaliacaoTermica Avaliar(Setup? setup, IReadOnlyList<SaidaApp> saidas)
    {
        if (setup is null)
            return new AvaliacaoTermica(false, Niveis.MsgSemSetup, false, []);
        var nomes = Ligacoes.NomesNaTela(saidas);
        var porId = new Dictionary<string, (Equipamento AltoFalante, List<(int, double)> Canais, List<string> SemGanho)>(StringComparer.Ordinal);
        var ordem = new List<string>();
        foreach (var canal in saidas.SelectMany(s => s.Canais).Distinct().Order())
        {
            foreach (var c in Ligacoes.Caminhos(setup.Equipamentos, canal))
            {
                if (!porId.TryGetValue(c.AltoFalante.Id, out var item))
                {
                    porId[c.AltoFalante.Id] = item = (c.AltoFalante, [], []);
                    ordem.Add(c.AltoFalante.Id);
                }
                var ganho = c.Ponte ? c.Modulo.Dados.GanhoPonte : c.Modulo.Dados.Ganho;
                if (ganho is { Origem: OrigemDado.Medido, Valor: > 0 } g)
                {
                    if (!item.Canais.Any(x => x.Item1 == canal))
                        item.Canais.Add((canal, (double)g.Valor));
                }
                else
                {
                    var i = IndiceDaSaida(saidas, canal);
                    item.SemGanho.Add(i >= 0 ? nomes[i] : Ligacoes.RotuloCanais([canal]));
                }
            }
        }
        var ambiente = Ambiente(setup);
        var lista = ordem.Select(id =>
        {
            var (af, canais, semGanho) = porId[id];
            var dados = DadosTermicos.De(af.Dados);
            var motivo = semGanho.Count > 0 || canais.Count == 0 ? Niveis.MsgCalibre(semGanho.FirstOrDefault() ?? af.Nome)
                : af.Dados.Impedancia is null ? MsgFaltaImpedancia(af.Nome)
                : dados is null ? MsgFalta(af.Nome)
                : dados.TMax <= ambiente + FolgaDescanso ? MsgMaximaBaixa(af.Nome, ambiente)
                : null;
            return new AltoFalanteTermico(af, dados, DadosTermicos.Estimados(af.Dados),
                af.Dados.Impedancia is { } z ? (double)z.Valor : null, canais, motivo);
        }).ToArray();
        if (lista.Length == 0)
            return new AvaliacaoTermica(false, Niveis.MsgFaltaTermico, false, lista);
        var falta = lista.FirstOrDefault(a => !a.Completo);
        return new AvaliacaoTermica(falta is null, falta?.Motivo, lista.Any(a => a.Estimado), lista);
    }

    private static int IndiceDaSaida(IReadOnlyList<SaidaApp> saidas, int canal)
    {
        for (var i = 0; i < saidas.Count; i++)
        {
            if (saidas[i].Canais.Contains(canal))
                return i;
        }
        return -1;
    }
}
