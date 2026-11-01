using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Setups;

namespace BgsControle.Nucleo.Rendimento;

/// <summary>
/// O maior nível de um canal na saída do processador, sem o master, para um tom de 0 dBFS (história 13c): o
/// pico em dB da escala do limitador (0 dB = 2,0 V) e a frequência dele, ou o dado da cadeia que falta.
/// </summary>
/// <param name="Canal">O canal do processador (0 a 3).</param>
/// <param name="Db">O pico sem o master, ou null com o dado que falta.</param>
/// <param name="Frequencia">A frequência do pico, em Hz.</param>
/// <param name="Falta">"O app não sabe o Ganho da saída 1.", ou null.</param>
public sealed record PicoCanal(int Canal, double? Db, double Frequencia, string? Falta);

/// <summary>
/// Um canal na conta do master (história 13c): o teto físico do nível (min(T, X, C), ou min(X, C) no Máximo)
/// em dB do limitador e em volts, o pico sem o master e a folga (teto − margem − pico), que é o master que
/// deixa este canal no teto do nível.
/// </summary>
/// <param name="Saida">O nome da saída na tela ("JBL L").</param>
/// <param name="TetoDb">O teto físico do nível, sem a margem, em dB do limitador.</param>
/// <param name="TetoV">O teto físico em volts RMS, ou null.</param>
/// <param name="Folga">teto − margem − pico, em dB: o maior master para este canal.</param>
public sealed record CanalNivel(string Saida, int Canal, decimal TetoDb, double? TetoV, TipoLimite Manda, string AltoFalante,
    double PicoSemMaster, double FrequenciaPico, double Folga);

/// <summary>
/// A conta de um nível (história 13c): o master do nível na cadeia lida, os canais que entraram e o motivo de
/// não ter conta (o dado que falta). Com <see cref="FaltaGanho"/>, a conta passou de 0 dB: o master fica em 0 e
/// o nível não chega ao teto.
/// </summary>
/// <param name="Master">O master do nível (−80 a 0, no passo de 0,1), ou null sem conta.</param>
/// <param name="Falta">Por que não há conta, ou null.</param>
/// <param name="FaltaGanho">A saída que fica abaixo do teto com o master em 0 (falta ganho na memória), ou null.</param>
/// <param name="Canais">Os canais calibrados e com limite, com a folga de cada um.</param>
/// <param name="MasterCurto">A saída que fica acima do teto com o master em −80 dB (o master não desce o bastante), ou null.</param>
public sealed record ContaNivel(NivelRendimento Nivel, decimal? Master, string? Falta, string? FaltaGanho, IReadOnlyList<CanalNivel> Canais,
    string? MasterCurto = null)
{
    /// <summary>
    /// "O nível não chega ao teto: falta ganho na memória. Saída: JBL L.", "O nível não chega ao teto: o master não
    /// desce o bastante. Saída: JBL L.", ou null.
    /// </summary>
    public string? Aviso => FaltaGanho is { } saida ? Niveis.MsgFaltaGanhoNa(saida)
        : MasterCurto is { } curta ? Niveis.MsgMasterCurtoNa(curta)
        : null;

    /// <summary>O canal que manda no master (a menor folga), ou null sem conta.</summary>
    public CanalNivel? QueManda => Canais.Count == 0 ? null : Canais.MinBy(c => c.Folga);

    /// <summary>
    /// As saídas que mandam no master (as de menor folga, até 0,05 dB do mínimo; "JBL L" e "JBL R" com a mesma
    /// ficha), na ordem das saídas, sem repetir.
    /// </summary>
    public IReadOnlyList<string> SaidasQueMandam =>
        QueManda is { } m ? Canais.Where(c => c.Folga - m.Folga < 0.05).Select(c => c.Saida).Distinct().ToArray() : [];

    /// <summary>O teto do nível em volts na saída que manda: o teto físico menos a margem, ou null.</summary>
    public double? TetoNivelV => QueManda?.TetoV is { } v ? v * Math.Pow(10, -(double)Niveis.Margem(Nivel) / 20) : null;
}

/// <summary>
/// O pior caso de uma memória (história 13c, spec-rendimento CAP-4): o maior nível na saída do processador, em
/// dB da escala do limitador (0 dB = 2,0 V), de um tom de 0 dBFS em qualquer frequência (a grade logarítmica de
/// 10 Hz a 20 kHz do <see cref="ModeloExcursao"/>). O nível é a soma:
/// <list type="bullet">
/// <item>o nível do Bluetooth do Setup (13b);</item>
/// <item>o EQ de entrada (<c>030</c>–<c>032</c>), em sino: a = 10^(g/40), (s² + s·a/Q + 1)/(s² + s/(a·Q) + 1);</item>
/// <item>as 15 bandas (<c>020</c>), um sino por banda em <see cref="Catalogo.BandasHz"/>, com Q = 2,1
/// [ASSUMPTION: o Q do gráfico; a 12d mede];</item>
/// <item>a rota (<c>010</c>): "a" ou "b" somam 0 dB e "a+b" soma +6 dB [ASSUMPTION: soma coerente, o pior caso];</item>
/// <item>o ganho (<c>081</c>) e o EQ de saída (<c>090</c>–<c>092</c>, em sino) do canal;</item>
/// <item>os cortes (<c>040</c>–<c>043</c>), em módulo LR/BT, como na 13a e na 13b;</item>
/// <item>e o master (<c>080</c>), que fica de fora do pico (é o que a conta acha).</item>
/// </list>
/// O limitador não entra: ele é a segurança, e o master é posto antes dele. Puro: a cadeia entra por um leitor
/// de endereço (o espelho, ou a memória e o preset no aplicar).
/// </summary>
public static class PiorCaso
{
    /// <summary>O Q de cada banda do EQ gráfico [ASSUMPTION: o Q do gráfico; a 12d mede].</summary>
    public const double QBandas = 2.1;

    /// <summary>A rota "a+b" soma as duas entradas: +6 dB [ASSUMPTION: soma coerente, o pior caso].</summary>
    public const double SomaAmbas = 6.0;

    public const string CodigoMaster = Camadas.CodigoMaster;

    /// <summary>A grade da conta: a do modelo de excursão (logarítmica, 10 Hz a 20 kHz, 4000 pontos).</summary>
    public static IReadOnlyList<double> Grade => ModeloExcursao.Grade;

    /// <summary>"O app não sabe o Ganho da saída 1."</summary>
    public static string MsgFalta(string codigo, int? indice) => $"O app não sabe o valor de {Camadas.Rotulo(codigo, indice)}.";

    // ------------------------------------------------------------------ os sinos

    /// <summary>
    /// O ganho em dB, na frequência <paramref name="f"/>, de um sino em <paramref name="f0"/> com o ganho
    /// <paramref name="ganhoDb"/> e o Q: |(s² + s·a/Q + 1)/(s² + s/(a·Q) + 1)| com s = j·f/f0 e a = 10^(g/40).
    /// </summary>
    public static double Sino(double f, double f0, double ganhoDb, double q)
    {
        if (ganhoDb == 0)
            return 0;
        var a = Math.Pow(10, ganhoDb / 40);
        var w = f / f0;
        var real = 1 - w * w;
        var num = real * real + Math.Pow(w * a / q, 2);
        var den = real * real + Math.Pow(w / (a * q), 2);
        return 10 * Math.Log10(num / den);
    }

    // ------------------------------------------------------------------ a resposta

    /// <summary>
    /// A resposta do canal, sem o master, em cada ponto da <see cref="Grade"/> (dB da escala do limitador), ou o
    /// dado que falta.
    /// </summary>
    public static (double[]? Db, string? Falta) Resposta(Func<Endereco, Valor?> ler, int canal, decimal nivelBluetooth)
    {
        var global = RespostaGlobal(ler, nivelBluetooth);
        if (global.Falta is not null)
            return (null, global.Falta);
        return RespostaDoCanal(ler, canal, global.Db!);
    }

    /// <summary>O pico do canal sem o master (o maior ponto da resposta) e a frequência dele, ou o dado que falta.</summary>
    public static PicoCanal Pico(Func<Endereco, Valor?> ler, int canal, decimal nivelBluetooth)
    {
        var (db, falta) = Resposta(ler, canal, nivelBluetooth);
        return PicoDe(canal, db, falta);
    }

    /// <summary>O nível na saída do canal com o master lido (o pior caso que vai para o limitador), ou null.</summary>
    public static double? ComMaster(Func<Endereco, Valor?> ler, int canal, decimal nivelBluetooth) =>
        Pico(ler, canal, nivelBluetooth).Db is { } pico && ler(new Endereco(CodigoMaster, null))?.Numero is { } master
            ? pico + (double)master
            : null;

    /// <summary>dB do limitador → volts na saída do módulo: V = 2,0 × 10^(dB/20) × ganho.</summary>
    public static double Volts(double db, decimal ganho) => LimiteFisico.ReferenciaLimitador * Math.Pow(10, db / 20) * (double)ganho;

    private static PicoCanal PicoDe(int canal, double[]? db, string? falta)
    {
        if (db is null)
            return new PicoCanal(canal, null, 0, falta);
        var i = 0;
        for (var k = 1; k < db.Length; k++)
        {
            if (db[k] > db[i])
                i = k;
        }
        return new PicoCanal(canal, db[i], Grade[i], null);
    }

    /// <summary>O que vale para todos os canais: o Bluetooth, o EQ de entrada e as 15 bandas.</summary>
    private static (double[]? Db, string? Falta) RespostaGlobal(Func<Endereco, Valor?> ler, decimal nivelBluetooth)
    {
        var grade = Grade;
        var db = new double[grade.Count];
        Array.Fill(db, (double)nivelBluetooth);

        var (entrada, faltaEntrada) = Sinos(ler, null, "030", "031", "032");
        if (faltaEntrada is not null)
            return (null, faltaEntrada);
        if (entrada is { } e)
            Somar(db, f => Sino(f, e.F0, e.Ganho, e.Q));

        for (var b = 0; b < Catalogo.BandasHz.Count; b++)
        {
            if (ler(new Endereco("020", b))?.Numero is not { } g)
                return (null, MsgFalta("020", b));
            if (g == 0)
                continue;
            var f0 = Catalogo.BandasHz[b];
            Somar(db, f => Sino(f, f0, (double)g, QBandas));
        }
        return (db, null);
    }

    /// <summary>A resposta global somada à do canal: a rota, o ganho, o EQ de saída e os cortes.</summary>
    private static (double[]? Db, string? Falta) RespostaDoCanal(Func<Endereco, Valor?> ler, int canal, double[] global)
    {
        var db = (double[])global.Clone();
        if (ler(new Endereco("010", canal)) is not { } rota)
            return (null, MsgFalta("010", canal));
        // "a" ou "b": uma entrada; qualquer outra (a+b): as duas somadas, o pior caso.
        var somaRota = rota.Texto is "a" or "b" ? 0 : SomaAmbas;
        if (ler(new Endereco(Camadas.CodigoGanho, canal))?.Numero is not { } ganho)
            return (null, MsgFalta(Camadas.CodigoGanho, canal));
        var fixo = somaRota + (double)ganho;
        for (var i = 0; i < db.Length; i++)
            db[i] += fixo;

        var (saida, faltaSaida) = Sinos(ler, canal, "091", "090", "092");
        if (faltaSaida is not null)
            return (null, faltaSaida);
        if (saida is { } s)
            Somar(db, f => Sino(f, s.F0, s.Ganho, s.Q));

        // Os cortes só pelo módulo (≤ 1): um filtro em OFF, sem leitura ou desconhecido fica de fora (o lado conservador).
        var passaAlta = LimiteFisico.PassaAltaDe(ler, canal);
        var passaBaixa = PassaBaixa.DoAparelho(ler(new Endereco("043", canal))?.Numero, ler(new Endereco("041", canal))?.Numero);
        if (passaAlta is not null || passaBaixa is not null)
            Somar(db, f => 20 * Math.Log10((passaAlta?.Modulo(f) ?? 1) * (passaBaixa?.Modulo(f) ?? 1)));
        return (db, null);
    }

    /// <summary>Um sino do aparelho (ganho, frequência, Q), ou null com o ganho em 0; ou o dado que falta.</summary>
    internal static ((double Ganho, double F0, double Q)? Sino, string? Falta) Sinos(Func<Endereco, Valor?> ler, int? canal,
        string codigoGanho, string codigoFrequencia, string codigoQ)
    {
        if (ler(new Endereco(codigoGanho, canal))?.Numero is not { } g)
            return (null, MsgFalta(codigoGanho, canal));
        if (g == 0)
            return (null, null);
        if (ler(new Endereco(codigoFrequencia, canal))?.Numero is not { } f0 || f0 <= 0)
            return (null, MsgFalta(codigoFrequencia, canal));
        if (ler(new Endereco(codigoQ, canal))?.Numero is not { } q || q <= 0)
            return (null, MsgFalta(codigoQ, canal));
        return (((double)g, (double)f0, (double)q), null);
    }

    private static void Somar(double[] db, Func<double, double> resposta)
    {
        var grade = Grade;
        for (var i = 0; i < db.Length; i++)
            db[i] += resposta(grade[i]);
    }

    // ------------------------------------------------------------------ o master do nível

    /// <summary>
    /// A conta do nível: master = min sobre os canais calibrados e com limite de (teto do nível − margem − pico
    /// sem o master), arredondado para baixo no passo 0,1, dentro de −80..0. Acima de 0, fica em 0 e a conta
    /// leva a saída que falta ganho (<see cref="ContaNivel.FaltaGanho"/>). O teto é min(T, X, C) do Baixo ao Alto
    /// e min(X, C) no Máximo, com o X pelo corte da cadeia lida.
    /// </summary>
    /// <param name="equipamentos">Os equipamentos do Setup.</param>
    /// <param name="saidas">As saídas do app (com o link que vale).</param>
    /// <param name="ler">A cadeia: o espelho, ou a memória e o preset.</param>
    /// <param name="nivelBluetooth">O nível do Bluetooth do Setup, em dB do limitador.</param>
    public static ContaNivel Conta(IReadOnlyList<Equipamento> equipamentos, IReadOnlyList<SaidaApp> saidas, Func<Endereco, Valor?> ler,
        decimal nivelBluetooth, NivelRendimento nivel)
    {
        var limites = LimiteFisico.Calcular(equipamentos, saidas, ler, Niveis.SemTermico(nivel));
        var global = RespostaGlobal(ler, nivelBluetooth);
        if (global.Falta is { } faltaGlobal)
            return new ContaNivel(nivel, null, faltaGlobal, null, []);

        var margem = (double)Niveis.Margem(nivel);
        var canais = new List<CanalNivel>();
        foreach (var limite in limites)
        {
            if (!limite.Calibrada || limite.TetoDb is null)
                continue;
            foreach (var c in limite.PorCanal)
            {
                var (db, falta) = RespostaDoCanal(ler, c.Canal, global.Db!);
                var pico = PicoDe(c.Canal, db, falta);
                if (pico.Db is not { } p)
                    return new ContaNivel(nivel, null, pico.Falta, null, canais);
                canais.Add(new CanalNivel(limite.Nome, c.Canal, c.TetoDb, c.TetoV, c.Manda, c.AltoFalante, p, pico.Frequencia,
                    (double)c.TetoDb - margem - p));
            }
        }
        if (canais.Count == 0)
            return new ContaNivel(nivel, null, Niveis.MsgSemLimite, null, canais);

        var manda = canais.MinBy(c => c.Folga)!;
        // O arredondamento a 6 casas tira o ruído do double antes do piso (como no LimiteFisico.ParaDb).
        var master = (decimal)Math.Floor(Math.Round(manda.Folga * 10, 6)) / 10m;
        string? faltaGanho = null;
        string? curto = null;
        if (master > 0)
        {
            master = 0;
            faltaGanho = manda.Saida;
        }
        else if (master < MinimoMaster)
        {
            // A saída fica acima do teto mesmo com o master no mínimo: o nível avisa.
            master = MinimoMaster;
            curto = manda.Saida;
        }
        return new ContaNivel(nivel, master, null, faltaGanho, canais, curto);
    }

    /// <summary>O mínimo do master (<c>080</c>).</summary>
    public const decimal MinimoMaster = -80m;
}
