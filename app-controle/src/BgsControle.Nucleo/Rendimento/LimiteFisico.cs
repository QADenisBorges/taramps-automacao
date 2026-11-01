using System.Globalization;
using System.Text;
using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Setups;

namespace BgsControle.Nucleo.Rendimento;

/// <summary>Os três limites físicos de uma saída (spec-rendimento CAP-1).</summary>
public enum TipoLimite
{
    /// <summary>T = √(P_RMS × Z).</summary>
    Termico,

    /// <summary>X: o cone no Xmax na pior frequência, com o passa-alta da saída.</summary>
    Excursao,

    /// <summary>C: o ceifamento do canal ou da ponte do módulo.</summary>
    Ceifamento,
}

/// <summary>
/// Um limite em tensão RMS de um tom contínuo, com a conta e o selo. <see cref="V"/> null e
/// <see cref="Infinito"/> falso: não dá para calcular (falta dado; o motivo em <see cref="Falta"/>), e ele
/// fica de fora do mínimo.
/// </summary>
/// <param name="Tipo">Térmico, excursão ou ceifamento.</param>
/// <param name="V">Em volts RMS, ou null.</param>
/// <param name="Infinito">O duto protege o cone (caso 2 da excursão): fica de fora do mínimo.</param>
/// <param name="Estimado">Algum dado da conta não é verificado (oficial ou medido), ou a conta é a estimativa dos casos 2 e 3.</param>
/// <param name="Conta">A conta, para a tela e a porta, ou null sem o dado.</param>
/// <param name="Falta">O dado que falta ("Falta a Fb da caixa do Sub."), ou null.</param>
public sealed record Limite(TipoLimite Tipo, double? V, bool Infinito, bool Estimado, string? Conta, string? Falta)
{
    /// <summary>Tem valor (em volts ou infinito).</summary>
    public bool Calculado => V is not null || Infinito;

    /// <summary>"verificado" ou "estimado".</summary>
    public string Selo => Estimado ? LimiteFisico.SeloEstimado : LimiteFisico.SeloVerificado;

    /// <summary>"térmico", "curso do cone" ou "ceifamento do módulo" (a mensagem da cerca).</summary>
    public string Nome => LimiteFisico.NomeLimite(Tipo);
}

/// <summary>
/// O limite de um alto-falante passivo pelo caminho de um canal do processador: T, X (com o passa-alta
/// desse canal) e C, o que manda, o teto em volts e, com o ganho medido do módulo, em dB do limitador.
/// </summary>
/// <param name="Canal">O canal do processador (0 a 3).</param>
/// <param name="Ganho">O ganho do módulo (no bridge, o da ponte) com a origem "medido", ou null (sem calibração).</param>
public sealed record LimiteCaminho(
    CaminhoAltoFalante Caminho,
    int Canal,
    Limite Termico,
    Limite Excursao,
    Limite Ceifamento,
    decimal? Ganho,
    TipoLimite? Manda,
    double? TetoV,
    decimal? TetoDb);

/// <summary>
/// O teto da cerca num canal calibrado de uma saída: o menor teto em dB entre os alto-falantes com o ganho
/// medido que o canal alimenta (história 13a). Com o teto abaixo do mínimo do limitador (−48 dB), a cerca
/// usa o mínimo (<see cref="TetoCerca"/>), e a saída leva o aviso.
/// </summary>
/// <param name="Canal">O canal do processador (0 a 3).</param>
/// <param name="TetoDb">O teto físico em dB do limitador (pode ficar abaixo de −48 dB).</param>
/// <param name="Manda">O limite que manda nesse alto-falante.</param>
/// <param name="AltoFalante">O alto-falante que manda no canal.</param>
public sealed record LimiteCanal(int Canal, decimal TetoDb, TipoLimite Manda, string AltoFalante)
{
    /// <summary>
    /// O teto que a cerca usa: o físico dentro da faixa do limitador (−48 a 0 dB). Abaixo de −48, fica em
    /// −48; acima de 0, fica em 0 (o 0,5 do OFF nunca sai daqui).
    /// </summary>
    public decimal TetoCerca => Math.Clamp(TetoDb, LimiteFisico.MinimoLimitador, LimiteFisico.MaximoLimitador);

    /// <summary>O limitador não chega ao limite físico deste canal.</summary>
    public bool AbaixoDoMinimo => TetoDb < LimiteFisico.MinimoLimitador;
}

/// <summary>Um alto-falante passivo do Setup que fica fora da conta dos limites, com o motivo (história 13a).</summary>
public sealed record AltoFalanteFora(string AltoFalante, string Motivo);

/// <summary>
/// O limite físico de uma saída do app (história 13a): o do alto-falante que manda (o menor teto entre
/// os alto-falantes passivos dela), com T, X e C, a conta, o selo e a falta de cada um. Sem alto-falante
/// passivo ou sem nenhum dos três limites, a saída fica "sem limite" (<see cref="SemLimite"/>), e a cerca
/// não vale ali. Sem o ganho medido, fica "sem calibração": os volts sim, o dB não. A cerca vale em cada
/// canal com algum alto-falante calibrado (<see cref="PorCanal"/>): uma calibração parcial não a desliga.
/// </summary>
/// <param name="Nome">O nome da saída na tela ("JBL L", "Sub · CH3").</param>
/// <param name="AltoFalante">O alto-falante que manda, ou null.</param>
/// <param name="Manda">O menor de T, X e C, ou null sem limite.</param>
/// <param name="TetoV">O teto em volts RMS, ou null sem limite.</param>
/// <param name="TetoDb">O teto do limitador em dB (arredondado para baixo, no passo de 0,1), ou null sem calibração ou sem limite. Pode ficar abaixo de −48 dB (veja <see cref="TetoCerca"/>).</param>
/// <param name="Calibrada">Todos os alto-falantes com limite têm o ganho do módulo medido.</param>
/// <param name="SemLimite">O motivo de a saída ficar sem limite, ou null.</param>
/// <param name="PorCanal">O teto da cerca em cada canal com algum alto-falante calibrado e com limite.</param>
public sealed record LimiteSaida(
    SaidaApp Saida,
    string Nome,
    string? AltoFalante,
    Limite? Termico,
    Limite? Excursao,
    Limite? Ceifamento,
    TipoLimite? Manda,
    double? TetoV,
    decimal? TetoDb,
    bool Calibrada,
    string? SemLimite,
    IReadOnlyList<LimiteCanal> PorCanal)
{
    public const string MsgAbaixoDoMinimo = "O limitador não chega ao limite físico desta saída.";

    /// <summary>Os canais da saída (0 a 3).</summary>
    public IReadOnlyList<int> Canais => Saida.Canais;

    /// <summary>O limite que manda leva o selo "estimado".</summary>
    public bool Estimado => Manda switch
    {
        TipoLimite.Termico => Termico?.Estimado ?? false,
        TipoLimite.Excursao => Excursao?.Estimado ?? false,
        TipoLimite.Ceifamento => Ceifamento?.Estimado ?? false,
        _ => false,
    };

    /// <summary>A cerca vale aqui: algum canal da saída tem um alto-falante calibrado e com limite.</summary>
    public bool Cercada => PorCanal.Count > 0;

    /// <summary>O teto que a cerca usa na saída (o menor dos canais, nunca abaixo de −48 dB), ou null.</summary>
    public decimal? TetoCerca => PorCanal.Count > 0 ? PorCanal.Min(c => c.TetoCerca) : null;

    /// <summary>"O limitador não chega ao limite físico desta saída." quando o teto de algum canal fica abaixo de −48 dB, ou null.</summary>
    public string? Aviso => PorCanal.Any(c => c.AbaixoDoMinimo) ? MsgAbaixoDoMinimo : null;
}

/// <summary>
/// O cálculo do limite físico (spec-rendimento CAP-1, <c>limites-e-niveis.md</c>): das fichas do Setup e
/// do corte de cada saída, o térmico, a excursão (com os três casos de dado faltando) e o ceifamento, e a
/// conversão para o limitador: dB = 20·log10(V / (2,0 × ganho)). Puro: o estado do aparelho entra por um
/// leitor de endereço (o espelho agora; a memória na 13c).
/// </summary>
public static class LimiteFisico
{
    public const string SeloVerificado = "verificado";
    public const string SeloEstimado = "estimado";

    /// <summary>A referência da escala do limitador: 0 dB = 2,0 V na entrada do módulo (MAPA §7).</summary>
    public const double ReferenciaLimitador = 2.0;

    /// <summary>O mínimo do limitador (<c>071</c>): um teto abaixo dele fica nele na cerca, com o aviso.</summary>
    public const decimal MinimoLimitador = -48m;

    /// <summary>O máximo do limitador (<c>071</c>): um teto acima dele fica nele na cerca.</summary>
    public const decimal MaximoLimitador = 0m;

    /// <summary>A margem do caso 3 da excursão: min(T, C) − 3 dB.</summary>
    public const double MargemCaso3Db = 3.0;

    public const string MsgSemAltoFalante = "Sem alto-falante passivo: o app não sabe o limite desta saída.";

    private static readonly CultureInfo Fio = CultureInfo.InvariantCulture;

    /// <summary>"térmico", "curso do cone" ou "ceifamento do módulo".</summary>
    public static string NomeLimite(TipoLimite tipo) => tipo switch
    {
        TipoLimite.Termico => "térmico",
        TipoLimite.Excursao => "curso do cone",
        _ => "ceifamento do módulo",
    };

    /// <summary>"termico", "excursao" ou "ceifamento" (a porta).</summary>
    public static string ChaveLimite(TipoLimite tipo) => tipo switch
    {
        TipoLimite.Termico => "termico",
        TipoLimite.Excursao => "excursao",
        _ => "ceifamento",
    };

    // ------------------------------------------------------------------ por Setup

    /// <summary>Os limites das saídas, na ordem delas. Sem equipamentos (sem Setup em uso), nenhum.</summary>
    public static IReadOnlyList<LimiteSaida> Calcular(IReadOnlyList<Equipamento>? equipamentos, IReadOnlyList<SaidaApp> saidas,
        Func<Endereco, Valor?> ler)
    {
        if (equipamentos is null || equipamentos.Count == 0)
            return [];
        var nomes = Ligacoes.NomesNaTela(saidas);
        return saidas.Select((s, i) => CalcularSaida(equipamentos, s, nomes[i], ler)).ToArray();
    }

    /// <summary>
    /// O limite de uma saída: o menor entre os alto-falantes passivos que os canais dela alimentam. O teto
    /// da cerca é por canal (o passa-alta e o ganho são de cada canal), com os alto-falantes calibrados.
    /// </summary>
    public static LimiteSaida CalcularSaida(IReadOnlyList<Equipamento> equipamentos, SaidaApp saida, string nome, Func<Endereco, Valor?> ler)
    {
        var caminhos = saida.Canais
            .SelectMany(c => Ligacoes.Caminhos(equipamentos, c).Select(x => CalcularCaminho(x, c, PassaAltaDe(ler, c))))
            .ToArray();
        if (caminhos.Length == 0)
            return new LimiteSaida(saida, nome, null, null, null, null, null, null, null, false, MsgSemAltoFalante, []);

        var comTeto = caminhos.Where(c => c.TetoV is not null).ToArray();
        if (comTeto.Length == 0)
        {
            var primeiro = caminhos[0];
            var faltas = new[] { primeiro.Termico.Falta, primeiro.Excursao.Falta, primeiro.Ceifamento.Falta }
                .OfType<string>().Distinct().ToArray();
            var motivo = $"Sem nenhum dos três limites do {primeiro.Caminho.AltoFalante.Nome}." + (faltas.Length > 0 ? " " + string.Join(" ", faltas) : "");
            return new LimiteSaida(saida, nome, primeiro.Caminho.AltoFalante.Nome, primeiro.Termico, primeiro.Excursao, primeiro.Ceifamento,
                null, null, null, false, motivo, []);
        }

        // A cerca em cada canal: o menor teto em dB entre os alto-falantes calibrados que ele alimenta.
        var porCanal = comTeto
            .Where(c => c.TetoDb is not null)
            .GroupBy(c => c.Canal)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var m = g.OrderBy(c => c.TetoDb).ThenBy(c => c.TetoV).First();
                return new LimiteCanal(g.Key, m.TetoDb!.Value, m.Manda!.Value, m.Caminho.AltoFalante.Nome);
            })
            .ToArray();

        var calibrada = comTeto.All(c => c.Ganho is not null);
        var manda = calibrada
            ? comTeto.OrderBy(c => c.TetoDb).ThenBy(c => c.TetoV).First()
            : comTeto.OrderBy(c => c.TetoV).First();
        return new LimiteSaida(saida, nome, manda.Caminho.AltoFalante.Nome, manda.Termico, manda.Excursao, manda.Ceifamento,
            manda.Manda, manda.TetoV, calibrada ? manda.TetoDb : null, calibrada, null, porCanal);
    }

    /// <summary>
    /// Os alto-falantes passivos do Setup que nenhum canal do processador alcança (desligados, ligados a um
    /// equipamento que não é amplificador, ou num canal do módulo sem sinal): ficam fora da conta, com o motivo.
    /// </summary>
    public static IReadOnlyList<AltoFalanteFora> ForaDaConta(IReadOnlyList<Equipamento>? equipamentos) =>
        equipamentos is null || equipamentos.Count == 0
            ? []
            : Ligacoes.ForaDosCaminhos(equipamentos).Select(f => new AltoFalanteFora(f.AltoFalante.Nome, f.Motivo)).ToArray();

    /// <summary>O passa-alta do canal no estado lido (o <c>042</c> e o <c>040</c>); sem leitura, sem filtro.</summary>
    public static PassaAlta? PassaAltaDe(Func<Endereco, Valor?> ler, int canal) =>
        PassaAlta.DoAparelho(ler(new Endereco("042", canal))?.Numero, ler(new Endereco("040", canal))?.Numero);

    // ------------------------------------------------------------------ por alto-falante

    /// <summary>T, X e C de um alto-falante pelo caminho de um canal, com o passa-alta desse canal (null = OFF).</summary>
    public static LimiteCaminho CalcularCaminho(CaminhoAltoFalante caminho, int canal, PassaAlta? passaAlta)
    {
        var af = caminho.AltoFalante;
        var t = Termico(af);
        var c = Ceifamento(caminho);
        var x = Excursao(af, passaAlta, t, c);

        var ganhoDado = caminho.Ponte ? caminho.Modulo.Dados.GanhoPonte : caminho.Modulo.Dados.Ganho;
        decimal? ganho = ganhoDado is { Origem: OrigemDado.Medido } g ? g.Valor : null;

        TipoLimite? manda = null;
        double? teto = null;
        foreach (var l in new[] { t, x, c })
        {
            if (l.V is { } v && (teto is null || v < teto))
            {
                teto = v;
                manda = l.Tipo;
            }
        }
        decimal? tetoDb = teto is { } tv && ganho is { } gv ? ParaDb(tv, gv) : null;
        return new LimiteCaminho(caminho, canal, t, x, c, ganho, manda, teto, tetoDb);
    }

    /// <summary>dB = 20·log10(V / (2,0 × ganho)), arredondado para baixo no passo do limitador (0,1).</summary>
    public static decimal ParaDb(double volts, decimal ganho)
    {
        var db = 20 * Math.Log10(volts / (ReferenciaLimitador * (double)ganho));
        // O arredondamento a 6 casas tira o ruído do double antes do piso (−67,999999… não vira −68,1).
        return (decimal)Math.Floor(Math.Round(db * 10, 6)) / 10m;
    }

    /// <summary>T = √(PotenciaRms × Impedancia).</summary>
    public static Limite Termico(Equipamento af)
    {
        var d = af.Dados;
        var faltam = new List<string>();
        if (d.PotenciaRms is null)
            faltam.Add("a potência RMS");
        if (d.Impedancia is null)
            faltam.Add("a impedância");
        if (faltam.Count > 0)
            return new Limite(TipoLimite.Termico, null, false, true, null, MsgFalta(faltam, af.Nome));
        var p = (double)d.PotenciaRms!.Valor;
        var z = (double)d.Impedancia!.Valor;
        var v = Math.Sqrt(p * z);
        var conta = $"√({Num(d.PotenciaRms.Valor)} W × {Num(d.Impedancia.Valor)} Ω) = {Volts(v)} V";
        return new Limite(TipoLimite.Termico, v, false, !(d.PotenciaRms.Verificado && d.Impedancia.Verificado), conta, null);
    }

    /// <summary>C = a tensão de ceifamento do canal do módulo; no bridge, a da ponte.</summary>
    public static Limite Ceifamento(CaminhoAltoFalante caminho)
    {
        var m = caminho.Modulo;
        var dado = caminho.Ponte ? m.Dados.TensaoCeifamentoPonte : m.Dados.TensaoCeifamentoCanal;
        var onde = caminho.Bridge is { } b ? $"da ponte {b.Rotulo} do {m.Nome}" : $"do canal {caminho.CanalModulo} do {m.Nome}";
        if (dado is null)
            return new Limite(TipoLimite.Ceifamento, null, false, true, null, $"Falta a tensão de ceifamento {onde}.");
        var v = (double)dado.Valor;
        return new Limite(TipoLimite.Ceifamento, v, false, !dado.Verificado, $"Ceifamento {onde}: {Volts(v)} V", null);
    }

    /// <summary>
    /// X pelos três casos (<c>limites-e-niveis.md</c>): 1) os dados completos dão X pela conta; 2) senão,
    /// uma caixa dutada com Fb e passa-alta de pelo menos 24 dB/oit com 0,75·Fb ≤ fc ≤ 0,8·Fb dá X = ∞
    /// (estimado); 3) senão, X = min(T, C) − 3 dB (estimado, com o dado que falta). Sem T nem C no caso 3,
    /// não dá para calcular.
    /// </summary>
    public static Limite Excursao(Equipamento af, PassaAlta? passaAlta, Limite termico, Limite ceifamento)
    {
        var d = af.Dados;
        var tipo = TipoDaCaixa(d.TipoCaixa?.Valor);
        var faltam = new List<string>();
        void Exigir(DadoTecnico? dado, string nome)
        {
            if (dado is null)
                faltam.Add(nome);
        }
        Exigir(d.Re, "a Re");
        Exigir(d.Fs, "a Fs");
        Exigir(d.Qms, "o Qms");
        Exigir(d.Vas, "o Vas");
        Exigir(d.Sd, "o Sd");
        Exigir(d.Xmax, "o Xmax");
        if (d.Bl is null && d.Qes is null)
            faltam.Add("o Bl ou o Qes");
        if (tipo is null)
            faltam.Add("o tipo da caixa (selada ou dutada)");
        Exigir(d.VolumeCaixa, "o volume da caixa");
        if (tipo == TipoCaixaModelo.Dutada)
            Exigir(d.Fb, "a Fb da caixa");

        if (faltam.Count == 0)
            return ExcursaoPelaConta(af, tipo!.Value, passaAlta);

        var falta = MsgFalta(faltam, af.Nome);
        // Caso 2: o duto protege o cone com o subsônico logo abaixo da Fb.
        if (tipo == TipoCaixaModelo.Dutada && d.Fb is { } fbDado && passaAlta is { Inclinacao: >= 24 } pa)
        {
            var fb = (double)fbDado.Valor;
            if (pa.Frequencia >= 0.75 * fb && pa.Frequencia <= 0.8 * fb)
            {
                var conta = $"Caixa dutada com Fb de {Num(fbDado.Valor)} Hz e passa-alta {pa.Rotulo} em {Num((decimal)pa.Frequencia)} Hz " +
                            "(entre 0,75 e 0,8 × Fb): o duto protege o cone.";
                return new Limite(TipoLimite.Excursao, null, true, true, conta, falta);
            }
        }

        // Caso 3: min(T, C) − 3 dB.
        var base3 = new[] { termico, ceifamento }.Where(l => l.V is not null).Select(l => (l.Tipo, V: l.V!.Value)).ToArray();
        if (base3.Length == 0)
            return new Limite(TipoLimite.Excursao, null, false, true, null, falta);
        var menor = base3.MinBy(b => b.V);
        var v = menor.V * Math.Pow(10, -MargemCaso3Db / 20);
        var conta3 = $"Sem a conta do cone: {NomeLimite(menor.Tipo)} ({Volts(menor.V)} V) − 3 dB = {Volts(v)} V";
        return new Limite(TipoLimite.Excursao, v, false, true, conta3, falta);
    }

    private static Limite ExcursaoPelaConta(Equipamento af, TipoCaixaModelo tipo, PassaAlta? passaAlta)
    {
        var d = af.Dados;
        var modelo = new AltoFalanteModelo(
            Re: (double)d.Re!.Valor,
            Fs: (double)d.Fs!.Valor,
            Qms: (double)d.Qms!.Valor,
            Vas: (double)d.Vas!.Valor / 1000,
            Sd: (double)d.Sd!.Valor / 1e4,
            Xmax: (double)d.Xmax!.Valor / 1000,
            Bl: d.Bl is { } bl ? (double)bl.Valor : null,
            Qes: d.Qes is { } qes ? (double)qes.Valor : null,
            Le: d.Le is { } le ? (double)le.Valor / 1000 : 0);
        var caixa = new CaixaModelo(tipo, (double)d.VolumeCaixa!.Valor / 1000, d.Fb is { } fb ? (double)fb.Valor : null);
        var r = ModeloExcursao.TensaoNoXmax(modelo, caixa, passaAlta);

        var usados = new List<DadoTecnico> { d.Re!, d.Fs!, d.Qms!, d.Vas!, d.Sd!, d.Xmax!, d.VolumeCaixa! };
        usados.Add(d.Bl ?? d.Qes!);
        if (d.Le is { } leUsado)
            usados.Add(leUsado);
        if (tipo == TipoCaixaModelo.Dutada)
            usados.Add(d.Fb!);
        var verificado = usados.All(u => u.Verificado) && d.TipoCaixa!.Verificado;

        var caixaTexto = tipo == TipoCaixaModelo.Selada
            ? $"caixa selada de {Num(d.VolumeCaixa!.Valor)} L"
            : $"caixa dutada de {Num(d.VolumeCaixa!.Valor)} L com Fb de {Num(d.Fb!.Valor)} Hz";
        var filtroTexto = passaAlta is { } pa ? $"passa-alta {pa.Rotulo} em {Num((decimal)pa.Frequencia)} Hz" : "sem passa-alta";
        var conta = $"Xmax de {Num(d.Xmax!.Valor)} mm na pior frequência ({Num(decimal.Round((decimal)r.PiorFrequencia))} Hz), " +
                    $"{caixaTexto}, {filtroTexto}: {Volts(r.Volts)} V";
        return new Limite(TipoLimite.Excursao, r.Volts, false, !verificado, conta, null);
    }

    private static readonly HashSet<string> PalavrasSelada = ["selada", "selado", "fechada", "fechado", "sealed"];
    private static readonly HashSet<string> PalavrasDutada = ["dutada", "dutado", "duto", "reflex", "vented", "ported"];
    private static readonly HashSet<string> Negacoes = ["sem", "nao"];

    /// <summary>
    /// O tipo da caixa no texto livre, por palavra inteira e sem diferença de caixa nem de acento:
    /// selada, selado, fechada, fechado ou sealed → selada; dutada, dutado, duto, reflex (bass-reflex),
    /// vented ou ported → dutada. Uma palavra depois de "sem" ou "não" não conta. Os dois tipos juntos, ou
    /// nenhum, dão null (dado que falta).
    /// </summary>
    public static TipoCaixaModelo? TipoDaCaixa(string? texto)
    {
        var semAcento = SemAcento(texto);
        var palavras = semAcento.Split(semAcento.Where(c => !char.IsLetter(c)).Distinct().ToArray(), StringSplitOptions.RemoveEmptyEntries);
        bool Tem(HashSet<string> tipo) =>
            palavras.Where((p, i) => tipo.Contains(p) && (i == 0 || !Negacoes.Contains(palavras[i - 1]))).Any();
        var selada = Tem(PalavrasSelada);
        var dutada = Tem(PalavrasDutada);
        if (selada == dutada)
            return null;
        return selada ? TipoCaixaModelo.Selada : TipoCaixaModelo.Dutada;
    }

    // ------------------------------------------------------------------ textos

    /// <summary>"Falta a Fb da caixa do Sub." / "Faltam o Vas e o Sd do Sub."</summary>
    private static string MsgFalta(IReadOnlyList<string> itens, string nome) =>
        $"{(itens.Count == 1 ? "Falta" : "Faltam")} {Ligacoes.Lista(itens)} do {nome}.";

    private static string SemAcento(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return "";
        var sem = new StringBuilder();
        foreach (var c in texto.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sem.Append(c);
        }
        return string.Join(' ', sem.ToString().Normalize(NormalizationForm.FormC).Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>"17,9".</summary>
    internal static string Volts(double v) => v.ToString("0.0", Fio).Replace('.', ',');

    /// <summary>"3,5", "80", "1,75".</summary>
    private static string Num(decimal v) => v.ToString("0.##", Fio).Replace('.', ',');

    /// <summary>"−6,8", "+0,5", "0,0": vírgula, menos verdadeiro e sinal.</summary>
    public static string Db(decimal db)
    {
        var texto = Math.Abs(db).ToString("0.0", Fio).Replace('.', ',');
        return db < 0 ? "−" + texto : db > 0 ? "+" + texto : texto;
    }
}
