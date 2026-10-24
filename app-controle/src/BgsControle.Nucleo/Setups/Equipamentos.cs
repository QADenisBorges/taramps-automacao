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

/// <summary>Uma via de dentro de uma caixa ativa ("woofer de 12"", "driver"), com o diâmetro em polegadas, se se sabe.</summary>
public sealed record Via(string Descricao, decimal? Diametro);

/// <summary>
/// Os dados técnicos de um equipamento, todos opcionais (o que não se sabe fica genérico).
/// </summary>
/// <param name="Diametro">Em polegadas.</param>
/// <param name="Impedancia">Em Ω.</param>
/// <param name="PotenciaRms">Em W.</param>
/// <param name="Sensibilidade">Em dB.</param>
/// <param name="FaixaMinima">O começo da faixa, em Hz.</param>
/// <param name="FaixaMaxima">O fim da faixa, em Hz.</param>
/// <param name="Ganho">O ganho do amplificador, em × (19,6 no módulo do Casa 2.1).</param>
/// <param name="Canais">O número de canais (informativo; num módulo, os canais são as <see cref="Equipamento.Entradas"/>).</param>
public sealed record DadosTecnicos(
    decimal? Diametro = null,
    decimal? Impedancia = null,
    decimal? PotenciaRms = null,
    decimal? Sensibilidade = null,
    decimal? FaixaMinima = null,
    decimal? FaixaMaxima = null,
    decimal? Ganho = null,
    int? Canais = null)
{
    public static DadosTecnicos Nenhum { get; } = new();

    /// <summary>As vias de dentro de uma caixa ativa, na ordem.</summary>
    public IReadOnlyList<Via> Vias { get; init; } = [];
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

    /// <summary>Uma nota (a do herdado), ou null.</summary>
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

    /// <summary>"CH1 e CH2 alimentam o mesmo bridge: ligue o Link 1–2 para ficarem iguais."</summary>
    public static string MsgBridgeSemLink(int a, int b) =>
        $"CH{a + 1} e CH{b + 1} alimentam o mesmo bridge: ligue o Link {a + 1}–{b + 1} para ficarem iguais.";

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
        return (e with { Nome = nome, Categoria = categoria, Modelo = modelo, Nota = nota, Dados = e.Dados with { Vias = vias } }, null);
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
        if (e.Dados.FaixaMinima is { } minimo && e.Dados.FaixaMaxima is { } maximo && minimo >= maximo)
            return MsgFaixaInvertida;
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
        new[] { d.Diametro, d.Impedancia, d.PotenciaRms, d.Sensibilidade, d.FaixaMinima, d.FaixaMaxima, d.Ganho }
            .All(v => v is null or > 0m)
        && d.Canais is null or > 0
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

    /// <summary>Os canais do processador (0 a 3) que alimentam o equipamento, em ordem.</summary>
    public static IReadOnlyList<int> CanaisQueAlimentam(IReadOnlyList<Equipamento> lista, string idEquipamento) =>
        Enumerable.Range(0, CanaisProcessador).Where(c => Seguir(lista, [c]).Equipamentos.Contains(idEquipamento)).ToArray();

    // ------------------------------------------------------------------ saídas do app

    /// <summary>
    /// As saídas do app, na ordem dos canais: cada par vizinho (1–2, 3–4) com o link ligado vira uma
    /// saída só, com o mestre no canal ímpar; senão, cada canal é uma saída. O nome vem dos que tocam
    /// e que a saída alimenta (sem repetir, na ordem de criação); sem nenhum que toca, dos outros que
    /// ela alimenta; sem nada, "CH1" ou "CH3 + CH4". Os avisos dos bridges vão para as saídas que os
    /// alimentam: os dois canais de um par sem link no mesmo bridge; um bridge com canais de saídas
    /// diferentes; um bridge com sinal num canal só.
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
                Avisar(todos, todos is [0, 1] or [2, 3]
                    ? MsgBridgeSemLink(todos[0], todos[1])
                    : MsgBridgeCanaisDiferentes(m.Nome, b, todos));
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
