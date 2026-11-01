using System.Globalization;
using System.Text;

namespace BgsControle.Nucleo.Setups;

/// <summary>
/// Os grupos do modelo de ligação (proposta de 23/10/2026, equipamentos, seção 2): os quatro com
/// entrada de linha e o alto-falante passivo.
/// </summary>
public enum GrupoEquipamento
{
    /// <summary>Grupo 1, amplificam e tocam: caixa ativa, sub ativo, monitor ativo, line array ativo, soundbar ou receiver.</summary>
    AmplificaETocam,

    /// <summary>Grupo 2, amplificam para alto-falantes passivos: módulo ou amplificador, com canais e bridges.</summary>
    Amplificador,

    /// <summary>Grupo 3, processam ou distribuem e repassam o que recebem: processador, crossover, equalizador…</summary>
    Repassa,

    /// <summary>Grupo 4, só recebem: interface de áudio, gravador, mesa.</summary>
    SoRecebe,

    /// <summary>Alto-falante passivo: fica num canal ou num bridge de um módulo (grupo 2).</summary>
    AltoFalante,
}

/// <summary>A função acústica de um equipamento. Sem escolha do usuário, vale a sugerida (<see cref="Ligacoes.SugerirFuncao"/>).</summary>
public enum FuncaoAcustica
{
    Sub,
    Grave,
    MedioGrave,
    Medio,
    Agudo,
    SuperAgudo,
    FullRange,
    Outra,
}

/// <summary>
/// De onde vem o sinal de uma entrada de linha: um canal do processador (<see cref="Canal"/>, 0 a 3 =
/// CH1 a CH4) ou a saída de um equipamento do grupo 3 (<see cref="IdEquipamento"/>), que repassa o
/// que recebe. Um canal pode alimentar quantas entradas quiser (cabo Y).
/// </summary>
public sealed record FonteSinal(int? Canal, string? IdEquipamento)
{
    public static FonteSinal DoCanal(int canal) => new(canal, null);

    public static FonteSinal DoEquipamento(string id) => new(null, id);
}

/// <summary>
/// Uma entrada de linha (grupos 1, 3 e 4) ou um canal do módulo (grupo 2), numerada a partir de 1,
/// com a fonte dela (null = sem ligação).
/// </summary>
public sealed record Entrada(int Numero, FonteSinal? Fonte);

/// <summary>Um bridge do módulo: dois canais vizinhos (1+2, 3+4…), numerados a partir de 1.</summary>
public sealed record Bridge(int A, int B)
{
    /// <summary>"3+4".</summary>
    public string Rotulo => $"{A}+{B}";
}

/// <summary>Onde um alto-falante passivo está ligado: um canal (<see cref="Canal"/>, a partir de 1) ou um bridge de um módulo.</summary>
public sealed record LigacaoAltoFalante(string IdModulo, int? Canal, Bridge? Bridge)
{
    public static LigacaoAltoFalante NoCanal(string idModulo, int canal) => new(idModulo, canal, null);

    public static LigacaoAltoFalante NoBridge(string idModulo, Bridge bridge) => new(idModulo, null, bridge);
}

/// <summary>De onde veio um dado da ficha (CAP-16; forja de 29/10/2026).</summary>
public enum OrigemDado
{
    /// <summary>O manual ou a ficha do fabricante (com o endereço, na <see cref="DadoTecnico.Fonte"/>).</summary>
    Oficial,

    /// <summary>Outra fonte (uma loja, um fórum, uma revisão), com o endereço ou a descrição.</summary>
    OutraFonte,

    /// <summary>Medido na instalação (a Re de 3,6 Ω da JBL do Casa 2.1).</summary>
    Medido,

    /// <summary>Estimado (o volume da caixa por foto e régua).</summary>
    Estimado,

    /// <summary>Digitado pelo usuário, sem fonte confirmada.</summary>
    Digitado,
}

/// <summary>
/// Um dado numérico da ficha, com a origem dele. Um dado sem origem confirmada nunca aparece como
/// verificado (<see cref="Verificado"/>).
/// </summary>
/// <param name="Valor">Maior que zero.</param>
/// <param name="Origem">De onde ele veio.</param>
/// <param name="Fonte">O endereço ou a descrição da fonte (até 120 caracteres), ou null.</param>
public sealed record DadoTecnico(decimal Valor, OrigemDado Origem, string? Fonte = null)
{
    /// <summary>Oficial ou medido: o dado conferido.</summary>
    public bool Verificado => Origem is OrigemDado.Oficial or OrigemDado.Medido;

    /// <summary>Um dado digitado pelo usuário (sem fonte), ou null sem valor.</summary>
    public static DadoTecnico? Digitado(decimal? valor) => valor is { } v ? new DadoTecnico(v, OrigemDado.Digitado) : null;
}

/// <summary>Um dado de texto da ficha (o tipo da caixa: "selada", "dutada"), com a origem dele.</summary>
public sealed record DadoTexto(string Valor, OrigemDado Origem, string? Fonte = null)
{
    /// <summary>Oficial ou medido: o dado conferido.</summary>
    public bool Verificado => Origem is OrigemDado.Oficial or OrigemDado.Medido;
}

/// <summary>Uma via de dentro de uma caixa ativa ("woofer de 12"", "driver"), com o diâmetro em polegadas, se se sabe.</summary>
public sealed record Via(string Descricao, decimal? Diametro);

/// <summary>
/// A ficha de um equipamento: os dados técnicos, todos opcionais e cada um com a origem (o que não se
/// sabe fica em branco). Os números são maiores que zero.
/// </summary>
public sealed record DadosTecnicos
{
    public static DadosTecnicos Nenhum { get; } = new();

    // ------------------------------------------------------------------ gerais

    /// <summary>Em polegadas.</summary>
    public DadoTecnico? Diametro { get; init; }

    /// <summary>A impedância nominal, em Ω.</summary>
    public DadoTecnico? Impedancia { get; init; }

    /// <summary>Em W.</summary>
    public DadoTecnico? PotenciaRms { get; init; }

    /// <summary>Em dB.</summary>
    public DadoTecnico? Sensibilidade { get; init; }

    /// <summary>O começo da faixa, em Hz.</summary>
    public DadoTecnico? FaixaMinima { get; init; }

    /// <summary>O fim da faixa, em Hz.</summary>
    public DadoTecnico? FaixaMaxima { get; init; }

    /// <summary>O ganho do amplificador por canal, em × (19,5 no módulo do Casa 2.1).</summary>
    public DadoTecnico? Ganho { get; init; }

    /// <summary>O número de canais (informativo, inteiro; num módulo, os canais são as <see cref="Equipamento.Entradas"/>).</summary>
    public DadoTecnico? Canais { get; init; }

    // ------------------------------------------------------------------ alto-falante

    /// <summary>A resistência da bobina, em Ω.</summary>
    public DadoTecnico? Re { get; init; }

    /// <summary>A frequência de ressonância, em Hz.</summary>
    public DadoTecnico? Fs { get; init; }

    public DadoTecnico? Qes { get; init; }

    public DadoTecnico? Qms { get; init; }

    /// <summary>Em L.</summary>
    public DadoTecnico? Vas { get; init; }

    /// <summary>Em T·m.</summary>
    public DadoTecnico? Bl { get; init; }

    /// <summary>Em cm².</summary>
    public DadoTecnico? Sd { get; init; }

    /// <summary>Em mm.</summary>
    public DadoTecnico? Xmax { get; init; }

    /// <summary>Em mH.</summary>
    public DadoTecnico? Le { get; init; }

    // ------------------------------------------------------------------ térmico do alto-falante (história 13f)

    /// <summary>A temperatura máxima da bobina, em °C.</summary>
    public DadoTecnico? TemperaturaMaximaBobina { get; init; }

    /// <summary>A resistência térmica da bobina ao ímã, em K/W.</summary>
    public DadoTecnico? RthBobinaIma { get; init; }

    /// <summary>A resistência térmica do ímã ao ar, em K/W.</summary>
    public DadoTecnico? RthImaAr { get; init; }

    /// <summary>A constante de tempo térmica da bobina, em s.</summary>
    public DadoTecnico? TauBobina { get; init; }

    /// <summary>A constante de tempo térmica do ímã, em s.</summary>
    public DadoTecnico? TauIma { get; init; }

    // ------------------------------------------------------------------ caixa

    /// <summary>"selada", "dutada", "radiador passivo"… (até 32 caracteres).</summary>
    public DadoTexto? TipoCaixa { get; init; }

    /// <summary>O volume interno, em L.</summary>
    public DadoTecnico? VolumeCaixa { get; init; }

    /// <summary>A sintonia do duto, em Hz.</summary>
    public DadoTecnico? Fb { get; init; }

    /// <summary>A área do duto, em cm².</summary>
    public DadoTecnico? AreaDuto { get; init; }

    // ------------------------------------------------------------------ módulo

    /// <summary>A potência por canal, em W.</summary>
    public DadoTecnico? PotenciaCanal { get; init; }

    /// <summary>A tensão de ceifamento por canal, em V.</summary>
    public DadoTecnico? TensaoCeifamentoCanal { get; init; }

    /// <summary>A potência em ponte, em W.</summary>
    public DadoTecnico? PotenciaPonte { get; init; }

    /// <summary>A tensão de ceifamento em ponte, em V.</summary>
    public DadoTecnico? TensaoCeifamentoPonte { get; init; }

    /// <summary>O ganho em ponte, em ×.</summary>
    public DadoTecnico? GanhoPonte { get; init; }

    // ------------------------------------------------------------------ alimentação

    /// <summary>A tensão da alimentação, em V.</summary>
    public DadoTecnico? TensaoAlimentacao { get; init; }

    /// <summary>A corrente da alimentação, em A.</summary>
    public DadoTecnico? CorrenteAlimentacao { get; init; }

    /// <summary>As vias de dentro de uma caixa ativa, na ordem.</summary>
    public IReadOnlyList<Via> Vias { get; init; } = [];

    /// <summary>Um campo numérico da ficha: o nome guardado no JSON, como ler e como trocar.</summary>
    public sealed record Campo(string Nome, Func<DadosTecnicos, DadoTecnico?> Ler, Func<DadosTecnicos, DadoTecnico?, DadosTecnicos> Com);

    /// <summary>Os campos numéricos, na ordem da ficha. O nome é a chave no <c>setups.json</c> e no <c>modelos.json</c>.</summary>
    public static IReadOnlyList<Campo> Campos { get; } =
    [
        new("diametro", d => d.Diametro, (d, v) => d with { Diametro = v }),
        new("impedancia", d => d.Impedancia, (d, v) => d with { Impedancia = v }),
        new("potenciaRms", d => d.PotenciaRms, (d, v) => d with { PotenciaRms = v }),
        new("sensibilidade", d => d.Sensibilidade, (d, v) => d with { Sensibilidade = v }),
        new("faixaMinima", d => d.FaixaMinima, (d, v) => d with { FaixaMinima = v }),
        new("faixaMaxima", d => d.FaixaMaxima, (d, v) => d with { FaixaMaxima = v }),
        new("ganho", d => d.Ganho, (d, v) => d with { Ganho = v }),
        new("canais", d => d.Canais, (d, v) => d with { Canais = v }),
        new("re", d => d.Re, (d, v) => d with { Re = v }),
        new("fs", d => d.Fs, (d, v) => d with { Fs = v }),
        new("qes", d => d.Qes, (d, v) => d with { Qes = v }),
        new("qms", d => d.Qms, (d, v) => d with { Qms = v }),
        new("vas", d => d.Vas, (d, v) => d with { Vas = v }),
        new("bl", d => d.Bl, (d, v) => d with { Bl = v }),
        new("sd", d => d.Sd, (d, v) => d with { Sd = v }),
        new("xmax", d => d.Xmax, (d, v) => d with { Xmax = v }),
        new("le", d => d.Le, (d, v) => d with { Le = v }),
        new("tMaxBobina", d => d.TemperaturaMaximaBobina, (d, v) => d with { TemperaturaMaximaBobina = v }),
        new("rthBobinaIma", d => d.RthBobinaIma, (d, v) => d with { RthBobinaIma = v }),
        new("rthImaAr", d => d.RthImaAr, (d, v) => d with { RthImaAr = v }),
        new("tauBobina", d => d.TauBobina, (d, v) => d with { TauBobina = v }),
        new("tauIma", d => d.TauIma, (d, v) => d with { TauIma = v }),
        new("volumeCaixa", d => d.VolumeCaixa, (d, v) => d with { VolumeCaixa = v }),
        new("fb", d => d.Fb, (d, v) => d with { Fb = v }),
        new("areaDuto", d => d.AreaDuto, (d, v) => d with { AreaDuto = v }),
        new("potenciaCanal", d => d.PotenciaCanal, (d, v) => d with { PotenciaCanal = v }),
        new("tensaoCeifamentoCanal", d => d.TensaoCeifamentoCanal, (d, v) => d with { TensaoCeifamentoCanal = v }),
        new("potenciaPonte", d => d.PotenciaPonte, (d, v) => d with { PotenciaPonte = v }),
        new("tensaoCeifamentoPonte", d => d.TensaoCeifamentoPonte, (d, v) => d with { TensaoCeifamentoPonte = v }),
        new("ganhoPonte", d => d.GanhoPonte, (d, v) => d with { GanhoPonte = v }),
        new("tensaoAlimentacao", d => d.TensaoAlimentacao, (d, v) => d with { TensaoAlimentacao = v }),
        new("correnteAlimentacao", d => d.CorrenteAlimentacao, (d, v) => d with { CorrenteAlimentacao = v }),
    ];

    /// <summary>A ficha só com os dados das origens dadas (o tipo da caixa também); as vias ficam de fora.</summary>
    public DadosTecnicos So(params OrigemDado[] origens)
    {
        var d = Nenhum;
        foreach (var c in Campos)
        {
            if (c.Ler(this) is { } v && origens.Contains(v.Origem))
                d = c.Com(d, v);
        }
        return TipoCaixa is { } t && origens.Contains(t.Origem) ? d with { TipoCaixa = t } : d;
    }

    /// <summary>A mesma ficha com cada dado (e o tipo da caixa) como digitado, sem a fonte; as vias ficam.</summary>
    public DadosTecnicos ComoDigitados()
    {
        var d = this;
        foreach (var c in Campos)
        {
            if (c.Ler(d) is { } v)
                d = c.Com(d, new DadoTecnico(v.Valor, OrigemDado.Digitado));
        }
        return d.TipoCaixa is { } t ? d with { TipoCaixa = new DadoTexto(t.Valor, OrigemDado.Digitado) } : d;
    }

    /// <summary>Nenhum dado (nem o tipo da caixa, nem as vias).</summary>
    public bool Vazia => Campos.All(c => c.Ler(this) is null) && TipoCaixa is null && Vias.Count == 0;
}

/// <summary>
/// Um equipamento de um Setup (CAP-16). Criar, editar e tirar não manda nada ao aparelho.
/// </summary>
/// <param name="Id">Único dentro do Setup e estável.</param>
/// <param name="Grupo">O grupo do modelo de ligação.</param>
/// <param name="Categoria">Texto livre ("tweeter", "módulo", "caixa ativa 2 vias"…), com sugestões por grupo.</param>
/// <param name="Nome">O nome na tela e nas saídas ("JBL R").</param>
/// <param name="Modelo">O modelo ("JBL 4TRMS80"); null = genérico.</param>
/// <param name="Funcao">A função acústica escolhida pelo usuário; null = a sugerida.</param>
public sealed record Equipamento(
    string Id,
    GrupoEquipamento Grupo,
    string Categoria,
    string Nome,
    string? Modelo = null,
    FuncaoAcustica? Funcao = null)
{
    public DadosTecnicos Dados { get; init; } = DadosTecnicos.Nenhum;

    /// <summary>Grupos 1, 3 e 4: as entradas de linha. Grupo 2: os canais do módulo (1 a N). Vazio no alto-falante.</summary>
    public IReadOnlyList<Entrada> Entradas { get; init; } = [];

    /// <summary>Só no grupo 2: os bridges do módulo.</summary>
    public IReadOnlyList<Bridge> Bridges { get; init; } = [];

    /// <summary>Só no alto-falante passivo: onde ele está ligado (null = em nada).</summary>
    public LigacaoAltoFalante? Ligado { get; init; }

    /// <summary>Uma nota, ou null.</summary>
    public string? Nota { get; init; }

    /// <summary>O modelo para a tela: "genérico" quando não se sabe.</summary>
    public string ModeloExibido => string.IsNullOrWhiteSpace(Modelo) ? Ligacoes.TextoGenerico : Modelo;

    /// <summary>Toca som: o grupo 1 e os alto-falantes passivos (são eles que dão nome às saídas).</summary>
    public bool Toca => Grupo is GrupoEquipamento.AmplificaETocam or GrupoEquipamento.AltoFalante;
}

/// <summary>
/// Uma saída do app (CAP-3, CAP-16): um canal do processador, ou um par vizinho com o link ligado,
/// que vira uma saída só e escreve nos dois.
/// </summary>
/// <param name="Mestre">O canal mestre (0 a 3): o próprio canal, ou o ímpar do par (CH1, CH3).</param>
/// <param name="Canais">Os canais da saída (0 a 3), em ordem.</param>
/// <param name="Nome">Dos equipamentos que ela alimenta ("JBL L", "Tweeter 1 + Tweeter 2"), ou "CH1", "CH3 + CH4".</param>
/// <param name="Aviso">Os avisos dos bridges que ela alimenta (par sem link, canais de saídas diferentes, sinal num canal só), ou null.</param>
public sealed record SaidaApp(int Mestre, IReadOnlyList<int> Canais, string Nome, string? Aviso)
{
    /// <summary>A cor: a do canal mestre (1 a 4, os tokens <c>canal-1</c> a <c>canal-4</c>).</summary>
    public int Cor => Mestre + 1;

    /// <summary>"CH1", "CH3 + CH4".</summary>
    public string Rotulo => Ligacoes.RotuloCanais(Canais);

    /// <summary>É um par com link.</summary>
    public bool Par => Canais.Count > 1;
}

/// <summary>
/// Um alto-falante passivo no caminho de um canal do processador: o módulo que o alimenta e o canal
/// (a partir de 1) ou o bridge em que ele está ligado (<see cref="Ligacoes.Caminhos"/>).
/// </summary>
public sealed record CaminhoAltoFalante(Equipamento AltoFalante, Equipamento Modulo, int? CanalModulo, Bridge? Bridge)
{
    /// <summary>No bridge: o ceifamento e o ganho são os da ponte.</summary>
    public bool Ponte => Bridge is not null;
}

/// <summary>O corte lido de um canal: a frequência de cada filtro, ou null com o filtro em OFF (ou sem leitura).</summary>
public sealed record CorteCanal(decimal? PassaAlta, decimal? PassaBaixa);

/// <summary>Como ficou a lista ao tirar um equipamento: a lista nova e os nomes dos que perderam a ligação ou a fonte.</summary>
public sealed record EquipamentoTirado(IReadOnlyList<Equipamento> Lista, IReadOnlyList<string> SemLigacao, IReadOnlyList<string> SemFonte);

/// <summary>
/// A lógica pura do modelo de ligação: grupos, categorias sugeridas, função acústica, validação das
/// ligações (bridge vizinho, um bridge por canal, sem ciclo, sem referência quebrada), a cadeia de
/// alimentação, as saídas do app e a sugestão de função.
/// </summary>
public static class Ligacoes
{
    public const int CanaisProcessador = 4;
    public const int TamanhoMaximoTexto = 32;

    /// <summary>A nota e a descrição de uma via: até 120 caracteres, sem controle.</summary>
    public const int TamanhoMaximoTextoLongo = 120;

    public const string TextoGenerico = "genérico";
    public const string TextoSemLigacao = "sem ligação";

    public const string MsgNomeInvalido = "Escreva o nome (até 32 caracteres).";
    public const string MsgCategoriaInvalida = "Escolha ou escreva a categoria (até 32 caracteres).";
    public const string MsgModeloInvalido = "O modelo vai até 32 caracteres.";
    public const string MsgIdRepetido = "Dois equipamentos com o mesmo id.";
    public const string MsgIdVazio = "O equipamento precisa de um id.";
    public const string MsgNotaInvalida = "A nota vai até 120 caracteres.";
    public const string MsgViaInvalida = "A descrição de cada via vai até 120 caracteres.";
    public const string MsgFaixaInvertida = "O começo da faixa vem antes do fim.";
    public const string MsgSemEntrada = "O equipamento precisa de pelo menos uma entrada.";
    public const string MsgSemCanalModulo = "O módulo precisa de pelo menos um canal.";
    public const string MsgCanaisModulo = "Os canais do módulo vão de 1 em diante, sem pular nem repetir.";
    public const string MsgEntradasNumeros = "As entradas vão de 1 em diante, sem pular nem repetir.";
    public const string MsgBridgeVizinho = "O bridge junta dois canais vizinhos do módulo: 1+2, 3+4…";
    public const string MsgBridgeRepetido = "Um canal do módulo fica em um bridge só.";
    public const string MsgBridgeSoModulo = "Só o módulo tem bridge.";
    public const string MsgCanalProcessador = "O canal do processador vai de CH1 a CH4.";
    public const string MsgFonteInvalida = "A fonte é um canal do processador ou um equipamento do grupo 3.";
    public const string MsgFonteNaoExiste = "A fonte não existe.";
    public const string MsgFonteGrupoErrado = "Só um equipamento do grupo 3 repassa o sinal.";
    public const string MsgCiclo = "A cadeia do grupo 3 não pode voltar a ela mesma.";
    public const string MsgAltoFalanteSemEntrada = "O alto-falante passivo não tem entrada de linha: ele fica num módulo.";
    public const string MsgSoAltoFalanteLiga = "Só o alto-falante passivo fica ligado num módulo.";
    public const string MsgModuloNaoExiste = "O módulo não existe.";
    public const string MsgModuloGrupoErrado = "O alto-falante passivo só fica num módulo ou amplificador (grupo 2).";
    public const string MsgLigacaoCanalOuBridge = "Ligue o alto-falante num canal ou num bridge do módulo.";
    public const string MsgCanalModuloNaoExiste = "O módulo não tem esse canal.";
    public const string MsgBridgeNaoExiste = "O módulo não tem esse bridge.";
    public const string MsgNumeroInvalido = "Os dados técnicos são números maiores que zero.";
    public const string MsgCanaisInteiro = "O número de canais é inteiro.";
    public const string MsgFonteDadoInvalida = "A fonte de cada dado vai até 120 caracteres.";
    public const string MsgTipoCaixaInvalido = "O tipo da caixa vai até 32 caracteres.";

    /// <summary>"O Sub está ligado no bridge 3+4 deste módulo: religue ou tire o Sub antes."</summary>
    public static string MsgAltoFalantePerdeLigacao(IReadOnlyList<(string Nome, string Onde)> perdem) => perdem.Count == 1
        ? $"O {perdem[0].Nome} está ligado {perdem[0].Onde} deste módulo: religue ou tire o {perdem[0].Nome} antes."
        : $"{Lista(perdem.Select(p => p.Nome).ToArray())} estão ligados neste módulo: religue ou tire eles antes.";

    /// <summary>"O bridge 1+2 do Módulo recebe o CH2 e o CH3: os dois canais precisam ser iguais."</summary>
    public static string MsgBridgeCanaisDiferentes(string modulo, Bridge bridge, IEnumerable<int> canais) =>
        $"O bridge {bridge.Rotulo} do {modulo} recebe {Lista(canais.Order().Select(c => $"o CH{c + 1}").ToArray())}: os dois canais precisam ser iguais.";

    /// <summary>"O bridge 3+4 do Módulo só recebe sinal no canal 3: ligue os dois canais do bridge."</summary>
    public static string MsgBridgeUmCanal(string modulo, Bridge bridge, int canalComSinal) =>
        $"O bridge {bridge.Rotulo} do {modulo} só recebe sinal no canal {canalComSinal}: ligue os dois canais do bridge.";

    // ------------------------------------------------------------------ rótulos

    /// <summary>Os grupos na ordem da folha.</summary>
    public static IReadOnlyList<GrupoEquipamento> Grupos { get; } =
    [
        GrupoEquipamento.AmplificaETocam, GrupoEquipamento.Amplificador, GrupoEquipamento.Repassa,
        GrupoEquipamento.SoRecebe, GrupoEquipamento.AltoFalante,
    ];

    /// <summary>"Amplifica e toca", "Amplificador", "Processa e repassa", "Só recebe", "Alto-falante passivo".</summary>
    public static string RotuloGrupo(GrupoEquipamento grupo) => grupo switch
    {
        GrupoEquipamento.AmplificaETocam => "Amplifica e toca",
        GrupoEquipamento.Amplificador => "Amplificador",
        GrupoEquipamento.Repassa => "Processa e repassa",
        GrupoEquipamento.SoRecebe => "Só recebe",
        _ => "Alto-falante passivo",
    };

    /// <summary>Os exemplos de cada grupo, para a folha.</summary>
    public static string ExemplosGrupo(GrupoEquipamento grupo) => grupo switch
    {
        GrupoEquipamento.AmplificaETocam => "caixa ativa, sub ativo, monitor ativo, line array, soundbar",
        GrupoEquipamento.Amplificador => "módulo ou amplificador, com canais e bridge",
        GrupoEquipamento.Repassa => "processador, crossover, equalizador, distribuidor, transmissor",
        GrupoEquipamento.SoRecebe => "interface de áudio, gravador, mesa",
        _ => "tweeter, driver, médio, woofer, subwoofer…",
    };

    /// <summary>As categorias sugeridas do grupo (a lista é aberta: o usuário pode escrever outra).</summary>
    public static IReadOnlyList<string> CategoriasSugeridas(GrupoEquipamento grupo) => grupo switch
    {
        GrupoEquipamento.AmplificaETocam =>
            ["caixa ativa full-range", "caixa ativa 2 vias", "caixa ativa 3 vias", "sub ativo", "monitor ativo", "line array ativo", "soundbar", "receiver"],
        GrupoEquipamento.Amplificador => ["módulo", "amplificador", "amplificador integrado"],
        GrupoEquipamento.Repassa => ["processador", "crossover ativo", "equalizador", "distribuidor de linha", "transmissor sem fio"],
        GrupoEquipamento.SoRecebe => ["interface de áudio", "gravador", "mesa de som"],
        _ => ["tweeter", "driver", "médio", "médio-grave", "woofer", "coaxial", "subwoofer", "shaker"],
    };

    /// <summary>As funções na ordem da folha.</summary>
    public static IReadOnlyList<FuncaoAcustica> Funcoes { get; } =
    [
        FuncaoAcustica.Sub, FuncaoAcustica.Grave, FuncaoAcustica.MedioGrave, FuncaoAcustica.Medio,
        FuncaoAcustica.Agudo, FuncaoAcustica.SuperAgudo, FuncaoAcustica.FullRange, FuncaoAcustica.Outra,
    ];

    public static string RotuloFuncao(FuncaoAcustica funcao) => funcao switch
    {
        FuncaoAcustica.Sub => "sub",
        FuncaoAcustica.Grave => "grave",
        FuncaoAcustica.MedioGrave => "médio-grave",
        FuncaoAcustica.Medio => "médio",
        FuncaoAcustica.Agudo => "agudo",
        FuncaoAcustica.SuperAgudo => "super-agudo",
        FuncaoAcustica.FullRange => "full-range",
        _ => "outra",
    };

    /// <summary>"CH1", "CH3 + CH4" (canais 0 a 3).</summary>
    public static string RotuloCanais(IEnumerable<int> canais) => string.Join(" + ", canais.Select(c => $"CH{c + 1}"));

    /// <summary>
    /// O nome de cada saída na tela (história 10), na mesma ordem: o nome da saída, e, quando duas
    /// saídas têm o mesmo nome (o Sub em CH3 e em CH4 com o Link 3–4 desligado), o nome com os canais
    /// ("Sub · CH3", "Sub · CH4"), para a tela nunca mostrar dois iguais.
    /// </summary>
    public static IReadOnlyList<string> NomesNaTela(IReadOnlyList<SaidaApp> saidas) =>
        saidas.Select(s => s.Nome != s.Rotulo && saidas.Count(o => string.Equals(o.Nome, s.Nome, StringComparison.Ordinal)) > 1
                ? $"{s.Nome} · {s.Rotulo}"
                : s.Nome)
            .ToArray();

    /// <summary>"A", "A e B", "A, B e C".</summary>
    public static string Lista(IReadOnlyList<string> nomes) => nomes.Count switch
    {
        0 => "",
        1 => nomes[0],
        _ => $"{string.Join(", ", nomes.Take(nomes.Count - 1))} e {nomes[^1]}",
    };

    // ------------------------------------------------------------------ textos

    /// <summary>O texto sem os espaços das pontas, de 1 a 32 caracteres e sem controle; null se não vale.</summary>
    public static string? TextoValido(string? digitado)
    {
        var texto = digitado?.Normalize(NormalizationForm.FormC).Trim();
        if (string.IsNullOrEmpty(texto) || texto.Length > TamanhoMaximoTexto || texto.Any(char.IsControl))
            return null;
        return texto;
    }

    /// <summary>A nota ou a descrição: sem os espaços das pontas, até 120 caracteres e sem controle; null se não vale.</summary>
    public static string? TextoLongoValido(string? digitado)
    {
        var texto = digitado?.Normalize(NormalizationForm.FormC).Trim();
        if (string.IsNullOrEmpty(texto) || texto.Length > TamanhoMaximoTextoLongo || texto.Any(char.IsControl))
            return null;
        return texto;
    }

    /// <summary>O equipamento com os textos limpos (nome, categoria e modelo; modelo vazio = genérico), ou o motivo.</summary>
    public static (Equipamento? Equipamento, string? Motivo) Limpar(Equipamento e)
    {
        if (TextoValido(e.Nome) is not { } nome)
            return (null, MsgNomeInvalido);
        if (TextoValido(e.Categoria) is not { } categoria)
            return (null, MsgCategoriaInvalida);
        string? modelo = null;
        if (!string.IsNullOrWhiteSpace(e.Modelo) && (modelo = TextoValido(e.Modelo)) is null)
            return (null, MsgModeloInvalido);
        string? nota = null;
        if (!string.IsNullOrWhiteSpace(e.Nota) && (nota = TextoLongoValido(e.Nota)) is null)
            return (null, MsgNotaInvalida);
        var vias = new List<Via>();
        foreach (var v in e.Dados.Vias.Where(v => !string.IsNullOrWhiteSpace(v.Descricao)))
        {
            if (TextoLongoValido(v.Descricao) is not { } descricao)
                return (null, MsgViaInvalida);
            vias.Add(v with { Descricao = descricao });
        }
        var dados = e.Dados with { Vias = vias };
        foreach (var c in DadosTecnicos.Campos)
        {
            if (c.Ler(dados) is not { Fonte: { } fonte } v)
                continue;
            if (string.IsNullOrWhiteSpace(fonte))
                dados = c.Com(dados, v with { Fonte = null });
            else if (TextoLongoValido(fonte) is { } limpa)
                dados = c.Com(dados, v with { Fonte = limpa });
            else
                return (null, MsgFonteDadoInvalida);
        }
        if (dados.TipoCaixa is { } tipo)
        {
            if (string.IsNullOrWhiteSpace(tipo.Valor))
            {
                dados = dados with { TipoCaixa = null };
            }
            else
            {
                if (TextoValido(tipo.Valor) is not { } valor)
                    return (null, MsgTipoCaixaInvalido);
                string? fonteTipo = null;
                if (!string.IsNullOrWhiteSpace(tipo.Fonte) && (fonteTipo = TextoLongoValido(tipo.Fonte)) is null)
                    return (null, MsgFonteDadoInvalida);
                dados = dados with { TipoCaixa = tipo with { Valor = valor, Fonte = fonteTipo } };
            }
        }
        return (e with { Nome = nome, Categoria = categoria, Modelo = modelo, Nota = nota, Dados = dados }, null);
    }

    // ------------------------------------------------------------------ validação

    /// <summary>
    /// Confere um equipamento sozinho (os textos, os números, a forma das entradas, dos bridges e da
    /// ligação), sem olhar os outros. Devolve o motivo, ou null.
    /// </summary>
    public static string? ValidarUm(Equipamento e)
    {
        if (string.IsNullOrWhiteSpace(e.Id))
            return MsgIdVazio;
        if (TextoValido(e.Nome) is null)
            return MsgNomeInvalido;
        if (TextoValido(e.Categoria) is null)
            return MsgCategoriaInvalida;
        if (!string.IsNullOrWhiteSpace(e.Modelo) && TextoValido(e.Modelo) is null)
            return MsgModeloInvalido;
        if (!DadosValidos(e.Dados))
            return MsgNumeroInvalido;
        if (e.Dados.Canais is { } canais && decimal.Truncate(canais.Valor) != canais.Valor)
            return MsgCanaisInteiro;
        if (e.Dados.FaixaMinima is { } minimo && e.Dados.FaixaMaxima is { } maximo && minimo.Valor >= maximo.Valor)
            return MsgFaixaInvertida;
        if (DadosTecnicos.Campos.Any(c => c.Ler(e.Dados)?.Fonte is { } fonte && TextoLongoValido(fonte) is null))
            return MsgFonteDadoInvalida;
        if (e.Dados.TipoCaixa is { } tipo && TextoValido(tipo.Valor) is null)
            return MsgTipoCaixaInvalido;
        if (e.Dados.TipoCaixa?.Fonte is { } fonteTipo && TextoLongoValido(fonteTipo) is null)
            return MsgFonteDadoInvalida;
        if (e.Nota is not null && TextoLongoValido(e.Nota) is null)
            return MsgNotaInvalida;
        if (e.Dados.Vias.Any(v => TextoLongoValido(v.Descricao) is null))
            return MsgViaInvalida;

        if (e.Grupo == GrupoEquipamento.AltoFalante)
        {
            if (e.Entradas.Count > 0)
                return MsgAltoFalanteSemEntrada;
            if (e.Bridges.Count > 0)
                return MsgBridgeSoModulo;
            if (e.Ligado is { } l && (l.Canal is null == l.Bridge is null || string.IsNullOrWhiteSpace(l.IdModulo)))
                return MsgLigacaoCanalOuBridge;
            return null;
        }

        if (e.Ligado is not null)
            return MsgSoAltoFalanteLiga;
        if (e.Entradas.Count == 0)
            return e.Grupo == GrupoEquipamento.Amplificador ? MsgSemCanalModulo : MsgSemEntrada;
        for (var i = 0; i < e.Entradas.Count; i++)
        {
            if (e.Entradas[i].Numero != i + 1)
                return e.Grupo == GrupoEquipamento.Amplificador ? MsgCanaisModulo : MsgEntradasNumeros;
            if (e.Entradas[i].Fonte is { } f)
            {
                if (f.Canal is null == f.IdEquipamento is null)
                    return MsgFonteInvalida;
                if (f.Canal is { } c && c is < 0 or >= CanaisProcessador)
                    return MsgCanalProcessador;
                if (f.IdEquipamento is { } id && string.IsNullOrWhiteSpace(id))
                    return MsgFonteInvalida;
            }
        }

        if (e.Grupo != GrupoEquipamento.Amplificador)
            return e.Bridges.Count > 0 ? MsgBridgeSoModulo : null;
        var usados = new HashSet<int>();
        foreach (var b in e.Bridges)
        {
            if (b.A < 1 || b.A % 2 != 1 || b.B != b.A + 1 || b.B > e.Entradas.Count)
                return MsgBridgeVizinho;
            if (!usados.Add(b.A) || !usados.Add(b.B))
                return MsgBridgeRepetido;
        }
        return null;
    }

    private static bool DadosValidos(DadosTecnicos d) =>
        DadosTecnicos.Campos.All(c => c.Ler(d) is not { } v || (v.Valor > 0m && Enum.IsDefined(v.Origem)))
        && (d.TipoCaixa is null || Enum.IsDefined(d.TipoCaixa.Origem))
        && d.Vias.All(v => v.Diametro is null or > 0m);

    /// <summary>
    /// Confere a lista inteira: cada equipamento (<see cref="ValidarUm"/>), ids sem repetir, as
    /// referências (fonte do grupo 3, alto-falante num canal ou bridge que existe de um módulo) e a
    /// cadeia do grupo 3 sem ciclo. Devolve o motivo, ou null. Com <paramref name="idEditado"/> (o
    /// módulo que acabou de mudar), os alto-falantes que perderiam o canal ou o bridge dele vêm no
    /// motivo, pelo nome.
    /// </summary>
    public static string? Validar(IReadOnlyList<Equipamento> lista, string? idEditado = null)
    {
        var porId = new Dictionary<string, Equipamento>();
        foreach (var e in lista)
        {
            if (ValidarUm(e) is { } motivo)
                return motivo;
            if (!porId.TryAdd(e.Id, e))
                return MsgIdRepetido;
        }

        if (idEditado is not null && porId.TryGetValue(idEditado, out var editado))
        {
            var perdem = lista
                .Where(e => e.Ligado is { } l && l.IdModulo == idEditado && !LigacaoExiste(editado, l))
                .Select(e => (e.Nome, e.Ligado!.Canal is { } c ? $"no canal {c}" : $"no bridge {e.Ligado.Bridge!.Rotulo}"))
                .ToArray();
            if (perdem.Length > 0)
                return MsgAltoFalantePerdeLigacao(perdem);
        }

        foreach (var e in lista)
        {
            foreach (var entrada in e.Entradas)
            {
                if (entrada.Fonte?.IdEquipamento is not { } id)
                    continue;
                if (!porId.TryGetValue(id, out var fonte))
                    return MsgFonteNaoExiste;
                if (fonte.Grupo != GrupoEquipamento.Repassa)
                    return MsgFonteGrupoErrado;
            }
            if (e.Ligado is { } l)
            {
                if (!porId.TryGetValue(l.IdModulo, out var modulo))
                    return MsgModuloNaoExiste;
                if (modulo.Grupo != GrupoEquipamento.Amplificador)
                    return MsgModuloGrupoErrado;
                if (l.Canal is { } c && (c < 1 || c > modulo.Entradas.Count))
                    return MsgCanalModuloNaoExiste;
                if (l.Bridge is { } b && !modulo.Bridges.Contains(b))
                    return MsgBridgeNaoExiste;
            }
        }

        return TemCiclo(lista) ? MsgCiclo : null;
    }

    /// <summary>O canal ou o bridge da ligação existe no módulo (e ele é do grupo 2).</summary>
    private static bool LigacaoExiste(Equipamento modulo, LigacaoAltoFalante l) =>
        modulo.Grupo == GrupoEquipamento.Amplificador
        && ((l.Canal is { } c && c >= 1 && c <= modulo.Entradas.Count) || (l.Bridge is { } b && modulo.Bridges.Contains(b)));

    /// <summary>
    /// O módulo só com os bridges que valem (dois canais vizinhos que ele tem, cada canal num bridge
    /// só; o primeiro fica). Outro grupo passa como está.
    /// </summary>
    public static Equipamento SemBridgesInvalidos(Equipamento e)
    {
        if (e.Grupo != GrupoEquipamento.Amplificador)
            return e;
        var usados = new HashSet<int>();
        var validos = e.Bridges
            .Where(b => b.A >= 1 && b.A % 2 == 1 && b.B == b.A + 1 && b.B <= e.Entradas.Count && usados.Add(b.A) && usados.Add(b.B))
            .ToArray();
        return validos.Length == e.Bridges.Count ? e : e with { Bridges = validos };
    }

    /// <summary>A cadeia do grupo 3 volta a ela mesma (um equipamento alimentado, direto ou não, por ele mesmo).</summary>
    private static bool TemCiclo(IReadOnlyList<Equipamento> lista)
    {
        var fontes = lista.ToDictionary(
            e => e.Id,
            e => e.Entradas.Select(x => x.Fonte?.IdEquipamento).OfType<string>().Distinct().ToArray());
        var estado = new Dictionary<string, int>(); // 1 = visitando, 2 = pronto
        bool Visitar(string id)
        {
            if (estado.TryGetValue(id, out var s))
                return s == 1;
            estado[id] = 1;
            foreach (var f in fontes.GetValueOrDefault(id) ?? [])
            {
                if (Visitar(f))
                    return true;
            }
            estado[id] = 2;
            return false;
        }
        return lista.Any(e => Visitar(e.Id));
    }

    /// <summary>
    /// Deixa a lista lida de um arquivo sempre válida: fica o primeiro de cada id; tira os bridges que
    /// não valem (o módulo fica), as fontes e as ligações quebradas e, numa cadeia do grupo 3 que volta
    /// a ela mesma, as fontes do grupo 3 de quem está no ciclo. Os equipamentos ficam.
    /// </summary>
    public static IReadOnlyList<Equipamento> Sanear(IReadOnlyList<Equipamento> lista)
    {
        var unicos = lista.GroupBy(e => e.Id).Select(g => SemBridgesInvalidos(g.First())).ToArray();
        var porId = unicos.ToDictionary(e => e.Id);
        var saida = unicos.Select(e =>
        {
            var entradas = e.Entradas.Select(x =>
                x.Fonte?.IdEquipamento is { } id && (!porId.TryGetValue(id, out var f) || f.Grupo != GrupoEquipamento.Repassa)
                    ? x with { Fonte = null }
                    : x).ToArray();
            var ligado = e.Ligado;
            if (ligado is not null && (!porId.TryGetValue(ligado.IdModulo, out var m) || !LigacaoExiste(m, ligado)))
                ligado = null;
            return e with { Entradas = entradas, Ligado = ligado };
        }).ToList();

        for (var volta = 0; volta < saida.Count && TemCiclo(saida); volta++)
        {
            // Corta as fontes do grupo 3 do primeiro que está num ciclo.
            var i = saida.FindIndex(e => EstaEmCiclo(saida, e.Id));
            if (i < 0)
                break;
            saida[i] = saida[i] with
            {
                Entradas = saida[i].Entradas.Select(x => x.Fonte?.IdEquipamento is not null ? x with { Fonte = null } : x).ToArray(),
            };
        }
        return saida;
    }

    private static bool EstaEmCiclo(IReadOnlyList<Equipamento> lista, string id)
    {
        var porId = lista.ToDictionary(e => e.Id);
        var vistos = new HashSet<string>();
        var fila = new Queue<string>(porId[id].Entradas.Select(x => x.Fonte?.IdEquipamento).OfType<string>());
        while (fila.Count > 0)
        {
            var atual = fila.Dequeue();
            if (atual == id)
                return true;
            if (!vistos.Add(atual) || !porId.TryGetValue(atual, out var e))
                continue;
            foreach (var f in e.Entradas.Select(x => x.Fonte?.IdEquipamento).OfType<string>())
                fila.Enqueue(f);
        }
        return false;
    }

    // ------------------------------------------------------------------ a cadeia de alimentação

    /// <summary>O que os canais alimentam: os equipamentos, os canais de cada módulo e os bridges.</summary>
    private sealed record Alcance(HashSet<string> Equipamentos, Dictionary<string, HashSet<int>> CanaisModulo, HashSet<(string, Bridge)> Bridges);

    /// <summary>
    /// Segue o sinal dos canais do processador (0 a 3): as entradas alimentadas por eles, o repasse do
    /// grupo 3 (tudo o que ele recebe sai para quem ele alimenta), os canais dos módulos e os
    /// alto-falantes nesses canais ou nos bridges que os juntam.
    /// </summary>
    private static Alcance Seguir(IReadOnlyList<Equipamento> lista, IReadOnlyCollection<int> canais)
    {
        var equipamentos = new HashSet<string>();
        var canaisModulo = new Dictionary<string, HashSet<int>>();
        var mudou = true;
        while (mudou)
        {
            mudou = false;
            foreach (var e in lista)
            {
                if (e.Grupo == GrupoEquipamento.AltoFalante)
                    continue;
                foreach (var entrada in e.Entradas)
                {
                    if (entrada.Fonte is not { } f)
                        continue;
                    var alimentada = f.Canal is { } c ? canais.Contains(c) : f.IdEquipamento is { } id && equipamentos.Contains(id);
                    if (!alimentada)
                        continue;
                    if (equipamentos.Add(e.Id))
                        mudou = true;
                    if (e.Grupo == GrupoEquipamento.Amplificador)
                    {
                        if (!canaisModulo.TryGetValue(e.Id, out var doModulo))
                            canaisModulo[e.Id] = doModulo = [];
                        doModulo.Add(entrada.Numero);
                    }
                }
            }
        }

        var bridges = new HashSet<(string, Bridge)>();
        foreach (var e in lista.Where(e => e.Grupo == GrupoEquipamento.Amplificador))
        {
            if (!canaisModulo.TryGetValue(e.Id, out var doModulo))
                continue;
            foreach (var b in e.Bridges.Where(b => doModulo.Contains(b.A) || doModulo.Contains(b.B)))
                bridges.Add((e.Id, b));
        }
        foreach (var e in lista.Where(e => e.Grupo == GrupoEquipamento.AltoFalante))
        {
            if (e.Ligado is not { } l || !canaisModulo.TryGetValue(l.IdModulo, out var doModulo))
                continue;
            if ((l.Canal is { } c && doModulo.Contains(c)) || (l.Bridge is { } b && bridges.Contains((l.IdModulo, b))))
                equipamentos.Add(e.Id);
        }
        return new Alcance(equipamentos, canaisModulo, bridges);
    }

    /// <summary>Os equipamentos que os canais (0 a 3) alimentam, direto ou pela cadeia, na ordem de criação.</summary>
    public static IReadOnlyList<Equipamento> Alimentados(IReadOnlyList<Equipamento> lista, IReadOnlyCollection<int> canais)
    {
        var alcance = Seguir(lista, canais);
        return lista.Where(e => alcance.Equipamentos.Contains(e.Id)).ToArray();
    }

    /// <summary>
    /// O caminho do canal do processador (0 a 3) até os alto-falantes passivos (história 13a): cada um
    /// que o canal alimenta, direto ou pela cadeia do grupo 3, com o módulo e o canal ou o bridge em que
    /// ele está ligado, na ordem de criação.
    /// </summary>
    internal static IReadOnlyList<CaminhoAltoFalante> Caminhos(IReadOnlyList<Equipamento> lista, int canal)
    {
        var alcance = Seguir(lista, [canal]);
        var modulos = lista.Where(e => e.Grupo == GrupoEquipamento.Amplificador)
            .GroupBy(e => e.Id).ToDictionary(g => g.Key, g => g.First());
        var caminhos = new List<CaminhoAltoFalante>();
        foreach (var e in lista.Where(e => e.Grupo == GrupoEquipamento.AltoFalante && alcance.Equipamentos.Contains(e.Id)))
        {
            if (e.Ligado is { } l && modulos.TryGetValue(l.IdModulo, out var modulo))
                caminhos.Add(new CaminhoAltoFalante(e, modulo, l.Canal, l.Bridge));
        }
        return caminhos;
    }

    /// <summary>
    /// Os alto-falantes passivos que nenhum canal do processador alcança (história 13a), com o motivo:
    /// desligados, ligados a um equipamento que não é amplificador, ou num canal ou ponte do módulo sem
    /// sinal do processador. Ficam fora da conta dos limites, na ordem de criação.
    /// </summary>
    internal static IReadOnlyList<(Equipamento AltoFalante, string Motivo)> ForaDosCaminhos(IReadOnlyList<Equipamento> lista)
    {
        var alcance = Seguir(lista, Enumerable.Range(0, CanaisProcessador).ToArray());
        var porId = lista.GroupBy(e => e.Id).ToDictionary(g => g.Key, g => g.First());
        var fora = new List<(Equipamento, string)>();
        foreach (var e in lista.Where(e => e.Grupo == GrupoEquipamento.AltoFalante && !alcance.Equipamentos.Contains(e.Id)))
        {
            string motivo;
            if (e.Ligado is not { } l)
                motivo = "Desligado: não está em nenhum canal de módulo.";
            else if (!porId.TryGetValue(l.IdModulo, out var m))
                motivo = "Ligado a um equipamento que não existe mais.";
            else if (m.Grupo != GrupoEquipamento.Amplificador)
                motivo = $"Ligado ao {m.Nome}, que não é amplificador.";
            else if (l.Bridge is { } b)
                motivo = $"A ponte {b.Rotulo} do {m.Nome} não recebe sinal do processador.";
            else
                motivo = $"O canal {l.Canal} do {m.Nome} não recebe sinal do processador.";
            fora.Add((e, motivo));
        }
        return fora;
    }

    /// <summary>Os canais do processador (0 a 3) que alimentam o equipamento, em ordem.</summary>
    public static IReadOnlyList<int> CanaisQueAlimentam(IReadOnlyList<Equipamento> lista, string idEquipamento) =>
        Enumerable.Range(0, CanaisProcessador).Where(c => Seguir(lista, [c]).Equipamentos.Contains(idEquipamento)).ToArray();

    // ------------------------------------------------------------------ saídas do app

    /// <summary>
    /// O link automático (decisão D1 do Denis, 29/10/2026): o par vizinho do processador (CH1+CH2 →
    /// Link 1–2; CH3+CH4 → Link 3–4) fica ligado quando os dois canais de um bridge de um equipamento
    /// do grupo 2 são alimentados, cada um, por um dos dois canais do par (um pelo CH3 e o outro pelo
    /// CH4, direto ou pela cadeia do grupo 3, e por nenhum outro canal). Senão, sem link. O usuário
    /// não mexe nele.
    /// </summary>
    public static (bool Link12, bool Link34) Links(IReadOnlyList<Equipamento> lista)
    {
        var porCanal = Enumerable.Range(0, CanaisProcessador).Select(c => Seguir(lista, [c])).ToArray();
        bool Ligado(int a, int b)
        {
            foreach (var m in lista.Where(e => e.Grupo == GrupoEquipamento.Amplificador))
            {
                foreach (var br in m.Bridges)
                {
                    int[] Quem(int canalModulo) => Enumerable.Range(0, CanaisProcessador)
                        .Where(c => porCanal[c].CanaisModulo.TryGetValue(m.Id, out var x) && x.Contains(canalModulo)).ToArray();
                    var deA = Quem(br.A);
                    var deB = Quem(br.B);
                    if (deA.Length == 1 && deB.Length == 1 && deA[0] != deB[0] && deA.Concat(deB).All(c => c == a || c == b))
                        return true;
                }
            }
            return false;
        }
        return (Ligado(0, 1), Ligado(2, 3));
    }

    /// <summary>As saídas do app com o link tirado das ligações (<see cref="Links"/>).</summary>
    public static IReadOnlyList<SaidaApp> Saidas(IReadOnlyList<Equipamento> lista)
    {
        var (link12, link34) = Links(lista);
        return Saidas(lista, link12, link34);
    }

    /// <summary>
    /// As saídas do app, na ordem dos canais: cada par vizinho (1–2, 3–4) com o link ligado vira uma
    /// saída só, com o mestre no canal ímpar; senão, cada canal é uma saída. O nome vem dos que tocam
    /// e que a saída alimenta (sem repetir, na ordem de criação); sem nenhum que toca, dos outros que
    /// ela alimenta; sem nada, "CH1" ou "CH3 + CH4". Os avisos dos bridges vão para as saídas que os
    /// alimentam: um bridge com canais de saídas diferentes; um bridge com sinal num canal só. O link
    /// vem de quem chama (o que vale agora no app: sem Setup em uso, desligado).
    /// </summary>
    public static IReadOnlyList<SaidaApp> Saidas(IReadOnlyList<Equipamento> lista, bool link12, bool link34)
    {
        var grupos = new List<int[]>();
        foreach (var (a, b, link) in new[] { (0, 1, link12), (2, 3, link34) })
        {
            if (link)
            {
                grupos.Add([a, b]);
            }
            else
            {
                grupos.Add([a]);
                grupos.Add([b]);
            }
        }

        var avisos = grupos.Select(_ => new List<string>()).ToArray();
        int SaidaDoCanal(int canal) => grupos.FindIndex(g => g.Contains(canal));
        void Avisar(IEnumerable<int> canais, string aviso)
        {
            foreach (var i in canais.Select(SaidaDoCanal).Distinct())
            {
                if (!avisos[i].Contains(aviso))
                    avisos[i].Add(aviso);
            }
        }

        var porCanal = Enumerable.Range(0, CanaisProcessador).Select(c => Seguir(lista, [c])).ToArray();
        foreach (var m in lista.Where(e => e.Grupo == GrupoEquipamento.Amplificador))
        {
            foreach (var b in m.Bridges)
            {
                int[] Quem(int canalModulo) => Enumerable.Range(0, CanaisProcessador)
                    .Where(c => porCanal[c].CanaisModulo.TryGetValue(m.Id, out var x) && x.Contains(canalModulo)).ToArray();
                var deA = Quem(b.A);
                var deB = Quem(b.B);
                if (deA.Length == 0 && deB.Length == 0)
                    continue;
                if (deA.Length == 0 || deB.Length == 0)
                {
                    Avisar(deA.Concat(deB), MsgBridgeUmCanal(m.Nome, b, deA.Length > 0 ? b.A : b.B));
                    continue;
                }
                var saidasA = deA.Select(SaidaDoCanal).ToHashSet();
                var saidasB = deB.Select(SaidaDoCanal).ToHashSet();
                if (saidasA.Count == 1 && saidasA.SetEquals(saidasB))
                    continue;
                var todos = deA.Union(deB).Order().ToArray();
                Avisar(todos, MsgBridgeCanaisDiferentes(m.Nome, b, todos));
            }
        }

        return grupos.Select((g, i) => Saida(lista, g, avisos[i].Count == 0 ? null : string.Join(" ", avisos[i]))).ToArray();
    }

    private static SaidaApp Saida(IReadOnlyList<Equipamento> lista, int[] canais, string? aviso)
    {
        var alimentados = Alimentados(lista, canais);
        var tocam = alimentados.Where(e => e.Toca).Select(e => e.Nome).Distinct(StringComparer.Ordinal).ToArray();
        var nomes = tocam.Length > 0 ? tocam : alimentados.Select(e => e.Nome).Distinct(StringComparer.Ordinal).ToArray();
        var nome = nomes.Length > 0 ? string.Join(" + ", nomes) : RotuloCanais(canais);
        return new SaidaApp(canais[0], canais, nome, aviso);
    }

    // ------------------------------------------------------------------ sugestão de função

    /// <summary>
    /// A função acústica sugerida: pela categoria (tweeter → agudo; driver → agudo com passa-alta a
    /// partir de 2 kHz, senão médio; médio; médio-grave; woofer → grave; subwoofer e sub ativo → sub;
    /// coaxial e caixa ativa full-range → full-range); senão, pelo corte do canal que alimenta: um
    /// passa-baixa até 200 Hz → sub; um passa-alta a partir de 2 kHz → agudo; senão, full-range.
    /// </summary>
    public static FuncaoAcustica SugerirFuncao(string? categoria, CorteCanal? corte)
    {
        var chave = Normalizar(categoria);
        switch (chave)
        {
            case "tweeter":
                return FuncaoAcustica.Agudo;
            case "driver":
                return corte?.PassaAlta >= 2000m ? FuncaoAcustica.Agudo : FuncaoAcustica.Medio;
            case "medio":
                return FuncaoAcustica.Medio;
            case "medio-grave" or "medio grave":
                return FuncaoAcustica.MedioGrave;
            case "woofer":
                return FuncaoAcustica.Grave;
            case "subwoofer" or "sub ativo" or "subwoofer ativo":
                return FuncaoAcustica.Sub;
            case "coaxial" or "caixa ativa full-range" or "caixa ativa full range":
                return FuncaoAcustica.FullRange;
        }
        if (corte?.PassaBaixa is { } pb && pb <= 200m)
            return FuncaoAcustica.Sub;
        if (corte?.PassaAlta is { } pa && pa >= 2000m)
            return FuncaoAcustica.Agudo;
        return FuncaoAcustica.FullRange;
    }

    /// <summary>Minúsculas, sem acento, sem espaços repetidos.</summary>
    private static string Normalizar(string? texto)
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

    // ------------------------------------------------------------------ tirar

    /// <summary>
    /// Tira o equipamento: os alto-falantes de um módulo tirado ficam sem ligação, e quem um
    /// equipamento do grupo 3 tirado alimentava fica sem fonte. Os nomes deles vêm junto, para o aviso.
    /// </summary>
    public static EquipamentoTirado Tirar(IReadOnlyList<Equipamento> lista, string id)
    {
        var semLigacao = new List<string>();
        var semFonte = new List<string>();
        var nova = new List<Equipamento>();
        foreach (var e in lista)
        {
            if (e.Id == id)
                continue;
            var atual = e;
            if (e.Ligado?.IdModulo == id)
            {
                atual = atual with { Ligado = null };
                semLigacao.Add(e.Nome);
            }
            if (e.Entradas.Any(x => x.Fonte?.IdEquipamento == id))
            {
                atual = atual with { Entradas = e.Entradas.Select(x => x.Fonte?.IdEquipamento == id ? x with { Fonte = null } : x).ToArray() };
                semFonte.Add(e.Nome);
            }
            nova.Add(atual);
        }
        return new EquipamentoTirado(nova, semLigacao, semFonte);
    }
}
