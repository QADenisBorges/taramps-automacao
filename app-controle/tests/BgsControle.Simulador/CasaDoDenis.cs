using System.Globalization;
using System.Text.Json;
using BgsControle.Nucleo;
using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Leitura;
using BgsControle.Nucleo.Setups;

namespace BgsControle.Simulador;

/// <summary>
/// A fixture com os dados reais do Denis (história 8g): o Setup "Casa 2.1" com os equipamentos e as
/// fichas (cada dado com a origem), os 3 modelos da base de modelos e a leitura completa de 18/10. Até a
/// 8g, isto vinha embutido no app (o conhecimento herdado); o app agora nasce zerado (CAP-6, decisão do
/// Denis de 30/10/2026), e os dados ficam só nos testes. Os testes que precisam do Casa 2.1 semeiam o
/// <c>setups.json</c> (<see cref="SetupsJson"/>) e o <c>modelos.json</c> (<see cref="ModelosJson"/>);
/// o <see cref="CenarioFilme"/> acrescenta a FILME e o Cinema.
/// </summary>
public static class CasaDoDenis
{
    /// <summary>O recurso embutido: as respostas cruas da leitura completa de 18/10/2026.</summary>
    private const string RecursoFilme = "leitura-2026-10-18_2145.json";

    /// <summary>O id que o app dava ao Casa 2.1 (um <c>setups.json</c> do celular do Denis ainda o tem).</summary>
    public const string IdSetupCasa = "herdado-setup-casa-2-1";

    public const string NomeSetupCasa = "Casa 2.1";

    /// <summary>Os ids dos equipamentos do Casa 2.1 (conhecimento-herdado.md, CAP-16).</summary>
    public const string IdModuloCasa = "herdado-modulo";

    public const string IdJblL = "herdado-jbl-l";
    public const string IdJblR = "herdado-jbl-r";
    public const string IdSubCasa = "herdado-sub";

    public const string NomeModuloCasa = "Módulo SD800.4 EVO";

    public const string ModeloJbl = "JBL 4TRMS80";
    public const string ModeloSub = "Protech Light 8\"";
    public const string ModeloModulo = "SD800.4 EVO";

    /// <summary>Quando o Casa 2.1 foi refeito só com o físico (decisão do Denis).</summary>
    public static DateTimeOffset QuandoCasa { get; } = new(2026, 10, 29, 0, 0, 0, TimeSpan.FromHours(-3));

    public const string NotaJbl = "Passa-alta mínimo de 100 Hz a 24 dB/oit (ficha do fabricante). O Bl não fecha com o Qes.";
    public const string NotaModuloCasa = "Ponte em 2 Ω proibida (manual). Alimentado pela BOB 60A (40 A contínuos).";
    public const string NotaSubCasa = "Qts 0,52 (site do fabricante).";

    private const string FonteJbl = "Ficha impressa do fabricante (JBL 4TRMS80)";
    private const string FonteProtech = "Site do fabricante, lido em 27/10/2026";
    private const string FonteManualModulo = "Manual do SD800.4 EVO";
    private const string FonteMedidoJbl = "Medido em 18/10/2026";
    private const string FonteEstimadoJbl = "Estimado por foto e régua";
    private const string FonteMedidoSub = "Medido em 17 e 20/10/2026";
    private const string FonteMedidoModulo = "Medido em 17 e 18/10/2026";

    private static DadoTecnico Oficial(decimal v, string fonte) => new(v, OrigemDado.Oficial, fonte);

    private static DadoTecnico Medido(decimal v, string fonte) => new(v, OrigemDado.Medido, fonte);

    /// <summary>A ficha oficial da JBL 4TRMS80 (ficha impressa).</summary>
    public static DadosTecnicos FichaJbl { get; } = new()
    {
        PotenciaRms = Oficial(80m, FonteJbl),
        Impedancia = Oficial(4m, FonteJbl),
        Re = Oficial(3.4m, FonteJbl),
        Fs = Oficial(98m, FonteJbl),
        Qes = Oficial(0.81m, FonteJbl),
        Qms = Oficial(3.19m, FonteJbl),
        Vas = Oficial(2.06m, FonteJbl),
        Sd = Oficial(64m, FonteJbl),
        Bl = Oficial(4.1m, FonteJbl),
        Xmax = Oficial(1.75m, FonteJbl),
        Sensibilidade = Oficial(87m, FonteJbl),
    };

    /// <summary>A ficha oficial do Protech Light 8" (site do fabricante, 27/10).</summary>
    public static DadosTecnicos FichaSub { get; } = new()
    {
        Diametro = Oficial(8m, FonteProtech),
        PotenciaRms = Oficial(450m, FonteProtech),
        Impedancia = Oficial(4m, FonteProtech),
        Re = Oficial(3.7m, FonteProtech),
        Fs = Oficial(46m, FonteProtech),
        Qms = Oficial(3.90m, FonteProtech),
        Qes = Oficial(0.61m, FonteProtech),
        Vas = Oficial(10m, FonteProtech),
        Bl = Oficial(13m, FonteProtech),
        Xmax = Oficial(8m, FonteProtech),
        Le = Oficial(1.63m, FonteProtech),
        Sd = Oficial(240m, FonteProtech),
        Sensibilidade = Oficial(84m, FonteProtech),
    };

    /// <summary>A ficha oficial do SD800.4 EVO (manual): 132 W por canal em 4 Ω (ceifa ~23 V) e 400 W em ponte (ceifa ~40 V).</summary>
    public static DadosTecnicos FichaModulo { get; } = new()
    {
        Canais = Oficial(4m, FonteManualModulo),
        PotenciaCanal = Oficial(132m, FonteManualModulo),
        TensaoCeifamentoCanal = Oficial(23m, FonteManualModulo),
        PotenciaPonte = Oficial(400m, FonteManualModulo),
        TensaoCeifamentoPonte = Oficial(40m, FonteManualModulo),
    };

    /// <summary>
    /// Os equipamentos do Casa 2.1 (decisões do Denis de 23/10 e de 29/10/2026): o Módulo SD800.4 EVO
    /// (grupo 2, 4 canais alimentados pelo CH1 a CH4, bridge 3+4), a JBL L no canal 1 dele, a JBL R no
    /// canal 2 e o Sub (Protech Light 8") no bridge 3+4. O medido na instalação vale sobre o oficial.
    /// </summary>
    public static IReadOnlyList<Equipamento> EquipamentosCasa { get; } =
    [
        new Equipamento(IdModuloCasa, GrupoEquipamento.Amplificador, "módulo", NomeModuloCasa, ModeloModulo)
        {
            Dados = FichaModulo with
            {
                Ganho = Medido(19.5m, FonteMedidoModulo),
                GanhoPonte = Medido(40.5m, FonteMedidoModulo),
                TensaoAlimentacao = Medido(13.89m, "Medido sem carga em 17/10/2026"),
                CorrenteAlimentacao = Oficial(40m, "BOB 60A: 40 A contínuos (fabricante)"),
            },
            Entradas = Enumerable.Range(1, 4).Select(n => new Entrada(n, FonteSinal.DoCanal(n - 1))).ToArray(),
            Bridges = [new Bridge(3, 4)],
            Nota = NotaModuloCasa,
        },
        new Equipamento(IdJblL, GrupoEquipamento.AltoFalante, "médio-grave", "JBL L", ModeloJbl)
        {
            Dados = FichaJblCasa,
            Ligado = LigacaoAltoFalante.NoCanal(IdModuloCasa, 1),
            Nota = NotaJbl,
        },
        new Equipamento(IdJblR, GrupoEquipamento.AltoFalante, "médio-grave", "JBL R", ModeloJbl)
        {
            Dados = FichaJblCasa,
            Ligado = LigacaoAltoFalante.NoCanal(IdModuloCasa, 2),
            Nota = NotaJbl,
        },
        new Equipamento(IdSubCasa, GrupoEquipamento.AltoFalante, "subwoofer", "Sub", ModeloSub)
        {
            Dados = FichaSub with
            {
                Re = Medido(3.7m, FonteMedidoSub),
                TipoCaixa = new DadoTexto("dutada", OrigemDado.Medido, FonteMedidoSub),
                VolumeCaixa = new DadoTecnico(28m, OrigemDado.Estimado, "Estimado pelas medidas da caixa, ~28 L"),
                Fb = Medido(51m, FonteMedidoSub),
                AreaDuto = Medido(38.5m, FonteMedidoSub),
            },
            Ligado = LigacaoAltoFalante.NoBridge(IdModuloCasa, new Bridge(3, 4)),
            Nota = NotaSubCasa,
        },
    ];

    /// <summary>A JBL do Casa 2.1: a ficha oficial, com a Re medida e a caixa selada de ~3,5 L.</summary>
    private static DadosTecnicos FichaJblCasa => FichaJbl with
    {
        Re = Medido(3.6m, FonteMedidoJbl),
        TipoCaixa = new DadoTexto("selada", OrigemDado.Medido, FonteMedidoJbl),
        VolumeCaixa = new DadoTecnico(3.5m, OrigemDado.Estimado, FonteEstimadoJbl),
    };

    /// <summary>Os 3 modelos da base (só os dados oficiais): JBL 4TRMS80, Protech Light 8" e SD800.4 EVO.</summary>
    public static IReadOnlyList<ModeloGuardado> Modelos { get; } =
    [
        new(ModeloJbl, GrupoEquipamento.AltoFalante, "médio-grave", FichaJbl, QuandoCasa),
        new(ModeloSub, GrupoEquipamento.AltoFalante, "subwoofer", FichaSub, QuandoCasa),
        new(ModeloModulo, GrupoEquipamento.Amplificador, "módulo", FichaModulo, QuandoCasa),
    ];

    /// <summary>O Casa 2.1: os equipamentos e as fichas, sem memórias e sem ambiente.</summary>
    public static Setup SetupCasa { get; } = new(IdSetupCasa, NomeSetupCasa, QuandoCasa) { Equipamentos = EquipamentosCasa };

    /// <summary>O equipamento do Casa 2.1 pelo id, ou null.</summary>
    public static Equipamento? EquipamentoCasa(string? id) => EquipamentosCasa.FirstOrDefault(e => e.Id == id);

    /// <summary>Só o Casa 2.1, sem memórias, como último Setup usado.</summary>
    public static DadosSetups Dados { get; } = new(
        [SetupCasa],
        [],
        IdSetupCasa,
        new Dictionary<string, UsoDoSetup> { [IdSetupCasa] = UsoDoSetup.Vazio });

    /// <summary>O <c>setups.json</c> (formato 3) com só o Casa 2.1.</summary>
    public static string SetupsJson => GerenteSetups.ParaJson(Dados);

    /// <summary>O <c>modelos.json</c> com os 3 modelos.</summary>
    public static string ModelosJson => BaseModelos.ParaJson(Modelos);

    private static readonly Lazy<(DateTimeOffset Quando, IReadOnlyList<ValorCopiado> Valores)> Leitura = new(LerFilme);

    /// <summary>A leitura completa de 18/10: os 84 valores do Marco 1, na ordem do catálogo.</summary>
    public static IReadOnlyList<ValorCopiado> LeituraFilme => Leitura.Value.Valores;

    /// <summary>Quando a leitura de 18/10 foi feita.</summary>
    public static DateTimeOffset QuandoFilme => Leitura.Value.Quando;

    /// <summary>
    /// Lê a leitura de 18/10 com as respostas cruas: para cada consulta do Marco 1, a resposta que casa
    /// (<see cref="LeitorCompleto.Casar"/>, o mesmo casamento do leitor do app).
    /// </summary>
    private static (DateTimeOffset, IReadOnlyList<ValorCopiado>) LerFilme()
    {
        using var fluxo = typeof(CasaDoDenis).Assembly.GetManifestResourceStream(RecursoFilme)
                          ?? throw new InvalidOperationException($"Recurso {RecursoFilme} ausente.");
        using var doc = JsonDocument.Parse(fluxo);
        var crus = doc.RootElement.GetProperty("leituras").EnumerateArray()
            .Select(l => l.GetProperty("cru").GetString()!)
            .ToArray();

        var quando = doc.RootElement.TryGetProperty("inicio", out var inicio)
                     && DateTimeOffset.TryParse(inicio.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var d)
            ? d
            : DateTimeOffset.MinValue;

        var valores = new List<ValorCopiado>();
        foreach (var consulta in Catalogo.ConsultasMarco1)
        {
            var noFio = crus.Select(c => LeitorCompleto.Casar(consulta, c)).FirstOrDefault(n => n is not null)
                        ?? throw new InvalidOperationException($"A leitura de 18/10 não tem {consulta.Parametro.Codigo} {consulta.Onde}.");
            if (consulta.Parametro.Decodificar(noFio).Valor is null)
                throw new InvalidOperationException($"A leitura de 18/10 tem {consulta.Parametro.Codigo} {consulta.Onde} ilegível ({noFio}).");
            valores.Add(new ValorCopiado(consulta.Parametro.Codigo, consulta.Indice, noFio));
        }
        return (quando, valores);
    }
}
