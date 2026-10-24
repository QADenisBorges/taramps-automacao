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
    {
        Rotulo = rotulo;
        Detalhe = detalhe;
        Escolhida = escolhida;
        Escolher = new Comando(escolher);
    }

    public string Rotulo { get; }

    public string? Detalhe { get; }

    public bool Escolhida { get; }

    public Comando Escolher { get; }
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

    /// <summary>O primeiro canal do processador que alimenta o equipamento (0 a 3; a barra na cor dele), ou null sem ligação.</summary>
    public int? Canal { get; }

    public string Id { get; }

    public string Nome { get; }

    /// <summary>"Alto-falante passivo · canal 2 do Módulo genérico · CH2".</summary>
    public string Detalhe { get; }

    /// <summary>O modelo, ou "genérico".</summary>
    public string Modelo { get; }

    /// <summary>A função acústica que vale ("médio-grave"): a escolhida, ou a sugerida; null para quem não toca (grupos 2 a 4).</summary>
    public string? Funcao { get; }

    /// <summary>A nota (a do herdado), ou null.</summary>
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
/// grupo e "Outra…"), nome, modelo, função (com a sugerida), dados técnicos, onde está ligado (só as
/// opções que existem no Setup), ✓, Cancelar e Tirar com confirmação e os avisos. Nada vai pelo BLE
/// e nada tem chave.
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
    private string _diametro = "";
    private string _impedancia = "";
    private string _potencia = "";
    private string _sensibilidade = "";
    private string _faixaMinima = "";
    private string _faixaMaxima = "";
    private string _ganho = "";
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

    /// <summary>"HERDADO" no Setup herdado (Casa 2.1), ou null.</summary>
    public string? Selo => Setup?.Herdado == true ? HerdadoViewModel.TextoSelo : null;

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
            Avisar();
            Avisar(nameof(Categorias));
            Avisar(nameof(Categoria));
            AvisarGrupo();
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

    /// <summary>"Ganho do amplificador", "Diâmetro"… só os que valem para o grupo: o ganho só no módulo.</summary>
    public bool MostraGanho => _grupo == GrupoEquipamento.Amplificador;

    /// <summary>Os dados de alto-falante (diâmetro, impedância, sensibilidade) valem para quem toca.</summary>
    public bool MostraDadosAltoFalante => _grupo is GrupoEquipamento.AltoFalante or GrupoEquipamento.AmplificaETocam;

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

    /// <summary>O modelo; vazio = genérico.</summary>
    public string Modelo
    {
        get => _modelo;
        set
        {
            if (_modelo == value)
                return;
            _modelo = value;
            Avisar();
        }
    }

    /// <summary>A dica do campo do modelo vazio.</summary>
    public string DicaModelo => Ligacoes.TextoGenerico;

    /// <summary>
    /// A função sugerida pela categoria e pelo corte guardado no Setup para o canal que alimenta o
    /// rascunho; null para quem não toca (grupos 2 a 4).
    /// </summary>
    public FuncaoAcustica? FuncaoSugerida => GerenteSetups.FuncaoSugerida(Setup, Montar(exigir: false).Equipamento!);

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

    // Os dados técnicos, como texto (vírgula ou ponto), todos opcionais.

    public string Diametro { get => _diametro; set => Mudar(ref _diametro, value); }

    public string Impedancia { get => _impedancia; set => Mudar(ref _impedancia, value); }

    public string PotenciaRms { get => _potencia; set => Mudar(ref _potencia, value); }

    public string Sensibilidade { get => _sensibilidade; set => Mudar(ref _sensibilidade, value); }

    public string FaixaMinima { get => _faixaMinima; set => Mudar(ref _faixaMinima, value); }

    public string FaixaMaxima { get => _faixaMaxima; set => Mudar(ref _faixaMaxima, value); }

    /// <summary>O ganho do amplificador, em ×.</summary>
    public string Ganho { get => _ganho; set => Mudar(ref _ganho, value); }

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
        _funcao = null;
        _diametro = _impedancia = _potencia = _sensibilidade = _faixaMinima = _faixaMaxima = _ganho = "";
        _vias = [];
        PrepararLigacoes(null);
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
        _funcao = e.Funcao;
        _diametro = Texto(e.Dados.Diametro);
        _impedancia = Texto(e.Dados.Impedancia);
        _potencia = Texto(e.Dados.PotenciaRms);
        _sensibilidade = Texto(e.Dados.Sensibilidade);
        _faixaMinima = Texto(e.Dados.FaixaMinima);
        _faixaMaxima = Texto(e.Dados.FaixaMaxima);
        _ganho = Texto(e.Dados.Ganho);
        _vias = e.Dados.Vias.Select(v => new ViaViewModel(v.Descricao, Texto(v.Diametro), Despachante, Relogio)).ToArray();
        PrepararLigacoes(e);
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
    /// que zero recusa com o motivo; sem, ele fica de fora (para a função sugerida).
    /// </summary>
    private (Equipamento? Equipamento, string? Erro) Montar(bool exigir)
    {
        string? erro = null;
        decimal? Numero(string texto, string rotulo)
        {
            if (string.IsNullOrWhiteSpace(texto))
                return null;
            if (decimal.TryParse(texto.Trim().Replace(',', '.'), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var v) && v > 0m)
                return v;
            erro ??= MsgNumero(rotulo);
            return null;
        }

        var dados = new DadosTecnicos(
            Numero(_diametro, "Diâmetro"), Numero(_impedancia, "Impedância"), Numero(_potencia, "Potência RMS"),
            Numero(_sensibilidade, "Sensibilidade"), Numero(_faixaMinima, "Faixa, início"), Numero(_faixaMaxima, "Faixa, fim"),
            Numero(_ganho, "Ganho"), _grupo == GrupoEquipamento.Amplificador ? _entradas.Count : null)
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
            _itens = setup.Equipamentos
                .Select(e => new ItemEquipamentoViewModel(e, Detalhe(setup, e),
                    _setups.Funcao(setup.Id, e) is { } f ? Ligacoes.RotuloFuncao(f) : null,
                    Ligacoes.CanaisQueAlimentam(setup.Equipamentos, e.Id) is { Count: > 0 } canais ? canais[0] : null, this))
                .ToArray();
            _saidas = _setups.Saidas(setup.Id);
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
        Avisar(nameof(Selo));
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
        Avisar(nameof(Funcao));
        Avisar(nameof(Diametro));
        Avisar(nameof(Impedancia));
        Avisar(nameof(PotenciaRms));
        Avisar(nameof(Sensibilidade));
        Avisar(nameof(FaixaMinima));
        Avisar(nameof(FaixaMaxima));
        Avisar(nameof(Ganho));
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
        Avisar(nameof(MostraGanho));
        Avisar(nameof(MostraDadosAltoFalante));
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

    private void Mudar(ref string campo, string valor, [System.Runtime.CompilerServices.CallerMemberName] string? nome = null)
    {
        if (campo == valor)
            return;
        campo = valor;
        Avisar(nome);
    }

    /// <summary>Um número para o campo, com vírgula ("3,6"); vazio sem valor.</summary>
    private static string Texto(decimal? v) => v is { } n ? n.ToString("0.###", CultureInfo.InvariantCulture).Replace('.', ',') : "";
}
