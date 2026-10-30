using System.Globalization;
using System.Text.Json;
using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Leitura;
using BgsControle.Nucleo.Presets;
using BgsControle.Nucleo.Setups;

namespace BgsControle.Nucleo.Herdado;

/// <summary>A nota herdada de uma das 3 memórias, pela posição (1 a 3).</summary>
/// <param name="Numero">A memória na tela (1 a 3).</param>
/// <param name="NomeNoPrototipo">O nome que o protótipo usava; o aparelho pode ter outro hoje.</param>
/// <param name="Texto">A nota.</param>
public sealed record NotaMemoriaHerdada(int Numero, string NomeNoPrototipo, string Texto);

/// <summary>Uma seção de texto do conhecimento herdado.</summary>
public sealed record SecaoHerdada(string Titulo, IReadOnlyList<string> Linhas);

/// <summary>
/// O conhecimento que os protótipos deixaram (CAP-6), embutido no app: o Setup "Casa 2.1" (os
/// equipamentos e as fichas com a origem de cada dado), os 3 modelos da base de modelos, as notas das
/// 3 memórias do aparelho e as seções de texto de <c>conhecimento-herdado.md</c>. O app nasce sem
/// memórias e sem presets do celular (decisão do Denis, 29/10/2026). A leitura de 18/10
/// (<see cref="LeituraFilme"/>) fica embutida para o simulador e para a paridade; ela não vira
/// memória nem preset. Está sempre presente: não depende de arquivo do usuário nem de digitar nada.
/// </summary>
public static class ConhecimentoHerdado
{
    /// <summary>O recurso embutido: as respostas cruas da leitura completa de 18/10/2026.</summary>
    internal const string RecursoFilme = "leitura-2026-10-18_2145.json";

    /// <summary>O id fixo do Setup herdado "Casa 2.1".</summary>
    public const string IdSetupCasa = "herdado-setup-casa-2-1";

    public const string NomeSetupCasa = "Casa 2.1";

    /// <summary>Os ids fixos dos equipamentos do Casa 2.1 (conhecimento-herdado.md, CAP-16).</summary>
    public const string IdModuloCasa = "herdado-modulo";

    public const string IdJblL = "herdado-jbl-l";
    public const string IdJblR = "herdado-jbl-r";
    public const string IdSubCasa = "herdado-sub";

    public const string NomeModuloCasa = "Módulo SD800.4 EVO";

    /// <summary>O nome do módulo herdado até 29/10/2026 (um <c>setups.json</c> antigo ainda o tem).</summary>
    public const string NomeModuloAntigo = "Módulo genérico";

    public const string ModeloJbl = "JBL 4TRMS80";
    public const string ModeloSub = "Protech Light 8\"";
    public const string ModeloModulo = "SD800.4 EVO";

    /// <summary>Quando o Setup herdado foi refeito só com o físico (decisão do Denis).</summary>
    public static DateTimeOffset QuandoCasa { get; } = new(2026, 10, 29, 0, 0, 0, TimeSpan.FromHours(-3));

    public const string NotaSetupCasa =
        "O 2.1 de casa: Módulo SD800.4 EVO com a JBL L no canal 1 (CH1), a JBL R no canal 2 (CH2) e o Sub "
        + "(Protech Light 8\") no bridge 3+4, alimentado pelo CH3 e pelo CH4. O Link 3–4 sai sozinho do bridge.";

    public const string NotaJbl = "Passa-alta mínimo de 100 Hz a 24 dB/oit (ficha do fabricante). O Bl não fecha com o Qes.";
    public const string NotaModuloCasa = "Ponte em 2 Ω proibida (manual). Alimentado pela BOB 60A (40 A contínuos).";
    public const string NotaSubCasa = "Qts 0,52 (site do fabricante).";

    /// <summary>As notas dos equipamentos herdados de antes da 8f: na migração, viram as notas novas.</summary>
    public static IReadOnlySet<string> NotasAntigas { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "JBL 4TRMS80, 3,6 Ω, com passa-alta mínimo de 100 Hz a 24 dB/oit (ficha do fabricante).",
        "×19,6 nos canais das JBL e ×40 no bridge do sub. O modelo não se sabe.",
        "Subwoofer genérico: o modelo não se sabe.",
    };

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
    /// Os equipamentos do Casa 2.1 (decisões do Denis de 23/10 e de 29/10/2026: o Setup é só o físico, e
    /// cada dado da ficha tem a origem): o Módulo SD800.4 EVO (grupo 2, 4 canais alimentados pelo CH1 a
    /// CH4, bridge 3+4), a JBL L no canal 1 dele, a JBL R no canal 2 e o Sub (Protech Light 8") no
    /// bridge 3+4. O que foi medido na instalação (a Re, o ganho, a caixa) vale sobre o oficial.
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

    /// <summary>
    /// Os modelos que a base de modelos traz de nascença (só os dados oficiais): JBL 4TRMS80, Protech
    /// Light 8" e SD800.4 EVO.
    /// </summary>
    public static IReadOnlyList<ModeloGuardado> Modelos { get; } =
    [
        new(ModeloJbl, GrupoEquipamento.AltoFalante, "médio-grave", FichaJbl, QuandoCasa),
        new(ModeloSub, GrupoEquipamento.AltoFalante, "subwoofer", FichaSub, QuandoCasa),
        new(ModeloModulo, GrupoEquipamento.Amplificador, "módulo", FichaModulo, QuandoCasa),
    ];

    private static readonly Lazy<(DateTimeOffset Quando, IReadOnlyList<ValorCopiado> Valores)> Leitura = new(LerFilme);
    private static readonly Lazy<Setup> Casa = new(() => new Setup(
        IdSetupCasa, NomeSetupCasa, QuandoCasa, Herdado: true, Nota: NotaSetupCasa) { Equipamentos = EquipamentosCasa });

    private static readonly IReadOnlyList<NotaMemoriaHerdada> Notas =
    [
        new(1, "SPL",
            "Salva em 20/10: o máximo seguro de cada falante pelo BT a 100 %. JBL +6 com passa-alta LR24 em 200 Hz e limitador −7 "
            + "(ataque 5 ms); sub +2,6 com passa-alta BT24 em 40 Hz, passa-baixa LR24 em 200 Hz e limitador −9 (ataque 10 ms); "
            + "liberação 200 ms, fase e atrasos 0, EQ plano. Medido: 17,7 V nas JBL e 27,5 V no sub."),
        new(2, "MUSICA ORIG",
            "Versão antiga, precisa ser refeita: ganhos −5/−10, limitadores −11/−6. É o teste 1 do sinal de sucesso."),
        new(3, "FILME",
            "A versão definitiva de 18/10. Atrasos: L 0, R 100 cm, sub 80 cm. Ganhos: L +10, R +7, sub +6. "
            + "Limitadores −15/−18/−12, liberação de 200 ms."),
    ];

    /// <summary>
    /// A nota herdada da memória pela posição (1 a 3), ou null fora disso. A nota diz o nome do
    /// protótipo; o aparelho pode ter outro nome hoje, e o selo vale pela posição.
    /// </summary>
    public static NotaMemoriaHerdada? NotaMemoria(int numero) =>
        numero is >= 1 and <= 3 ? Notas[numero - 1] : null;

    /// <summary>
    /// A leitura completa de 18/10: os 84 valores do Marco 1, na ordem do catálogo. Fica para o
    /// simulador e a paridade; não é memória nem preset.
    /// </summary>
    public static IReadOnlyList<ValorCopiado> LeituraFilme => Leitura.Value.Valores;

    /// <summary>Quando a leitura de 18/10 foi feita.</summary>
    public static DateTimeOffset QuandoFilme => Leitura.Value.Quando;

    /// <summary>Os presets herdados: nenhum (decisão do Denis, 29/10/2026).</summary>
    public static IReadOnlyList<Preset> Presets { get; } = [];

    /// <summary>O Setup herdado "Casa 2.1": os equipamentos e as fichas, sem memórias.</summary>
    public static Setup SetupCasa => Casa.Value;

    /// <summary>O equipamento herdado do Casa 2.1 pelo id, ou null.</summary>
    public static Equipamento? EquipamentoCasa(string? id) => EquipamentosCasa.FirstOrDefault(e => e.Id == id);

    /// <summary>As seções de texto, em português, na ordem do arquivo de conhecimento herdado.</summary>
    public static IReadOnlyList<SecaoHerdada> Secoes { get; } =
    [
        new("Protocolo",
        [
            "O catálogo, o formato dos quadros (*cod*canal*valor* e #cod#canal#), a senha, os comandos de aparelho inteiro, o link de canais e o limiter em dB estão em protocolo/MAPA.md; as conversões, em protocolo/APK.md.",
            "As lacunas do MAPA §8 continuam abertas. A primeira leitura real deu 84 de 84 em cerca de 13 s, com o firmware R003 (MAPA §9).",
            "A resposta da senha traz o firmware nos 4 últimos caracteres. ERRO quer dizer senha errada.",
        ]),
        new("O sistema do Denis",
        [
            "Medido em 18/10/2026. É um 2.1: a saída 1 é a JBL L (rota B), a saída 2 é a JBL R (rota A) e as saídas 3 e 4 são o sub em ponte. O L do Bluetooth chega pela entrada B.",
            "JBL 4TRMS80: 4 Ω nominais (ficha do fabricante) e Re de 3,6 Ω medida, com passa-alta mínimo de 100 Hz a 24 dB/oit (ficha do fabricante).",
            "Módulo: ganho medido de ×19,5 por canal (as JBL) e ×40,5 em ponte (o sub). O teto técnico vem do recálculo de 20/10: JBL 17,9 V e sub 28,6 V. A potência do sub foi confirmada em 27/10 (Protech Light 8\", 450 W).",
            "Fonte principal: o Bluetooth do celular, que entra cerca de 13 dB abaixo do gerador. Com o BT a 100 % e ganho +2, a saída dá 11,4 V. O gerador só pode ser usado em −13 dB, ou em 0 dB sem os alto-falantes.",
            "Sala: L a 3,20 m, sub a 2,40 m, R a 2,20 m, com 1,60 m entre as JBL. O ouvinte fica 0,9 m à direita da R.",
            "Os limites do guardião (estado/guardiao.json) são provisórios: não são regra nem referência, e quem decide os valores é o Denis.",
        ]),
        new("Os equipamentos do Casa 2.1",
        [
            "Decisões do Denis de 23/10 e de 29/10/2026: o Setup é só o físico; cada dado da ficha tem a origem; o que não se sabe fica em branco.",
            "Módulo SD800.4 EVO (amplificador, grupo 2): os 4 canais alimentados pelo CH1, CH2, CH3 e CH4, bridge 3+4. Oficial (manual): 132 W por canal em 4 Ω (ceifa ~23 V), 400 W em ponte em 4 Ω (ceifa ~40 V), ponte em 2 Ω proibida. Medido: ganho ×19,5 por canal e ×40,5 em ponte; fonte 13,89 V sem carga.",
            "JBL L e JBL R: JBL 4TRMS80. Oficial (ficha impressa): 80 W RMS, 4 Ω, Re 3,4 Ω, Fs 98 Hz, Qes 0,81, Qms 3,19, Vas 2,06 L, Sd 64 cm², Bl 4,1, Xmax 1,75 mm, 87 dB. Medido no Casa 2.1: Re 3,6 Ω; caixa selada de ~3,5 L (estimada). A L no canal 1 do módulo (CH1), a R no canal 2 (CH2).",
            "Sub: Protech Light 8\". Oficial (site do fabricante, 27/10): 450 W RMS, 4 Ω, Re 3,7 Ω, Fs 46 Hz, Qms 3,90, Qes 0,61, Qts 0,52, Vas 10 L, Bl 13, Xmax 8 mm, Le 1,63 mH, Sd 240 cm², 84 dB. Medido: caixa dutada de ~28 L (estimada), Fb 51 Hz, duto de 38,5 cm². No bridge 3+4 do módulo, alimentado pelo CH3 e pelo CH4.",
            "O Link 3–4 sai sozinho: o CH3 e o CH4 alimentam o mesmo bridge. Na tela, o Sub é uma saída só.",
            "As três fichas oficiais entram também na base de modelos do celular.",
        ]),
        new("As 3 memórias do aparelho",
        [
            "As memórias do processador não aparecem no app (decisão do Denis, 29/10/2026): o app não lê os nomes nem carrega; quem salva, renomeia e carrega é o app da Taramps. Elas são intocáveis e não são referência para as memórias novas do celular.",
            "Memória 1, SPL (salva em 20/10): o máximo seguro de cada falante pelo BT a 100 %. JBL +6 com passa-alta LR24 em 200 Hz e limitador −7 (ataque 5 ms); sub +2,6 com passa-baixa LR24 em 200 Hz e limitador −9 (ataque 10 ms); EQ plano. Medido: 17,7 V nas JBL e 27,5 V no sub.",
            "Memória 2, MUSICA ORIG: ainda é a versão antiga (ganhos −5/−10, limitadores −11/−6). Precisa ser refeita, e esse é o teste 1 do sinal de sucesso.",
            "Memória 3: a versão definitiva de 18/10 (leitura 2026-10-18_2145). Atrasos L 0, R 100 cm, sub 80 cm; ganhos L +10, R +7, sub +6; limitadores −15 / −18 / −12, liberação de 200 ms.",
            "O nome que o aparelho tem hoje pode ser outro; o selo vale pela posição da memória.",
        ]),
        new("Memórias e presets do celular",
        [
            "O app nasce sem memórias e sem presets do celular (decisão do Denis, 29/10/2026): as calibrações de antes estavam mal feitas, e tudo começa do zero.",
            "Cada memória do celular guarda toda a regulagem, com o valor final, e lembra um preset de 15 bandas.",
            "Para comparar com o app da Taramps, os dois olham o estado atual do aparelho; este app não carrega memória do aparelho.",
        ]),
        new("Presets",
        [
            "O preset é só o EQ de 15 bandas. Não há preset herdado.",
            "Os presets DIALOGO e NOITE, que estão no celular, são de antes do alinhamento e estão desatualizados. Eles não entram no app: são arquivos .trps do app da Taramps, cifrados, e não são lidos.",
            "Um preset antigo entra carregando-o pelo app da Taramps e salvando a leitura como memória do celular.",
        ]),
        new("Preferências e método",
        [
            "O Denis avalia sempre as 4 dimensões juntas (voz, grave, detalhes, envolvimento) e dá notas.",
            "Antes de propor EQ, confere posição, tempo, fase e LFE.",
            "As instruções vêm curtas, um passo por vez, cada uma com o que conferir e o que responder.",
            "Toda medida vem com a foto do multímetro VC830L marcada.",
            "A diretriz de 18/10 (\"não mexer no celular\") valia para o app da Taramps operado por adb. Com este app, o celular volta a ser a tela dele.",
        ]),
        new("Cuidados com o app da Taramps",
        [
            "Deslizar uma memória na tela Salvar carrega essa memória.",
            "Abrir a tela Limiter com o Auto ligado faz o app reescrever o attack e o release.",
            "Depois de usar o app da Taramps, é preciso reler o aparelho.",
        ]),
        new("Situação registrada",
        [
            "Uma regulagem feita sobre o nível do gerador do processador deixou o som pelo Bluetooth muito baixo, cerca de 13 dB abaixo. O Denis passou a usar só o BT como fonte, e a equalização melhorou.",
            "Os sons de teste pelo celular ficam para uma versão futura. Marcar em cada preset a fonte para a qual ele foi regulado fica para quando houver uma segunda fonte (TV box).",
        ]),
    ];

    /// <summary>
    /// Lê a leitura de 18/10 com as respostas cruas: para cada consulta do Marco 1, a resposta que
    /// casa, decodificada pelo catálogo.
    /// </summary>
    private static (DateTimeOffset, IReadOnlyList<ValorCopiado>) LerFilme()
    {
        using var fluxo = typeof(ConhecimentoHerdado).Assembly.GetManifestResourceStream(RecursoFilme)
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
