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
/// O conhecimento que os protótipos deixaram (CAP-6), embutido no app: as notas das 3 memórias do
/// aparelho, a leitura da FILME de 18/10 dividida em três camadas (o Setup "Casa 2.1", a memória
/// FILME e o preset "Cinema", com as 15 bandas) e as seções de texto de <c>conhecimento-herdado.md</c>.
/// Está sempre presente: não depende de arquivo do usuário nem de digitar nada.
/// </summary>
public static class ConhecimentoHerdado
{
    /// <summary>O recurso embutido: as respostas cruas da leitura completa de 18/10/2026.</summary>
    internal const string RecursoFilme = "leitura-2026-10-18_2145.json";

    /// <summary>O id fixo do Setup herdado "Casa 2.1".</summary>
    public const string IdSetupCasa = "herdado-setup-casa-2-1";

    public const string NomeSetupCasa = "Casa 2.1";

    /// <summary>O id fixo da memória do celular herdada FILME (no Setup "Casa 2.1").</summary>
    public const string IdMemoriaFilme = "herdado-memoria-filme";

    public const string NomeMemoriaFilme = "FILME";

    /// <summary>O id fixo do preset herdado "Cinema" (as 15 bandas da leitura de 18/10).</summary>
    public const string IdCinema = "herdado-cinema-2026-10-18";

    /// <summary>O nome do preset de 15 bandas herdado.</summary>
    public const string NomeCinema = "Cinema";

    /// <summary>O equilíbrio do Casa 2.1, por canal: a JBL R (CH2) 3 dB abaixo no ganho e no limiter; os outros 0.</summary>
    public static Equilibrio EquilibrioCasa { get; } = new(0m, 0m, -3.0m, -3.0m, 0m, 0m, 0m, 0m);

    /// <summary>Os ids fixos dos equipamentos do Casa 2.1 (conhecimento-herdado.md, CAP-16).</summary>
    public const string IdModuloCasa = "herdado-modulo";

    public const string IdJblL = "herdado-jbl-l";
    public const string IdJblR = "herdado-jbl-r";
    public const string IdSubCasa = "herdado-sub";

    public const string NomeModuloCasa = "Módulo genérico";
    public const string ModeloJbl = "JBL 4TRMS80";

    public const string NotaSetupCasa =
        "A calibração da FILME de 18/10: atrasos L 0, R 100 cm, sub 80 cm; fase do sub 180°; JBL com corte LR24 em 150 Hz; "
        + "sub com passa-alta BT24 em 40 Hz e passa-baixa LR24 em 150 Hz; rotas 1 = B e 2 = A; Link 3–4 ligado; "
        + "equilíbrio: R (CH2) −3 dB no ganho e no limiter. Equipamentos: Módulo genérico (×19,6) com a JBL L no CH1, "
        + "a JBL R no CH2 e o Sub (subwoofer genérico) no bridge 3+4, alimentado pelo CH3 e pelo CH4.";

    public const string NotaJbl = "JBL 4TRMS80, 3,6 Ω, com passa-alta mínimo de 100 Hz a 24 dB/oit (ficha do fabricante).";
    public const string NotaModuloCasa = "×19,6 nos canais das JBL e ×40 no bridge do sub. O modelo não se sabe.";
    public const string NotaSubCasa = "Subwoofer genérico: o modelo não se sabe.";

    /// <summary>
    /// Os equipamentos do Casa 2.1 (decisão do Denis de 23/10/2026: o que não se sabe fica genérico):
    /// o "Módulo genérico" (grupo 2, 4 canais alimentados pelo CH1 a CH4, bridge 3+4, ×19,6), a JBL L
    /// no canal 1 dele, a JBL R no canal 2 e o Sub (subwoofer genérico) no bridge 3+4.
    /// </summary>
    public static IReadOnlyList<Equipamento> EquipamentosCasa { get; } =
    [
        new Equipamento(IdModuloCasa, GrupoEquipamento.Amplificador, "módulo", NomeModuloCasa)
        {
            Dados = new DadosTecnicos(Ganho: 19.6m, Canais: 4),
            Entradas = Enumerable.Range(1, 4).Select(n => new Entrada(n, FonteSinal.DoCanal(n - 1))).ToArray(),
            Bridges = [new Bridge(3, 4)],
            Nota = NotaModuloCasa,
        },
        new Equipamento(IdJblL, GrupoEquipamento.AltoFalante, "médio-grave", "JBL L", ModeloJbl)
        {
            Dados = new DadosTecnicos(Impedancia: 3.6m),
            Ligado = LigacaoAltoFalante.NoCanal(IdModuloCasa, 1),
            Nota = NotaJbl,
        },
        new Equipamento(IdJblR, GrupoEquipamento.AltoFalante, "médio-grave", "JBL R", ModeloJbl)
        {
            Dados = new DadosTecnicos(Impedancia: 3.6m),
            Ligado = LigacaoAltoFalante.NoCanal(IdModuloCasa, 2),
            Nota = NotaJbl,
        },
        new Equipamento(IdSubCasa, GrupoEquipamento.AltoFalante, "subwoofer", "Sub")
        {
            Ligado = LigacaoAltoFalante.NoBridge(IdModuloCasa, new Bridge(3, 4)),
            Nota = NotaSubCasa,
        },
    ];

    public const string NotaMemoriaFilme =
        "O caráter da FILME de 18/10: ganhos L +10, R +10 (no aparelho, +7 com o equilíbrio), sub +6; limiter JBL −15, sub −12; "
        + "ataque 50 ms, liberação 200 ms; EQ de saída JBL 2 kHz +2,5 (Q 0,7) e sub 45 Hz +3 (Q 0,7).";

    public const string NotaCinema =
        "As 15 bandas da FILME de 18/10: 160 Hz +1, 400 Hz −1, 4 kHz −1, 6,3 kHz −2, 10 kHz +2, 16 kHz +2; o resto 0.";

    private static readonly Lazy<(DateTimeOffset Quando, IReadOnlyList<ValorCopiado> Valores)> Leitura = new(LerFilme);
    private static readonly Lazy<Setup> Casa = new(() => new Setup(
        IdSetupCasa, NomeSetupCasa, QuandoFilme, Da(Camadas.Consultas(Camada.Calibracao)), EquilibrioCasa,
        Link12: false, Link34: true, Herdado: true, Nota: NotaSetupCasa) { Equipamentos = EquipamentosCasa });
    private static readonly Lazy<MemoriaCelular> MemoriaFilmeHerdada = new(() => new MemoriaCelular(
        IdMemoriaFilme, IdSetupCasa, NomeMemoriaFilme, QuandoFilme,
        Camadas.Separar(Da(Camadas.Consultas(Camada.Carater)), EquilibrioCasa),
        Herdado: true, Nota: NotaMemoriaFilme, IdPreset: IdCinema));
    private static readonly Lazy<Preset> Cinema = new(() => new Preset(
        IdCinema, NomeCinema, QuandoFilme, Da(GerentePresets.ConsultasBandas), Herdado: true, Nota: NotaCinema));

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
            + "Limitadores −15/−18/−12, liberação de 200 ms. No app, ela é o Setup Casa 2.1, a memória FILME e o preset Cinema."),
    ];

    /// <summary>
    /// A nota herdada da memória pela posição (1 a 3), ou null fora disso. A nota diz o nome do
    /// protótipo; o aparelho pode ter outro nome hoje, e o selo vale pela posição.
    /// </summary>
    public static NotaMemoriaHerdada? NotaMemoria(int numero) =>
        numero is >= 1 and <= 3 ? Notas[numero - 1] : null;

    /// <summary>
    /// A leitura completa de 18/10: os 84 valores do Marco 1, na ordem do catálogo. É a base do
    /// Setup "Casa 2.1", da memória FILME e do preset "Cinema"; não é um preset.
    /// </summary>
    public static IReadOnlyList<ValorCopiado> LeituraFilme => Leitura.Value.Valores;

    /// <summary>Quando a leitura de 18/10 foi feita.</summary>
    public static DateTimeOffset QuandoFilme => Leitura.Value.Quando;

    /// <summary>O preset herdado "Cinema": as 15 bandas (<c>020</c>) da leitura de 18/10.</summary>
    public static Preset PresetCinema => Cinema.Value;

    /// <summary>Os presets herdados, que vêm sempre primeiro na lista e não mudam.</summary>
    public static IReadOnlyList<Preset> Presets => [PresetCinema];

    /// <summary>O Setup herdado "Casa 2.1": a calibração da leitura de 18/10, o equilíbrio R −3, o Link 3–4 ligado e os equipamentos.</summary>
    public static Setup SetupCasa => Casa.Value;

    /// <summary>A memória do celular herdada FILME: o caráter da leitura de 18/10 sem o equilíbrio (R +10 e −15); lembra o Cinema.</summary>
    public static MemoriaCelular MemoriaFilme => MemoriaFilmeHerdada.Value;

    /// <summary>As 15 bandas da leitura de 18/10: as do preset "Cinema".</summary>
    public static IReadOnlyList<ValorCopiado> BandasCinema => PresetCinema.Valores;

    /// <summary>Os valores da leitura de 18/10 nas consultas dadas, na ordem da leitura.</summary>
    private static IReadOnlyList<ValorCopiado> Da(IEnumerable<Consulta> consultas)
    {
        var codigos = consultas.Select(c => (c.Parametro.Codigo, c.Indice)).ToHashSet();
        return LeituraFilme.Where(v => codigos.Contains((v.Codigo, v.Indice))).ToArray();
    }

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
            "JBL 4TRMS80, 3,6 Ω, com passa-alta mínimo de 100 Hz a 24 dB/oit (ficha do fabricante).",
            "Módulo: ×19,6 nas JBL e ×40 no sub em ponte. Limitador das JBL em −11 dB = 11,7 V; do sub em −9 dB = 28,6 V (em −6 dB passava de 40 V).",
            "Fonte principal: o Bluetooth do celular, que entra cerca de 13 dB abaixo do gerador. Com o BT a 100 % e ganho +2, a saída dá 11,4 V. O gerador só pode ser usado em −13 dB, ou em 0 dB sem os alto-falantes.",
            "Sala: L a 3,20 m, sub a 2,40 m, R a 2,20 m, com 1,60 m entre as JBL. O ouvinte fica 0,9 m à direita da R.",
            "Os limites do guardião (estado/guardiao.json) são provisórios: não são regra nem referência, e quem decide os valores é o Denis.",
        ]),
        new("As 3 memórias",
        [
            "Memória 1, SPL (salva em 20/10): o máximo seguro de cada falante pelo BT a 100 %. JBL +6 com passa-alta LR24 em 200 Hz e limitador −7 (ataque 5 ms); sub +2,6 com passa-baixa LR24 em 200 Hz e limitador −9 (ataque 10 ms); EQ plano. Medido: 17,7 V nas JBL e 27,5 V no sub.",
            "Memória 2, MUSICA ORIG: ainda é a versão antiga (ganhos −5/−10, limitadores −11/−6). Precisa ser refeita, e esse é o teste 1 do sinal de sucesso.",
            "Memória 3, FILME: a versão definitiva, retratada na leitura de 18/10 (2026-10-18_2145).",
            "FILME, atrasos: L 0, R 100 cm, sub 80 cm. Ganhos: L +10, R +7, sub +6. Limitadores: −15 / −18 / −12, com liberação de 200 ms.",
            "FILME, EQ de saída: JBL 2 kHz +2,5 (Q 0,7); sub 45 Hz +3 (Q 0,7).",
            "FILME, EQ global: 160 Hz +1, 400 Hz −1, 4 kHz −1, 6,3 kHz −2, 10 kHz +2, 16 kHz +2.",
            "FILME, sub: fase 180°, passa-alta 40 Hz BT24, passa-baixa 150 Hz LR24. JBL: corte LR24 em 150 Hz.",
            "O nome que o aparelho tem hoje pode ser outro; o selo vale pela posição da memória.",
        ]),
        new("A FILME em três camadas",
        [
            "A leitura de 18/10 entra no app dividida em três: o Setup \"Casa 2.1\" (a calibração), a memória FILME (o caráter) e o preset \"Cinema\" (as 15 bandas).",
            "Casa 2.1: atrasos L 0, R 100 cm, sub 80 cm; fase do sub 180°; JBL com corte LR24 em 150 Hz; sub com passa-alta BT24 em 40 Hz e passa-baixa LR24 em 150 Hz; rotas 1 = B e 2 = A; Link 3–4 ligado; equilíbrio: R (CH2) −3 dB no ganho e no limiter.",
            "FILME: ganhos L +10, sub +6; limiter JBL −15, sub −12; ataque 50 ms, liberação 200 ms; EQ de saída JBL 2 kHz +2,5 (Q 0,7) e sub 45 Hz +3 (Q 0,7).",
            "Cinema: 160 Hz +1, 400 Hz −1, 4 kHz −1, 6,3 kHz −2, 10 kHz +2, 16 kHz +2; o resto 0.",
            "Ganho e limiter finais = valor da memória + equilíbrio do Setup: a R fica com ganho +7 e limiter −18, como na leitura.",
        ]),
        new("Os equipamentos do Casa 2.1",
        [
            "Decisão do Denis de 23/10/2026: o que não se sabe fica genérico.",
            "Módulo genérico (amplificador, grupo 2): os 4 canais alimentados pelo CH1, CH2, CH3 e CH4; ×19,6 nos canais das JBL e ×40 no bridge 3+4 do sub.",
            "JBL L e JBL R: JBL 4TRMS80, 3,6 Ω, passa-alta mínimo de 100 Hz a 24 dB/oit; a L no canal 1 do módulo (CH1), a R no canal 2 (CH2).",
            "Sub (subwoofer genérico): no bridge 3+4 do módulo, alimentado pelo CH3 e pelo CH4.",
            "O Link 3–4 fica ligado: o CH3 e o CH4 alimentam o mesmo bridge e precisam ficar iguais. Na tela, o Sub é uma saída só.",
        ]),
        new("Presets",
        [
            "O preset é só o EQ de 15 bandas. O herdado é o Cinema, as 15 bandas da FILME de 18/10: ele se aplica como qualquer outro e não se renomeia, não se apaga nem se sobrescreve.",
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
