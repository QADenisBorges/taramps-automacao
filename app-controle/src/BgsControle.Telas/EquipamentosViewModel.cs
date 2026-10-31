using System.Globalization;
using BgsControle.Nucleo.Setups;

namespace BgsControle.Telas;

/// <summary>Um grupo na folha: "Amplifica e toca" e os exemplos.</summary>
public sealed record OpcaoGrupo(GrupoEquipamento Grupo, string Rotulo, string Exemplos);

/// <summary>Uma função na folha; a primeira é a sugerida (<see cref="Funcao"/> null): "Sugerida: sub".</summary>
public sealed record OpcaoFuncao(string Rotulo, FuncaoAcustica? Funcao);

/// <summary>Uma fonte possível para uma entrada: "Sem ligação", "CH1" a "CH4" ou "Saída do Crossover".</summary>
public sealed record OpcaoFonte(string Rotulo, FonteSinal? Fonte);

/// <summary>Onde um alto-falante passivo pode ficar: "Sem ligação", "canal 2 do Módulo genérico", "bridge 3+4 do Módulo genérico".</summary>
public sealed record OpcaoLigado(string Rotulo, LigacaoAltoFalante? Ligado);

/// <summary>
/// Uma opção de escolha na tela (história 10; DESIGN, linha-de-escolha e as pílulas da folha de
/// equipamento): o rótulo, o detalhe, se é a escolhida (em branco) e o toque que escolhe.
/// </summary>
public sealed class EscolhaViewModel
{
    internal EscolhaViewModel(string rotulo, string? detalhe, bool escolhida, Action escolher)
        : this(rotulo, detalhe, escolhida, new Comando(escolher))
    {
    }

    internal EscolhaViewModel(string rotulo, string? detalhe, bool escolhida, Func<Task> escolher)
        : this(rotulo, detalhe, escolhida, new Comando(escolher))
    {
    }

    private EscolhaViewModel(string rotulo, string? detalhe, bool escolhida, Comando escolher)
    {
        Rotulo = rotulo;
        Detalhe = detalhe;
        Escolhida = escolhida;
        Escolher = escolher;
    }

    public string Rotulo { get; }

    public string? Detalhe { get; }

    public bool Escolhida { get; }

    public Comando Escolher { get; }

    /// <summary>O que o leitor de tela diz da pílula (o rótulo e se está escolhida), ou null (o rótulo basta).</summary>
    public string? Descricao { get; init; }
}

/// <summary>Uma linha da lista de equipamentos de um Setup: nome, detalhe, modelo ("genérico"), função e a cor do canal.</summary>
public sealed class ItemEquipamentoViewModel
{
    internal ItemEquipamentoViewModel(Equipamento e, string detalhe, string? funcao, int? canal, EquipamentosViewModel tela)
    {
        Id = e.Id;
        Nome = e.Nome;
        Detalhe = detalhe;
        Modelo = e.ModeloExibido;
        Generico = string.IsNullOrWhiteSpace(e.Modelo);
        Funcao = funcao;
        Nota = e.Nota;
        Canal = canal;
        Editar = new Comando(() => tela.AbrirEditar(Id));
    }

    /// <summary>O modelo é "genérico" (a tela o mostra apagado).</summary>
    public bool Generico { get; }

    /// <summary>O canal mestre da saída que alimenta o equipamento (0 a 3; a barra na cor dela), ou null sem ligação.</summary>
    public int? Canal { get; }

    public string Id { get; }

    public string Nome { get; }

    /// <summary>"Alto-falante passivo · canal 2 do Módulo genérico · CH2".</summary>
    public string Detalhe { get; }

    /// <summary>O modelo, ou "genérico".</summary>
    public string Modelo { get; }

    /// <summary>A função acústica que vale ("médio-grave"): a escolhida, ou a sugerida; null para quem não toca (grupos 2 a 4).</summary>
    public string? Funcao { get; }

    /// <summary>A nota, ou null.</summary>
    public string? Nota { get; }

    /// <summary>Abre a folha do equipamento.</summary>
    public Comando Editar { get; }
}

/// <summary>Uma entrada de linha (grupos 1, 3 e 4) ou um canal do módulo (grupo 2) na folha, com a fonte escolhida.</summary>
public sealed class EntradaViewModel : TelaBase
{
    private readonly EquipamentosViewModel _tela;
    private IReadOnlyList<OpcaoFonte> _opcoes;
    private OpcaoFonte _fonte;

    internal EntradaViewModel(int numero, string rotulo, IReadOnlyList<OpcaoFonte> opcoes, FonteSinal? fonte,
        EquipamentosViewModel tela, IDespachante despachante, TimeProvider relogio)
        : base(despachante, relogio)
    {
        _tela = tela;
        Numero = numero;
        Rotulo = rotulo;
        _opcoes = opcoes;
        _fonte = Achar(opcoes, fonte);
    }

    public int Numero { get; }

    /// <summary>"Entrada 1" ou "Canal 1 do módulo".</summary>
    public string Rotulo { get; }

    /// <summary>As fontes possíveis: "Sem ligação", CH1 a CH4 e as saídas dos equipamentos do grupo 3.</summary>
    public IReadOnlyList<OpcaoFonte> Opcoes => _opcoes;

    /// <summary>A fonte escolhida.</summary>
    public OpcaoFonte Fonte
    {
        get => _fonte;
        set
        {
            if (_fonte == value)
                return;
            _fonte = value;
            Avisar();
            Avisar(nameof(FontesTela));
            _tela.AoMudarLigacao();
        }
    }

    /// <summary>As fontes como pílulas, a escolhida em branco.</summary>
    public IReadOnlyList<EscolhaViewModel> FontesTela =>
        _opcoes.Select(o => new EscolhaViewModel(o.Rotulo, null, o == _fonte, () => Fonte = o)).ToArray();

    internal void TrocarOpcoes(IReadOnlyList<OpcaoFonte> opcoes)
    {
        _opcoes = opcoes;
        _fonte = Achar(opcoes, _fonte.Fonte);
        Avisar(nameof(Opcoes));
        Avisar(nameof(Fonte));
        Avisar(nameof(FontesTela));
    }

    private static OpcaoFonte Achar(IReadOnlyList<OpcaoFonte> opcoes, FonteSinal? fonte) =>
        opcoes.FirstOrDefault(o => o.Fonte == fonte) ?? opcoes[0];
}

/// <summary>Um bridge possível do módulo na folha ("Bridge 3+4"), ligado ou não.</summary>
public sealed class BridgeViewModel : TelaBase
{
    private readonly EquipamentosViewModel _tela;
    private bool _ligado;

    internal BridgeViewModel(Bridge bridge, bool ligado, EquipamentosViewModel tela, IDespachante despachante, TimeProvider relogio)
        : base(despachante, relogio)
    {
        _tela = tela;
        Bridge = bridge;
        _ligado = ligado;
        Alternar = new Comando(() => Ligado = !Ligado);
    }

    /// <summary>Tocar na pílula do bridge liga ou desliga.</summary>
    public Comando Alternar { get; }

    public Bridge Bridge { get; }

    /// <summary>"Bridge 3+4".</summary>
    public string Rotulo => $"Bridge {Bridge.Rotulo}";

    public bool Ligado
    {
        get => _ligado;
        set
        {
            if (_ligado == value)
                return;
            _ligado = value;
            Avisar();
            _tela.AoMudarLigacao();
        }
    }
}

/// <summary>Uma via de dentro de uma caixa ativa na folha: a descrição e o diâmetro (texto, em polegadas).</summary>
public sealed class ViaViewModel : TelaBase
{
    private string _descricao;
    private string _diametro;

    internal ViaViewModel(string descricao, string diametro, IDespachante despachante, TimeProvider relogio)
        : base(despachante, relogio)
    {
        _descricao = descricao;
        _diametro = diametro;
    }

    public string Descricao
    {
        get => _descricao;
        set
        {
            if (_descricao == value)
                return;
            _descricao = value;
            Avisar();
        }
    }

    public string Diametro
    {
        get => _diametro;
        set
        {
            if (_diametro == value)
                return;
            _diametro = value;
            Avisar();
        }
    }
}

/// <summary>
/// Os equipamentos de um Setup (história 8e; a tela Setup é da história 10): a lista (nome, detalhe
/// como "Alto-falante passivo · canal 2 do Módulo genérico · CH2", o modelo ou "genérico" e a função),
/// as saídas do app que eles dão, e a folha de equipamento: grupo, categoria (com as sugestões do
/// grupo e "Outra…"), nome, modelo (que se completa pela base de modelos, história 10c), função (com a
/// sugerida), a ficha por grupo com o selo de origem de cada dado (<see cref="DadoViewModel"/>), onde está
/// ligado (só as opções que existem no Setup), ✓, Cancelar e Tirar com confirmação e os avisos. Nada vai
/// pelo BLE e nada tem chave.
/// </summary>
public sealed class EquipamentosViewModel : TelaBase, IDisposable
{
    public const string TextoVazioSetup = "Adicione os equipamentos ligados em cada canal.";
    public const string TextoAdicionar = "Adicionar equipamento";
    public const string TextoOutra = "Outra…";
    public const string TextoSemLigacao = "Sem ligação";
    public const string TextoSemModulo = "Adicione um módulo para ligar o alto-falante.";
    public const string TextoCancelar = "Cancelar";
    public const string TextoTirar = "Tirar";
    public const string MsgSemSetup = "Escolha um Setup para ver os equipamentos.";
    public const string MsgSetupMudou = "O Setup mudou com a folha aberta. Feche a folha e abra de novo.";

    /// <summary>O módulo vai de 1 a 16 canais.</summary>
    public const int MaximoCanais = 16;

    public static string MsgNumero(string rotulo) => $"{rotulo}: escreva um número maior que zero.";

    /// <summary>"Fs: a fonte vai até 120 caracteres, sem quebra de linha." (longa ou com um caractere de controle).</summary>
    public static string MsgFonte(string rotulo) => $"{rotulo}: a fonte vai até {Ligacoes.TamanhoMaximoTextoLongo} caracteres, sem quebra de linha.";

    /// <summary>Com 2 letras ou mais no Modelo, a base de modelos sugere até 5.</summary>
    public const int MinimoSugestao = 2;
    public const int MaximoSugestoes = 5;

    public const string SecaoGerais = "GERAIS";
    public const string SecaoAltoFalante = "ALTO-FALANTE";
    public const string SecaoCaixa = "CAIXA";
    public const string SecaoModulo = "MÓDULO";
    public const string SecaoAlimentacao = "ALIMENTAÇÃO";
    public const string TituloDados = "DADOS TÉCNICOS";
    public const string LinhaDados = "Todos opcionais. Toque no selo para dizer de onde veio.";
    public const string CampoTipoCaixa = "tipoCaixa";
    public const string CampoCanais = "canais";

    /// <summary>
    /// As linhas da ficha, na ordem da folha: a chave do campo (a do <c>setups.json</c>), o rótulo das
    /// mensagens e a unidade. O tipo da caixa é texto; os canais vêm do "Onde está ligado".
    /// </summary>
    private static readonly (string Nome, string Rotulo, string? Unidade)[] Linhas =
    [
        ("diametro", "Diâmetro", "pol."),
        ("impedancia", "Impedância", "Ω"),
        ("potenciaRms", "Potência RMS", "W"),
        ("sensibilidade", "Sensibilidade", "dB"),
        ("faixaMinima", "Faixa, início", "Hz"),
        ("faixaMaxima", "Faixa, fim", "Hz"),
        ("re", "Re", "Ω"),
        ("fs", "Fs", "Hz"),
        ("qes", "Qes", null),
        ("qms", "Qms", null),
        ("vas", "Vas", "L"),
        ("bl", "Bl", "T·m"),
        ("sd", "Sd", "cm²"),
        ("xmax", "Xmax", "mm"),
        ("le", "Le", "mH"),
        (CampoTipoCaixa, "Tipo da caixa", null),
        ("volumeCaixa", "Volume da caixa", "L"),
        ("fb", "Fb", "Hz"),
        ("areaDuto", "Área do duto", "cm²"),
        (CampoCanais, "Canais", null),
        ("ganho", "Ganho por canal", "×"),
        ("ganhoPonte", "Ganho em ponte", "×"),
        ("potenciaCanal", "Potência por canal", "W"),
        ("tensaoCeifamentoCanal", "Ceifamento por canal", "V"),
        ("potenciaPonte", "Potência em ponte", "W"),
        ("tensaoCeifamentoPonte", "Ceifamento em ponte", "V"),
        ("tensaoAlimentacao", "Tensão da alimentação", "V"),
        ("correnteAlimentacao", "Corrente da alimentação", "A"),
    ];

    /// <summary>Os blocos da ficha que valem para quem toca (alto-falante passivo e grupo 1).</summary>
    private static readonly (string Titulo, string[] Campos)[] SecoesQueTocam =
    [
        (SecaoGerais, ["diametro", "impedancia", "potenciaRms", "sensibilidade", "faixaMinima", "faixaMaxima"]),
        (SecaoAltoFalante, ["re", "fs", "qes", "qms", "vas", "bl", "sd", "xmax", "le"]),
        (SecaoCaixa, [CampoTipoCaixa, "volumeCaixa", "fb", "areaDuto"]),
    ];

    /// <summary>Os blocos da ficha do módulo (grupo 2).</summary>
    private static readonly (string Titulo, string[] Campos)[] SecoesModulo =
    [
        (SecaoModulo, [CampoCanais, "ganho", "ganhoPonte", "potenciaCanal", "tensaoCeifamentoCanal", "potenciaPonte", "tensaoCeifamentoPonte"]),
        (SecaoAlimentacao, ["tensaoAlimentacao", "correnteAlimentacao"]),
    ];

    /// <summary>Os grupos 3 e 4: os dados de antes da ficha de 29/10.</summary>
    private static readonly (string Titulo, string[] Campos)[] SecoesRepasse =
    [
        (SecaoGerais, ["potenciaRms"]),
    ];

    private static (string Titulo, string[] Campos)[] SecoesDo(GrupoEquipamento grupo) => grupo switch
    {
        GrupoEquipamento.AltoFalante or GrupoEquipamento.AmplificaETocam => SecoesQueTocam,
        GrupoEquipamento.Amplificador => SecoesModulo,
        _ => SecoesRepasse,
    };

    /// <summary>As chaves dos campos que valem para o grupo.</summary>
    public static IReadOnlyList<string> CamposDo(GrupoEquipamento grupo) => SecoesDo(grupo).SelectMany(s => s.Campos).ToArray();

    /// <summary>"Tirar JBL R do Casa 2.1?"</summary>
    public static string MsgConfirmarTirar(string nome, string setup) => $"Tirar {nome} do {setup}?";

    /// <summary>"Os alto-falantes ligados nele ficam sem ligação: JBL L, JBL R e Sub."</summary>
    public static string MsgAvisoSemLigacao(IReadOnlyList<string> nomes) =>
        $"Os alto-falantes ligados nele ficam sem ligação: {Ligacoes.Lista(nomes)}.";

    /// <summary>"Ficam sem fonte: Módulo A e Módulo B."</summary>
    public static string MsgAvisoSemFonte(IReadOnlyList<string> nomes) => $"Ficam sem fonte: {Ligacoes.Lista(nomes)}.";

    private const string IdRascunho = "rascunho";

    private readonly GerenteSetups _setups;

    private string? _idSetup;
    private IReadOnlyList<ItemEquipamentoViewModel> _itens = [];
    private IReadOnlyList<SaidaApp> _saidas = [];
    private string? _mensagem;
    private bool _agindo;

    // A folha.
    private bool _folhaAberta;
    private string? _idFolha;
    private Equipamento? _editando;
    private GrupoEquipamento _grupo = GrupoEquipamento.AltoFalante;
    private string _categoria = "";
    private bool _categoriaLivre;
    private string _nome = "";
    private string? _nomeSugerido;
    private string _modelo = "";
    private FuncaoAcustica? _funcao;
    private bool _modeloDigitado;
    private IReadOnlyList<EscolhaViewModel> _sugestoes = [];
    private readonly IReadOnlyList<DadoViewModel> _dados;
    private readonly Dictionary<string, DadoViewModel> _porNome;
    private IReadOnlyList<SecaoDadosViewModel> _secoes = [];
    private IReadOnlyList<ViaViewModel> _vias = [];
    private IReadOnlyList<EntradaViewModel> _entradas = [];
    private IReadOnlyList<BridgeViewModel> _bridges = [];
    private IReadOnlyList<OpcaoLigado> _opcoesLigado = [];
    private OpcaoLigado? _ligado;
    private string? _erro;
    private bool _tirarAberto;
    private bool _dadosAbertos;

    public EquipamentosViewModel(GerenteSetups setups, IDespachante despachante, TimeProvider relogio)
        : base(despachante, relogio)
    {
        _setups = setups;
        _dados = Linhas
            .Select(l => new DadoViewModel(l.Nome, l.Rotulo, l.Unidade, numerico: l.Nome != CampoTipoCaixa, soLeitura: l.Nome == CampoCanais,
                this, despachante, relogio))
            .ToArray();
        _porNome = _dados.ToDictionary(d => d.Nome, StringComparer.Ordinal);
        _secoes = Secoes();
        Adicionar = new Comando(AbrirAdicionar);
        Confirmar = new Comando(ConfirmarAsync);
        Cancelar = new Comando(FecharFolha);
        Tirar = new Comando(AbrirTirar);
        AlternarDados = new Comando(() => DadosAbertos = !DadosAbertos);
        ConfirmarTirar = new Comando(ConfirmarTirarAsync);
        CancelarTirar = new Comando(() =>
        {
            _tirarAberto = false;
            Avisar(nameof(TirarAberto));
            Avisar(nameof(ConfirmacaoTirar));
            Avisar(nameof(AvisoTirar));
        });
        AdicionarEntrada = new Comando(() => MudarEntradas(_entradas.Count + 1));
        TirarEntrada = new Comando(() => MudarEntradas(Math.Max(1, _entradas.Count - 1)));
        AdicionarVia = new Comando(() =>
        {
            Vias = [.. _vias, new ViaViewModel("", "", Despachante, Relogio)];
        });
        TirarVia = new Comando(() =>
        {
            if (_vias.Count > 0)
                Vias = _vias.Take(_vias.Count - 1).ToArray();
        });

        _setups.Mudou += AoMudar;
        Atualizar();
    }

    public void Dispose() => _setups.Mudou -= AoMudar;

    // ------------------------------------------------------------------ a lista

    /// <summary>Mostra os equipamentos do Setup (null = o Setup em uso).</summary>
    public void Mostrar(string? idSetup)
    {
        _idSetup = idSetup;
        FecharFolha();
        Mensagem = null;
        Atualizar();
    }

    /// <summary>O Setup mostrado: o escolhido em <see cref="Mostrar"/>, ou o Setup em uso.</summary>
    public Setup? Setup => _idSetup is { } id ? _setups.Setup(id) : _setups.SetupEmUso;

    /// <summary>O nome do Setup ("Casa 2.1"), ou null.</summary>
    public string? Titulo => Setup?.Nome;

    /// <summary>Os equipamentos, na ordem de criação.</summary>
    public IReadOnlyList<ItemEquipamentoViewModel> Itens => _itens;

    /// <summary>O item pelo nome (sem distinguir maiúsculas), ou null.</summary>
    public ItemEquipamentoViewModel? Item(string nome) =>
        _itens.FirstOrDefault(i => string.Equals(i.Nome, nome, StringComparison.OrdinalIgnoreCase));

    /// <summary>As saídas do app que este Setup dá (com o link dele).</summary>
    public IReadOnlyList<SaidaApp> Saidas => _saidas;

    /// <summary>As saídas com os nomes da tela (a cor do canal mestre), para a tela Setup.</summary>
    public IReadOnlyList<SaidaTela> SaidasTela => SaidaTela.De(_saidas);

    /// <summary>
    /// O ambiente do Setup em pílulas (Sala, Carro, Ao ar livre, Outro), o escolhido em branco. Tocar
    /// grava (tocar no escolhido deixa sem informar). Sem Setup, nenhuma.
    /// </summary>
    public IReadOnlyList<EscolhaViewModel> AmbientesTela => Setup is { } setup
        ? Ambientes.Todos.Select(a => new EscolhaViewModel(Ambientes.Rotulo(a), null, setup.Ambiente == a,
            () => EscolherAmbienteAsync(a)) { Descricao = SetupViewModel.DescricaoAmbiente(a, setup.Ambiente == a) }).ToArray()
        : [];

    /// <summary>
    /// Toca no ambiente: grava no Setup mostrado (o escolhido de novo fica sem informar), relê e mostra
    /// a frase ("Casa: Ao ar livre."). Se não gravar, a mensagem diz e o ambiente fica como estava. Um
    /// toque com outra operação andando não faz nada.
    /// </summary>
    public async Task EscolherAmbienteAsync(AmbienteSetup ambiente)
    {
        if (Setup is not { } setup || Ocupado)
            return;
        _agindo = true;
        Avisar(nameof(Ocupado));
        try
        {
            var r = await _setups.DefinirAmbienteAsync(setup.Id, setup.Ambiente == ambiente ? null : ambiente);
            Mensagem = r.Mensagem ?? r.Motivo;
            Atualizar();
        }
        finally
        {
            _agindo = false;
            Avisar(nameof(Ocupado));
        }
    }

    /// <summary>"Adicione os equipamentos ligados em cada canal." quando o Setup não tem nenhum; sem Setup, o pedido para escolher um.</summary>
    public string? TextoVazio => Setup is null ? MsgSemSetup : _itens.Count == 0 ? TextoVazioSetup : null;

    /// <summary>A última frase ("JBL R guardado.", "Módulo tirado do Casa 2.1. Ficaram sem ligação: …").</summary>
    public string? Mensagem
    {
        get => _mensagem;
        private set
        {
            if (_mensagem == value)
                return;
            _mensagem = value;
            Avisar();
        }
    }

    /// <summary>Uma operação anda.</summary>
    public bool Ocupado => _agindo || _setups.Ocupado;

    /// <summary>Abre a folha para um equipamento novo.</summary>
    public Comando Adicionar { get; }

    // ------------------------------------------------------------------ a folha

    public bool FolhaAberta => _folhaAberta;

    /// <summary>A folha é de um equipamento novo (sem Tirar).</summary>
    public bool Nova => _folhaAberta && _editando is null;

    /// <summary>"Adicionar equipamento", ou o nome do equipamento editado.</summary>
    public string? TituloFolha => !_folhaAberta ? null : _editando?.Nome ?? TextoAdicionar;

    /// <summary>Os grupos, na ordem: os quatro com entrada de linha e o alto-falante passivo.</summary>
    public IReadOnlyList<OpcaoGrupo> Grupos { get; } =
        Ligacoes.Grupos.Select(g => new OpcaoGrupo(g, Ligacoes.RotuloGrupo(g), Ligacoes.ExemplosGrupo(g))).ToArray();

    /// <summary>O grupo escolhido. Trocar o grupo refaz a categoria e as ligações.</summary>
    public GrupoEquipamento Grupo
    {
        get => _grupo;
        set
        {
            if (_grupo == value)
                return;
            _grupo = value;
            if (!_categoriaLivre && !Ligacoes.CategoriasSugeridas(value).Contains(_categoria))
                _categoria = "";
            PrepararLigacoes(null);
            SincronizarCanais();
            _secoes = Secoes();
            Avisar();
            Avisar(nameof(Categorias));
            Avisar(nameof(Categoria));
            AvisarGrupo();
            AvisarOrigens();
            AvisarSugestoes();
            AoMudarLigacao();
        }
    }

    /// <summary>As categorias sugeridas do grupo e, por último, "Outra…" (escrever).</summary>
    public IReadOnlyList<string> Categorias => [.. Ligacoes.CategoriasSugeridas(_grupo), TextoOutra];

    /// <summary>Os grupos em linhas de escolha (o rótulo e os exemplos), o escolhido em branco.</summary>
    public IReadOnlyList<EscolhaViewModel> GruposTela =>
        Grupos.Select(g => new EscolhaViewModel(g.Rotulo, g.Exemplos, g.Grupo == _grupo, () => Grupo = g.Grupo)).ToArray();

    /// <summary>As categorias como pílulas: a escolhida em branco; "Outra…" em branco com o campo aberto.</summary>
    public IReadOnlyList<EscolhaViewModel> CategoriasTela =>
        Categorias.Select(c => new EscolhaViewModel(c, null, c == TextoOutra ? _categoriaLivre : !_categoriaLivre && c == _categoria,
            () => EscolherCategoria(c))).ToArray();

    /// <summary>As funções como pílulas (a sugerida primeiro), a que vale em branco.</summary>
    public IReadOnlyList<EscolhaViewModel> FuncoesTela =>
        Funcoes.Select(f => new EscolhaViewModel(f.Rotulo, null, f.Funcao == _funcao, () => Funcao = f.Funcao)).ToArray();

    /// <summary>Alto-falante passivo: os lugares como pílulas, o escolhido em branco.</summary>
    public IReadOnlyList<EscolhaViewModel> LigadoTela =>
        _opcoesLigado.Select(o => new EscolhaViewModel(o.Rotulo, null, o == _ligado, () => Ligado = o)).ToArray();

    /// <summary>Os dados técnicos (Avançado da folha) estão abertos.</summary>
    public bool DadosAbertos
    {
        get => _dadosAbertos;
        private set
        {
            if (_dadosAbertos == value)
                return;
            _dadosAbertos = value;
            Avisar();
            Avisar(nameof(IndicadorDados));
        }
    }

    public string IndicadorDados => _dadosAbertos ? "⌃" : "⌄";

    /// <summary>Abre e fecha os dados técnicos (vêm recolhidos).</summary>
    public Comando AlternarDados { get; }

    /// <summary>"DADOS TÉCNICOS".</summary>
    public string TituloDadosTela => TituloDados;

    /// <summary>"Todos opcionais. Toque no selo para dizer de onde veio."</summary>
    public string LinhaDadosTela => LinhaDados;

    /// <summary>
    /// A ficha em blocos, só com os dados que valem para o grupo: quem toca (alto-falante passivo e grupo
    /// 1) tem os gerais, os do alto-falante (Re, Fs, Qes…) e os da caixa; o módulo (grupo 2), os dele e os
    /// da alimentação; os grupos 3 e 4, a potência RMS.
    /// </summary>
    public IReadOnlyList<SecaoDadosViewModel> SecoesDados => _secoes;

    /// <summary>Todas as linhas da ficha, as que valem para o grupo e as outras.</summary>
    public IReadOnlyList<DadoViewModel> Dados => _dados;

    /// <summary>A linha de um campo pela chave ("fs", "tipoCaixa"), ou null.</summary>
    public DadoViewModel? Dado(string nome) => _porNome.GetValueOrDefault(nome);

    /// <summary>
    /// A origem carregada do equipamento editado ainda vale: o mesmo grupo e o mesmo modelo. Com o
    /// modelo ou o grupo trocados, os dados que ficam passam a digitados.
    /// </summary>
    internal bool OrigemDoEditadoVale =>
        _editando is not null && _editando.Grupo == _grupo && BaseModelos.Chave(_editando.Modelo) == BaseModelos.Chave(_modelo);

    /// <summary>Abre a escolha da origem de uma linha e fecha a que estava aberta (uma por vez).</summary>
    internal void AbrirOrigem(DadoViewModel dado)
    {
        foreach (var d in _dados)
        {
            if (!ReferenceEquals(d, dado))
                d.FecharOrigem();
        }
        dado.AbrirOrigemInterno();
    }

    private IReadOnlyList<SecaoDadosViewModel> Secoes() =>
        SecoesDo(_grupo).Select(s => new SecaoDadosViewModel(s.Titulo, s.Campos.Select(c => _porNome[c]).ToArray())).ToArray();

    private void AvisarOrigens()
    {
        foreach (var d in _dados)
            d.AvisarOrigem();
    }

    /// <summary>A linha dos canais (só leitura) segue o número de canais do módulo.</summary>
    private void SincronizarCanais() =>
        _porNome[CampoCanais].DefinirTexto(_grupo == GrupoEquipamento.Amplificador ? _entradas.Count.ToString(CultureInfo.InvariantCulture) : "");

    /// <summary>Escolhe uma categoria da lista; "Outra…" abre o campo para escrever. O nome é sugerido pela categoria.</summary>
    public void EscolherCategoria(string categoria)
    {
        if (categoria == TextoOutra)
        {
            _categoriaLivre = true;
            _categoria = "";
        }
        else
        {
            _categoriaLivre = false;
            _categoria = categoria;
            SugerirNome();
        }
        Avisar(nameof(CategoriaLivre));
        Avisar(nameof(Categoria));
        Avisar(nameof(CategoriasTela));
        AtualizarSugestao();
    }

    /// <summary>O campo da categoria escrita está aberto ("Outra…").</summary>
    public bool CategoriaLivre => _categoriaLivre;

    /// <summary>A categoria (escolhida ou escrita).</summary>
    public string Categoria
    {
        get => _categoria;
        set
        {
            if (_categoria == value)
                return;
            _categoria = value;
            Avisar();
            Avisar(nameof(CategoriasTela));
            AtualizarSugestao();
        }
    }

    public string Nome
    {
        get => _nome;
        set
        {
            if (_nome == value)
                return;
            _nome = value;
            Avisar();
        }
    }

    /// <summary>O modelo; vazio = genérico. Escrever nele abre as sugestões da base de modelos.</summary>
    public string Modelo
    {
        get => _modelo;
        set
        {
            if (_modelo == value)
                return;
            _modelo = value;
            _modeloDigitado = true;
            Avisar();
            AvisarSugestoes();
            AvisarOrigens();
        }
    }

    /// <summary>A dica do campo do modelo vazio.</summary>
    public string DicaModelo => Ligacoes.TextoGenerico;

    /// <summary>
    /// Com 2 letras ou mais escritas no Modelo, até 5 modelos da base do mesmo grupo da folha que começam
    /// com o texto (o nome e a categoria); tocar num deles preenche a ficha. Sem escrever (ao abrir a
    /// folha), nenhum. Calculadas uma vez por mudança do Modelo (ou do grupo).
    /// </summary>
    public IReadOnlyList<EscolhaViewModel> SugestoesModelo => _sugestoes;

    /// <summary>Há sugestões para mostrar embaixo do Modelo.</summary>
    public bool TemSugestoes => _sugestoes.Count > 0;

    /// <summary>Para o leitor de tela, numa sugestão: "Toque para preencher a ficha com este modelo."</summary>
    public const string DicaSugestao = "Toque para preencher a ficha com este modelo.";

    /// <summary>O Modelo da folha tem esta chave (a origem que veio da base vale só com o modelo dela).</summary>
    internal bool ModeloE(string? chave) => chave is not null && BaseModelos.Chave(_modelo) == chave;

    /// <summary>
    /// Toque numa sugestão: o nome vai para o Modelo e os dados que a base tem preenchem a ficha, com as
    /// origens dela; os que a base não tem ficam como estavam. A lista some. Um modelo de outro grupo não
    /// preenche nada. Num módulo que já existe, a base não tira canais (só preenche quando tem mais).
    /// </summary>
    public void EscolherModelo(string modelo)
    {
        if (!_folhaAberta)
            return;
        var ficha = _setups.Modelos?.Ficha(modelo);
        if (ficha is not null && ficha.Grupo != _grupo)
            ficha = null;
        _modelo = ficha?.Modelo ?? modelo;
        _modeloDigitado = false;
        if (ficha is not null)
        {
            var chave = BaseModelos.Chave(ficha.Modelo);
            foreach (var c in DadosTecnicos.Campos)
            {
                if (c.Ler(ficha.Dados) is not { } v)
                    continue;
                if (c.Nome == CampoCanais)
                {
                    // Os canais do módulo são as entradas dele: a base muda o número de canais, sem tirar
                    // canais (e as ligações deles) de um módulo que já existe.
                    if (_grupo != GrupoEquipamento.Amplificador || decimal.Truncate(v.Valor) != v.Valor || v.Valor > MaximoCanais
                        || (_editando is not null && v.Valor < _entradas.Count))
                        continue;
                    _porNome[CampoCanais].CarregarDaBase(v, chave);
                    MudarEntradas((int)v.Valor);
                    SincronizarCanais();
                    continue;
                }
                _porNome[c.Nome].CarregarDaBase(v, chave);
            }
            if (ficha.Dados.TipoCaixa is { } tipo)
                _porNome[CampoTipoCaixa].Carregar(tipo, doEditado: false, chave);
        }
        Avisar(nameof(Modelo));
        AvisarSugestoes();
        AvisarOrigens();
    }

    /// <summary>Refaz as sugestões (uma vez) e avisa.</summary>
    private void AvisarSugestoes()
    {
        _sugestoes = Sugestoes();
        Avisar(nameof(SugestoesModelo));
        Avisar(nameof(TemSugestoes));
    }

    private IReadOnlyList<EscolhaViewModel> Sugestoes()
    {
        if (!_folhaAberta || !_modeloDigitado || _setups.Modelos is not { } base_ || _modelo.Trim().Length < MinimoSugestao)
            return [];
        return base_.Sugerir(_modelo)
            .Select(m => base_.Ficha(m))
            .Where(f => f is not null && f.Grupo == _grupo)
            .Take(MaximoSugestoes)
            .Select(f => new EscolhaViewModel(f!.Modelo, f.Categoria, false, () => EscolherModelo(f.Modelo)))
            .ToArray();
    }

    /// <summary>
    /// A função sugerida pela categoria e pelo corte da memória ativa do Setup no canal que alimenta o
    /// rascunho; null para quem não toca (grupos 2 a 4).
    /// </summary>
    public FuncaoAcustica? FuncaoSugerida => GerenteSetups.FuncaoSugerida(Setup, _setups.Ativa(Setup?.Id), Montar(exigir: false).Equipamento!);

    /// <summary>A função sugerida de um equipamento do Setup (o esquema), pelo corte da memória ativa dele.</summary>
    internal FuncaoAcustica? FuncaoSugeridaNo(Setup setup, Equipamento equipamento) =>
        GerenteSetups.FuncaoSugerida(setup, _setups.Ativa(setup.Id), equipamento);

    /// <summary>A função acústica aparece: só para quem toca (grupo 1 e alto-falante passivo).</summary>
    public bool MostraFuncao => _grupo is GrupoEquipamento.AmplificaETocam or GrupoEquipamento.AltoFalante;

    /// <summary>As funções: a primeira é a sugerida ("Sugerida: sub"), depois as oito. Vazio para quem não toca.</summary>
    public IReadOnlyList<OpcaoFuncao> Funcoes => FuncaoSugerida is not { } sugerida
        ? []
        :
        [
            new($"Sugerida: {Ligacoes.RotuloFuncao(sugerida)}", null),
            .. Ligacoes.Funcoes.Select(f => new OpcaoFuncao(Ligacoes.RotuloFuncao(f), f)),
        ];

    /// <summary>A função escolhida; null = a sugerida. A escolha do usuário sempre vale mais.</summary>
    public FuncaoAcustica? Funcao
    {
        get => _funcao;
        set
        {
            if (_funcao == value)
                return;
            _funcao = value;
            Avisar();
            Avisar(nameof(FuncaoQueVale));
            Avisar(nameof(FuncoesTela));
        }
    }

    /// <summary>A função que vale ("sub"): a escolhida, ou a sugerida; null para quem não toca.</summary>
    public string? FuncaoQueVale => !MostraFuncao ? null : (_funcao ?? FuncaoSugerida) is { } f ? Ligacoes.RotuloFuncao(f) : null;

    /// <summary>As vias de dentro (caixa ativa).</summary>
    public IReadOnlyList<ViaViewModel> Vias
    {
        get => _vias;
        private set
        {
            _vias = value;
            Avisar();
        }
    }

    public Comando AdicionarVia { get; }

    public Comando TirarVia { get; }

    /// <summary>Grupo 1: as vias de dentro aparecem.</summary>
    public bool MostraVias => _grupo == GrupoEquipamento.AmplificaETocam;

    /// <summary>Grupos 1, 3 e 4: as entradas de linha, cada uma com a fonte.</summary>
    public bool MostraEntradas => _grupo is GrupoEquipamento.AmplificaETocam or GrupoEquipamento.Repassa or GrupoEquipamento.SoRecebe;

    /// <summary>Grupo 2: os canais do módulo (cada um com a fonte) e os bridges.</summary>
    public bool MostraCanaisModulo => _grupo == GrupoEquipamento.Amplificador;

    /// <summary>Alto-falante passivo: o "ligado em".</summary>
    public bool MostraLigado => _grupo == GrupoEquipamento.AltoFalante;

    /// <summary>As entradas (grupos 1, 3 e 4) ou os canais do módulo (grupo 2).</summary>
    public IReadOnlyList<EntradaViewModel> Entradas => _entradas;

    /// <summary>Mais uma entrada (ou mais um canal do módulo).</summary>
    public Comando AdicionarEntrada { get; }

    /// <summary>Tira a última entrada (fica pelo menos uma).</summary>
    public Comando TirarEntrada { get; }

    /// <summary>Grupo 2: quantos canais o módulo tem, de 1 a 16 (os canais e os bridges acompanham).</summary>
    public int NumeroCanais
    {
        get => _entradas.Count;
        set => MudarEntradas(value);
    }

    /// <summary>Grupo 2: os bridges possíveis (1+2, 3+4…), cada um ligado ou não.</summary>
    public IReadOnlyList<BridgeViewModel> Bridges => _bridges;

    /// <summary>Alto-falante passivo: os lugares possíveis (sem módulo no Setup, nenhum).</summary>
    public IReadOnlyList<OpcaoLigado> OpcoesLigado => _opcoesLigado;

    /// <summary>Alto-falante passivo: onde ele fica.</summary>
    public OpcaoLigado? Ligado
    {
        get => _ligado;
        set
        {
            if (_ligado == value)
                return;
            _ligado = value;
            Avisar();
            Avisar(nameof(LigadoTela));
            AoMudarLigacao();
        }
    }

    /// <summary>"Adicione um módulo para ligar o alto-falante." quando o Setup não tem módulo; senão null.</summary>
    public string? FraseSemModulo => MostraLigado && _opcoesLigado.Count == 0 ? TextoSemModulo : null;

    /// <summary>Por que o ✓ não guardou, ou null.</summary>
    public string? Erro
    {
        get => _erro;
        private set
        {
            if (_erro == value)
                return;
            _erro = value;
            Avisar();
        }
    }

    /// <summary>✓: guarda o equipamento (nada vai pelo BLE).</summary>
    public Comando Confirmar { get; }

    public Comando Cancelar { get; }

    // ------------------------------------------------------------------ tirar

    /// <summary>Abre a confirmação "Tirar JBL R do Casa 2.1?" (só ao editar).</summary>
    public Comando Tirar { get; }

    public bool TirarAberto => _tirarAberto;

    public string? ConfirmacaoTirar =>
        _tirarAberto && _editando is { } e && Setup is { } s ? MsgConfirmarTirar(e.Nome, s.Nome) : null;

    /// <summary>O que tirar desfaz: os alto-falantes de um módulo ou quem um equipamento do grupo 3 alimentava; senão null.</summary>
    public string? AvisoTirar
    {
        get
        {
            if (!_tirarAberto || _editando is not { } e || Setup is not { } s)
                return null;
            var r = Ligacoes.Tirar(s.Equipamentos, e.Id);
            var avisos = new List<string>();
            if (r.SemLigacao.Count > 0)
                avisos.Add(MsgAvisoSemLigacao(r.SemLigacao));
            if (r.SemFonte.Count > 0)
                avisos.Add(MsgAvisoSemFonte(r.SemFonte));
            return avisos.Count == 0 ? null : string.Join(" ", avisos);
        }
    }

    public Comando ConfirmarTirar { get; }

    public Comando CancelarTirar { get; }

    // ------------------------------------------------------------------ abrir e fechar

    private void AbrirAdicionar()
    {
        if (Setup is null)
        {
            Mensagem = MsgSemSetup;
            return;
        }
        _editando = null;
        _grupo = GrupoEquipamento.AltoFalante;
        _categoria = "";
        _categoriaLivre = false;
        _nome = "";
        _nomeSugerido = null;
        _modelo = "";
        _modeloDigitado = false;
        _funcao = null;
        foreach (var d in _dados)
            d.Limpar();
        _vias = [];
        PrepararLigacoes(null);
        SincronizarCanais();
        _secoes = Secoes();
        AbrirFolha();
    }

    /// <summary>Abre a folha do equipamento (pelo id).</summary>
    public void AbrirEditar(string id)
    {
        if (Setup?.Equipamentos.FirstOrDefault(e => e.Id == id) is not { } e)
        {
            Mensagem = GerenteSetups.MsgEquipamentoNaoExiste;
            return;
        }
        _editando = e;
        _grupo = e.Grupo;
        _categoria = e.Categoria;
        _categoriaLivre = !Ligacoes.CategoriasSugeridas(e.Grupo).Contains(e.Categoria);
        _nome = e.Nome;
        _nomeSugerido = null;
        _modelo = e.Modelo ?? "";
        _modeloDigitado = false;
        _funcao = e.Funcao;
        // Cada dado com o valor, a origem e a fonte que o núcleo guarda.
        foreach (var c in DadosTecnicos.Campos)
            _porNome[c.Nome].Carregar(c.Ler(e.Dados));
        _porNome[CampoTipoCaixa].Carregar(e.Dados.TipoCaixa, doEditado: true, chave: null);
        _vias = e.Dados.Vias.Select(v => new ViaViewModel(v.Descricao, v.Diametro is { } d ? DadoViewModel.TextoNumero(d) : "", Despachante, Relogio)).ToArray();
        PrepararLigacoes(e);
        SincronizarCanais();
        _secoes = Secoes();
        AbrirFolha();
    }

    private void AbrirFolha()
    {
        _idFolha = Setup?.Id;
        _folhaAberta = true;
        _tirarAberto = false;
        _erro = null;
        AvisarFolha();
    }

    private void FecharFolha()
    {
        if (!_folhaAberta && !_tirarAberto)
            return;
        _folhaAberta = false;
        _tirarAberto = false;
        _editando = null;
        _erro = null;
        foreach (var d in _dados)
            d.FecharOrigem();
        AvisarFolha();
    }

    private void AbrirTirar()
    {
        if (_editando is null)
            return;
        _tirarAberto = true;
        Avisar(nameof(TirarAberto));
        Avisar(nameof(ConfirmacaoTirar));
        Avisar(nameof(AvisoTirar));
    }

    // ------------------------------------------------------------------ guardar

    private async Task ConfirmarAsync()
    {
        if (!_folhaAberta)
            return;
        if (Setup is not { } setup || setup.Id != _idFolha)
        {
            Erro = MsgSetupMudou;
            return;
        }
        var (rascunho, erro) = Montar(exigir: true);
        if (rascunho is null)
        {
            Erro = erro;
            return;
        }
        _agindo = true;
        Avisar(nameof(Ocupado));
        try
        {
            var r = _editando is null
                ? await _setups.AdicionarEquipamentoAsync(setup.Id, rascunho)
                : await _setups.EditarEquipamentoAsync(setup.Id, rascunho);
            if (!r.Ok)
            {
                Erro = r.Mensagem ?? r.Motivo;
                return;
            }
            FecharFolha();
            Mensagem = r.Mensagem;
            Atualizar();
        }
        finally
        {
            _agindo = false;
            Avisar(nameof(Ocupado));
        }
    }

    private async Task ConfirmarTirarAsync()
    {
        if (!_tirarAberto || _editando is not { } e)
            return;
        if (Setup is not { } setup || setup.Id != _idFolha)
        {
            FecharTirarComErro(MsgSetupMudou);
            return;
        }
        _agindo = true;
        Avisar(nameof(Ocupado));
        try
        {
            var r = await _setups.TirarEquipamentoAsync(setup.Id, e.Id);
            if (!r.Ok)
            {
                FecharTirarComErro(r.Mensagem ?? r.Motivo);
                return;
            }
            FecharFolha();
            Mensagem = r.Mensagem;
            Atualizar();
        }
        finally
        {
            _agindo = false;
            Avisar(nameof(Ocupado));
        }
    }

    /// <summary>A confirmação do Tirar fecha (o texto e o aviso também) e a folha mostra o erro.</summary>
    private void FecharTirarComErro(string? erro)
    {
        _tirarAberto = false;
        Erro = erro;
        Avisar(nameof(TirarAberto));
        Avisar(nameof(ConfirmacaoTirar));
        Avisar(nameof(AvisoTirar));
    }

    /// <summary>
    /// O equipamento da folha. Com <paramref name="exigir"/>, um dado técnico que não é número maior
    /// que zero (ou com a fonte de mais de 120 caracteres) recusa com o motivo; sem, ele fica de fora
    /// (para a função sugerida). Cada linha que vale para o grupo vira um <see cref="DadoTecnico"/> com a
    /// origem e a fonte do selo (um dado que não mudou fica com a origem e a fonte que tinha); os dados
    /// que não valem para o grupo ficam como estavam. Com o modelo ou o grupo trocados, a origem de antes
    /// não vale para o equipamento novo: os dados que ficam passam a digitados (menos os que vieram da
    /// base de modelos ou os de origem escolhida no selo).
    /// </summary>
    private (Equipamento? Equipamento, string? Erro) Montar(bool exigir)
    {
        string? erro = null;
        decimal? Numero(string texto, string rotulo)
        {
            if (string.IsNullOrWhiteSpace(texto))
                return null;
            if (DadoViewModel.Numero(texto) is { } v)
                return v;
            erro ??= MsgNumero(rotulo);
            return null;
        }

        var antes = _editando is null ? DadosTecnicos.Nenhum : OrigemDoEditadoVale ? _editando.Dados : _editando.Dados.ComoDigitados();
        var valem = CamposDo(_grupo).ToHashSet(StringComparer.Ordinal);
        var dados = antes;
        foreach (var c in DadosTecnicos.Campos)
        {
            if (!valem.Contains(c.Nome))
                continue;
            var (dado, erroDado) = _porNome[c.Nome].MontarNumero();
            erro ??= erroDado;
            dados = c.Com(dados, dado);
        }
        if (valem.Contains(CampoTipoCaixa))
        {
            var (tipo, erroTipo) = _porNome[CampoTipoCaixa].MontarTexto();
            erro ??= erroTipo;
            dados = dados with { TipoCaixa = tipo };
        }
        // Os canais (e os outros dados) de um grupo que não os mostra ficam como estavam (vêm de antes).
        dados = dados with
        {
            Vias = MostraVias
                ? _vias.Where(v => !string.IsNullOrWhiteSpace(v.Descricao)).Select(v => new Via(v.Descricao, Numero(v.Diametro, "Diâmetro da via"))).ToArray()
                : [],
        };
        var e = new Equipamento(_editando?.Id ?? IdRascunho, _grupo, _categoria, _nome, string.IsNullOrWhiteSpace(_modelo) ? null : _modelo,
            MostraFuncao ? _funcao : null)
        {
            Dados = dados,
            Entradas = _grupo == GrupoEquipamento.AltoFalante ? [] : _entradas.Select(x => new Entrada(x.Numero, x.Fonte.Fonte)).ToArray(),
            Bridges = _grupo == GrupoEquipamento.Amplificador ? _bridges.Where(b => b.Ligado).Select(b => b.Bridge).ToArray() : [],
            Ligado = _grupo == GrupoEquipamento.AltoFalante ? _ligado?.Ligado : null,
            Nota = _editando?.Nota,
        };
        if (exigir && erro is not null)
            return (null, erro);
        return (e, null);
    }

    // ------------------------------------------------------------------ ligações na folha

    /// <summary>Monta as entradas, os bridges e o "ligado em" para o grupo (com os valores do equipamento editado).</summary>
    private void PrepararLigacoes(Equipamento? e)
    {
        var fontes = OpcoesFonte();
        var entradas = e is not null && e.Grupo == _grupo ? e.Entradas : null;
        if (_grupo == GrupoEquipamento.AltoFalante)
        {
            _entradas = [];
        }
        else
        {
            var n = entradas?.Count ?? (_grupo == GrupoEquipamento.Amplificador ? 2 : 1);
            _entradas = Enumerable.Range(1, Math.Max(1, n))
                .Select(i => NovaEntrada(i, fontes, entradas?.FirstOrDefault(x => x.Numero == i)?.Fonte))
                .ToArray();
        }
        var bridges = e is not null && e.Grupo == GrupoEquipamento.Amplificador ? e.Bridges : [];
        _bridges = _grupo == GrupoEquipamento.Amplificador ? NovosBridges(_entradas.Count, bridges) : [];
        _opcoesLigado = OpcoesDeLigado();
        _ligado = _grupo == GrupoEquipamento.AltoFalante
            ? _opcoesLigado.FirstOrDefault(o => o.Ligado == e?.Ligado) ?? _opcoesLigado.FirstOrDefault()
            : null;
    }

    private EntradaViewModel NovaEntrada(int numero, IReadOnlyList<OpcaoFonte> fontes, FonteSinal? fonte) =>
        new(numero, _grupo == GrupoEquipamento.Amplificador ? $"Canal {numero} do módulo" : $"Entrada {numero}", fontes, fonte, this, Despachante, Relogio);

    private IReadOnlyList<BridgeViewModel> NovosBridges(int canais, IReadOnlyList<Bridge> ligados) =>
        Enumerable.Range(0, canais / 2)
            .Select(i => new Bridge(2 * i + 1, 2 * i + 2))
            .Select(b => new BridgeViewModel(b, ligados.Contains(b), this, Despachante, Relogio))
            .ToArray();

    private void MudarEntradas(int quantas)
    {
        quantas = Math.Clamp(quantas, 1, MaximoCanais);
        if (!_folhaAberta || _grupo == GrupoEquipamento.AltoFalante || quantas == _entradas.Count)
            return;
        var fontes = OpcoesFonte();
        _entradas = Enumerable.Range(1, quantas)
            .Select(i => i <= _entradas.Count ? _entradas[i - 1] : NovaEntrada(i, fontes, null))
            .ToArray();
        if (_grupo == GrupoEquipamento.Amplificador)
            _bridges = NovosBridges(_entradas.Count, _bridges.Where(b => b.Ligado).Select(b => b.Bridge).ToArray());
        SincronizarCanais();
        Avisar(nameof(Entradas));
        Avisar(nameof(NumeroCanais));
        Avisar(nameof(Bridges));
        AoMudarLigacao();
    }

    /// <summary>"Sem ligação", CH1 a CH4 e a saída de cada equipamento do grupo 3 do Setup (menos o editado).</summary>
    private IReadOnlyList<OpcaoFonte> OpcoesFonte()
    {
        var opcoes = new List<OpcaoFonte> { new(TextoSemLigacao, null) };
        for (var c = 0; c < Ligacoes.CanaisProcessador; c++)
            opcoes.Add(new OpcaoFonte($"CH{c + 1}", FonteSinal.DoCanal(c)));
        foreach (var e in Setup?.Equipamentos ?? [])
        {
            if (e.Grupo == GrupoEquipamento.Repassa && e.Id != _editando?.Id)
                opcoes.Add(new OpcaoFonte($"Saída do {e.Nome}", FonteSinal.DoEquipamento(e.Id)));
        }
        return opcoes;
    }

    /// <summary>
    /// Os lugares de um alto-falante passivo: cada canal e cada bridge de cada módulo do Setup, depois
    /// de "Sem ligação". Sem módulo, nenhum.
    /// </summary>
    private IReadOnlyList<OpcaoLigado> OpcoesDeLigado()
    {
        var modulos = (Setup?.Equipamentos ?? []).Where(e => e.Grupo == GrupoEquipamento.Amplificador && e.Id != _editando?.Id).ToArray();
        if (modulos.Length == 0)
            return [];
        var opcoes = new List<OpcaoLigado> { new(TextoSemLigacao, null) };
        foreach (var m in modulos)
        {
            foreach (var x in m.Entradas)
                opcoes.Add(new OpcaoLigado($"canal {x.Numero} do {m.Nome}", LigacaoAltoFalante.NoCanal(m.Id, x.Numero)));
            foreach (var b in m.Bridges)
                opcoes.Add(new OpcaoLigado($"bridge {b.Rotulo} do {m.Nome}", LigacaoAltoFalante.NoBridge(m.Id, b)));
        }
        return opcoes;
    }

    /// <summary>Uma ligação mudou na folha: a função sugerida acompanha.</summary>
    internal void AoMudarLigacao() => AtualizarSugestao();

    private void AtualizarSugestao()
    {
        Avisar(nameof(FuncaoSugerida));
        Avisar(nameof(Funcoes));
        Avisar(nameof(FuncaoQueVale));
        Avisar(nameof(FuncoesTela));
    }

    /// <summary>O nome sugerido pela categoria e pela posição ("Tweeter", "Tweeter 2"), só se o campo está vazio ou com a sugestão anterior.</summary>
    private void SugerirNome()
    {
        if (_nome.Length > 0 && _nome != _nomeSugerido)
            return;
        if (string.IsNullOrWhiteSpace(_categoria))
            return;
        var base_ = char.ToUpper(_categoria[0], CultureInfo.InvariantCulture) + _categoria[1..];
        var usados = (Setup?.Equipamentos ?? []).Where(e => e.Id != _editando?.Id).Select(e => e.Nome).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var nome = base_;
        for (var i = 2; usados.Contains(nome); i++)
            nome = $"{base_} {i}";
        _nomeSugerido = nome;
        Nome = nome;
    }

    // ------------------------------------------------------------------ atualizar

    private void AoMudar() => NaTela(Atualizar);

    private void Atualizar()
    {
        var setup = Setup;
        if (setup is null)
        {
            _itens = [];
            _saidas = [];
        }
        else
        {
            _saidas = _setups.Saidas(setup.Id);
            var saidas = _saidas;
            // A cor da linha: a do canal mestre da saída que alimenta o equipamento (no Casa 2.1 com o
            // Link 3–4, um equipamento só no CH4 é do Sub, âmbar).
            int? Cor(Equipamento e) =>
                Ligacoes.CanaisQueAlimentam(setup.Equipamentos, e.Id) is { Count: > 0 } canais
                    ? saidas.FirstOrDefault(s => s.Canais.Contains(canais[0]))?.Mestre ?? canais[0]
                    : null;
            _itens = setup.Equipamentos
                .Select(e => new ItemEquipamentoViewModel(e, Detalhe(setup, e),
                    _setups.Funcao(setup.Id, e) is { } f ? Ligacoes.RotuloFuncao(f) : null, Cor(e), this))
                .ToArray();
        }
        if (_folhaAberta)
        {
            // Um equipamento entrou ou saiu: as opções de ligação acompanham, sem perder a escolha.
            var fontes = OpcoesFonte();
            foreach (var entrada in _entradas)
                entrada.TrocarOpcoes(fontes);
            _opcoesLigado = OpcoesDeLigado();
            if (_grupo == GrupoEquipamento.AltoFalante)
                _ligado = _opcoesLigado.FirstOrDefault(o => o == _ligado) ?? _opcoesLigado.FirstOrDefault(o => o.Ligado == _ligado?.Ligado) ?? _opcoesLigado.FirstOrDefault();
            Avisar(nameof(OpcoesLigado));
            Avisar(nameof(Ligado));
            Avisar(nameof(LigadoTela));
            Avisar(nameof(FraseSemModulo));
            AtualizarSugestao();
        }
        Avisar(nameof(Setup));
        Avisar(nameof(AmbientesTela));
        Avisar(nameof(SaidasTela));
        Avisar(nameof(Titulo));
        Avisar(nameof(Itens));
        Avisar(nameof(Saidas));
        Avisar(nameof(TextoVazio));
        Avisar(nameof(Ocupado));
    }

    /// <summary>"Alto-falante passivo · canal 2 do Módulo genérico · CH2"; "Amplificador · 4 canais · bridge 3+4 · CH1 + CH2 + CH3 + CH4".</summary>
    public static string Detalhe(Setup setup, Equipamento e)
    {
        var partes = new List<string> { Ligacoes.RotuloGrupo(e.Grupo) };
        if (e.Grupo == GrupoEquipamento.AltoFalante && e.Ligado is { } l)
        {
            var modulo = setup.Equipamentos.FirstOrDefault(m => m.Id == l.IdModulo)?.Nome ?? "?";
            partes.Add(l.Canal is { } c ? $"canal {c} do {modulo}" : $"bridge {l.Bridge!.Rotulo} do {modulo}");
        }
        if (e.Grupo == GrupoEquipamento.Amplificador)
        {
            partes.Add(e.Entradas.Count == 1 ? "1 canal" : $"{e.Entradas.Count} canais");
            partes.AddRange(e.Bridges.Select(b => $"bridge {b.Rotulo}"));
        }
        var canais = Ligacoes.CanaisQueAlimentam(setup.Equipamentos, e.Id);
        partes.Add(canais.Count > 0 ? Ligacoes.RotuloCanais(canais) : Ligacoes.TextoSemLigacao);
        return string.Join(" · ", partes);
    }

    private void AvisarFolha()
    {
        Avisar(nameof(FolhaAberta));
        Avisar(nameof(Nova));
        Avisar(nameof(TituloFolha));
        Avisar(nameof(Grupo));
        Avisar(nameof(Categorias));
        Avisar(nameof(CategoriaLivre));
        Avisar(nameof(Categoria));
        Avisar(nameof(CategoriasTela));
        Avisar(nameof(Nome));
        Avisar(nameof(Modelo));
        AvisarSugestoes();
        Avisar(nameof(Funcao));
        Avisar(nameof(SecoesDados));
        AvisarOrigens();
        Avisar(nameof(Vias));
        AvisarGrupo();
        Avisar(nameof(Erro));
        Avisar(nameof(TirarAberto));
        Avisar(nameof(ConfirmacaoTirar));
        Avisar(nameof(AvisoTirar));
        AtualizarSugestao();
    }

    private void AvisarGrupo()
    {
        Avisar(nameof(GruposTela));
        Avisar(nameof(CategoriasTela));
        Avisar(nameof(LigadoTela));
        Avisar(nameof(SecoesDados));
        Avisar(nameof(MostraFuncao));
        Avisar(nameof(MostraVias));
        Avisar(nameof(MostraEntradas));
        Avisar(nameof(MostraCanaisModulo));
        Avisar(nameof(MostraLigado));
        Avisar(nameof(Entradas));
        Avisar(nameof(NumeroCanais));
        Avisar(nameof(Bridges));
        Avisar(nameof(OpcoesLigado));
        Avisar(nameof(Ligado));
        Avisar(nameof(FraseSemModulo));
    }

}
