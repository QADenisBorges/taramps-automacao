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
/// aparelho, a leitura da FILME de 18/10 (o preset herdado FILME, até a história 8d, e a mesma
/// leitura dividida em três camadas: o Setup "Casa 2.1", a memória FILME e as 15 bandas do
/// "Cinema") e as seções de texto de <c>conhecimento-herdado.md</c>. Está sempre presente: não
/// depende de arquivo do usuário nem de digitar nada.
/// </summary>
public static class ConhecimentoHerdado
{
    /// <summary>O id fixo do preset herdado FILME.</summary>
    public const string IdFilme = "herdado-filme-2026-10-18";

    public const string NomeFilme = "FILME";

    /// <summary>O recurso embutido: as respostas cruas da leitura completa de 18/10/2026.</summary>
    internal const string RecursoFilme = "leitura-2026-10-18_2145.json";

    /// <summary>O id fixo do Setup herdado "Casa 2.1".</summary>
    public const string IdSetupCasa = "herdado-setup-casa-2-1";

    public const string NomeSetupCasa = "Casa 2.1";

    /// <summary>O id fixo da memória do celular herdada FILME (no Setup "Casa 2.1").</summary>
    public const string IdMemoriaFilme = "herdado-memoria-filme";

    public const string NomeMemoriaFilme = "FILME";

    /// <summary>O nome do preset de 15 bandas herdado (a 8d o cria a partir de <see cref="BandasCinema"/>).</summary>
    public const string NomeCinema = "Cinema";

    /// <summary>O equilíbrio do Casa 2.1: a JBL R (saída 2) 3 dB abaixo no ganho e no limiter.</summary>
    public static Equilibrio EquilibrioCasa { get; } = new(0m, 0m, -3.0m, -3.0m);

    public const string NotaSetupCasa =
        "A calibração da FILME de 18/10: atrasos L 0, R 100 cm, sub 80 cm; fase do sub 180°; JBL com corte LR24 em 150 Hz; "
        + "sub com passa-alta BT24 em 40 Hz e passa-baixa LR24 em 150 Hz; rotas 1 = B e 2 = A; equilíbrio: R −3 dB no ganho e no limiter.";

    public const string NotaMemoriaFilme =
        "O caráter da FILME de 18/10: ganhos L +10, R +10 (no aparelho, +7 com o equilíbrio), sub +6; limiter JBL −15, sub −12; "
        + "ataque 50 ms, liberação 200 ms; EQ de saída JBL 2 kHz +2,5 (Q 0,7) e sub 45 Hz +3 (Q 0,7).";

    public const string NotaCinema =
        "As 15 bandas da FILME de 18/10: 160 Hz +1, 400 Hz −1, 4 kHz −1, 6,3 kHz −2, 10 kHz +2, 16 kHz +2; o resto 0.";

    private static readonly Lazy<Preset> Filme = new(LerFilme);
    private static readonly Lazy<Setup> Casa = new(() => new Setup(
        IdSetupCasa, NomeSetupCasa, PresetFilme.Quando, Da(Camada.Calibracao), EquilibrioCasa,
        Link12: false, Link34: false, Herdado: true, Nota: NotaSetupCasa));
    private static readonly Lazy<MemoriaCelular> MemoriaFilmeHerdada = new(() => new MemoriaCelular(
        IdMemoriaFilme, IdSetupCasa, NomeMemoriaFilme, PresetFilme.Quando, Camadas.Separar(Da(Camada.Carater), EquilibrioCasa),
        Herdado: true, Nota: NotaMemoriaFilme));
    private static readonly Lazy<IReadOnlyList<ValorCopiado>> Cinema = new(() => Da(Camada.Bandas));

    private static readonly IReadOnlyList<NotaMemoriaHerdada> Notas =
    [
        new(1, "MUSICA HIGH",
            "Aprovada. JBL +2, sub −1,4, EQ plano, ataque 10 ms, limitadores −11 (JBL) e −9 (sub)."),
        new(2, "MUSICA ORIG",
            "Versão antiga, precisa ser refeita: ganhos −5/−10, limitadores −11/−6. É o teste 1 do sinal de sucesso."),
        new(3, "FILME",
            "A versão definitiva de 18/10. Atrasos: L 0, R 100 cm, sub 80 cm. Ganhos: L +10, R +7, sub +6. "
            + "Limitadores −15/−18/−12, liberação de 200 ms. O preset herdado FILME tem esses valores."),
    ];

    /// <summary>
    /// A nota herdada da memória pela posição (1 a 3), ou null fora disso. A nota diz o nome do
    /// protótipo; o aparelho pode ter outro nome hoje, e o selo vale pela posição.
    /// </summary>
    public static NotaMemoriaHerdada? NotaMemoria(int numero) =>
        numero is >= 1 and <= 3 ? Notas[numero - 1] : null;

    /// <summary>O preset herdado FILME: os 84 valores do Marco 1 da leitura de 18/10.</summary>
    public static Preset PresetFilme => Filme.Value;

    /// <summary>Os presets herdados, que vêm sempre primeiro na lista e não mudam.</summary>
    public static IReadOnlyList<Preset> Presets => [PresetFilme];

    /// <summary>O Setup herdado "Casa 2.1": a calibração da leitura de 18/10, o equilíbrio R −3 e o link desligado.</summary>
    public static Setup SetupCasa => Casa.Value;

    /// <summary>A memória do celular herdada FILME: o caráter da leitura de 18/10 sem o equilíbrio (R +10 e −15).</summary>
    public static MemoriaCelular MemoriaFilme => MemoriaFilmeHerdada.Value;

    /// <summary>As 15 bandas e o preset de EQ do aparelho da leitura de 18/10: o "Cinema" (para a história 8d).</summary>
    public static IReadOnlyList<ValorCopiado> BandasCinema => Cinema.Value;

    /// <summary>Os valores da leitura de 18/10 na camada, na ordem da leitura.</summary>
    private static IReadOnlyList<ValorCopiado> Da(Camada camada)
    {
        var codigos = Camadas.Consultas(camada).Select(c => (c.Parametro.Codigo, c.Indice)).ToHashSet();
        return PresetFilme.Valores.Where(v => codigos.Contains((v.Codigo, v.Indice))).ToArray();
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
            "Memória 1, MUSICA HIGH: JBL +2, sub −1,4, EQ plano, ataque 10 ms, limitadores −11 (JBL) e −9 (sub). Aprovada.",
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
            "Casa 2.1: atrasos L 0, R 100 cm, sub 80 cm; fase do sub 180°; JBL com corte LR24 em 150 Hz; sub com passa-alta BT24 em 40 Hz e passa-baixa LR24 em 150 Hz; rotas 1 = B e 2 = A; equilíbrio: R −3 dB no ganho e no limiter.",
            "FILME: ganhos L +10, sub +6; limiter JBL −15, sub −12; ataque 50 ms, liberação 200 ms; EQ de saída JBL 2 kHz +2,5 (Q 0,7) e sub 45 Hz +3 (Q 0,7).",
            "Cinema: 160 Hz +1, 400 Hz −1, 4 kHz −1, 6,3 kHz −2, 10 kHz +2, 16 kHz +2; o resto 0.",
            "Ganho e limiter finais = valor da memória + equilíbrio do Setup: a R fica com ganho +7 e limiter −18, como na leitura.",
        ]),
        new("Presets",
        [
            "O preset FILME vem da leitura de 18/10 e se aplica como qualquer outro.",
            "Os presets DIALOGO e NOITE, que estão no celular, são de antes do alinhamento e estão desatualizados. Eles não entram no app: são arquivos .trps do app da Taramps, cifrados, e não são lidos.",
            "Um preset antigo entra carregando-o pelo app da Taramps e salvando a leitura como preset próprio.",
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
    /// Monta o preset FILME com as respostas cruas da leitura de 18/10: para cada consulta do
    /// Marco 1, a resposta que casa, decodificada pelo catálogo.
    /// </summary>
    private static Preset LerFilme()
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
        return new Preset(IdFilme, NomeFilme, quando, valores, Herdado: true, Nota: Notas[2].Texto);
    }
}
