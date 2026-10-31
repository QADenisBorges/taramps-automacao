using BgsControle.Nucleo;
using BgsControle.Nucleo.Setups;

namespace BgsControle.Telas;

/// <summary>Uma linha do que não bate: o nome do ajuste pela saída do Setup, o valor no Setup e no aparelho, e o canal mestre da saída dona (a cor; null quando não é de uma saída).</summary>
public sealed record LinhaDiferencaViewModel(string Rotulo, string NaMemoria, string NoAparelho, string NomeMemoria, int? Canal)
{
    /// <summary>"80 cm na FILME, 0 cm no aparelho".</summary>
    public string Detalhe => $"{NaMemoria} na {NomeMemoria}, {NoAparelho} no aparelho";
}

/// <summary>Uma linha da lista de Setups: nome, em uso, e o toque que escolhe.</summary>
public sealed class ItemSetupViewModel
{
    internal ItemSetupViewModel(Setup setup, bool emUso, SetupViewModel tela)
    {
        Id = setup.Id;
        Nome = setup.Nome;
        EmUso = emUso;
        Escolher = new Comando(() => tela.EscolherAsync(Id));
    }

    public string Id { get; }

    public string Nome { get; }

    /// <summary>É o último Setup usado (fica em branco na lista).</summary>
    public bool EmUso { get; }

    /// <summary>Aplica a memória ativa deste Setup, relê e desmuta (sem memória, só desmuta).</summary>
    public Comando Escolher { get; }
}

/// <summary>
/// A tela Setup em dúvida e Trocar de Setup (CAP-16, EXPERIENCE "Conectar e Setup"), e o seletor do
/// cabeçalho ("Setup Casa ›"). Na dúvida não há ‹: só se sai por Escolher o Setup, Criar Setup
/// novo ou Seguir sem Setup. Sem nenhum Setup (o app nasce zerado), "Escolher o Setup" some. Na troca, ‹ (ou Voltar) desiste: desmuta e não aplica nada. O som fica
/// mutado no aparelho até a escolha; a lista do que não bate é só leitura.
/// </summary>
public sealed class SetupViewModel : TelaBase, IDisposable
{
    public const string TituloDuvida = "Setup em dúvida";
    public const string TituloTroca = "Trocar de Setup";
    public const string TextoMudo = "Som mutado até você escolher.";
    public const string RotuloDiferencas = "O QUE NÃO BATE";
    public const string SubRotuloDiferencas = "lido agora no aparelho";
    public const string TextoEscolher = "Escolher o Setup";
    public const string FraseEscolher = "Aplica a memória ativa dele, relê e desmuta. Sem memória, só desmuta.";
    public const string TextoCriar = "Criar Setup novo";
    public const string FraseCriar = "O som do aparelho vira a primeira memória dele.";

    /// <summary>A linha da folha do Criar Setup novo.</summary>
    public const string LinhaFolhaCriar = "O som que está no aparelho vira a primeira memória do Setup novo. Os equipamentos você cadastra depois.";
    public const string RotuloNomeSetup = "Nome do Setup";
    public const string DicaNomeSetup = "Carro";
    public const string RotuloNomeMemoria = "Nome da primeira memória";
    public const string LinhaNomeMemoria = "Vazio, a memória fica com o nome do Setup.";
    public const string TextoConfirmarCriar = "Criar Setup";
    public const string TextoCancelar = "Cancelar";
    public const string FraseSemReferencia = "O app não sabe em que Setup o aparelho está.";

    /// <summary>A frase da dúvida sem nenhum Setup cadastrado (a primeira conexão).</summary>
    public const string FraseNenhumSetup = "Nenhum Setup cadastrado. Crie um Setup do aparelho ou siga sem Setup.";

    /// <summary>O rótulo das pílulas do ambiente, na folha do Criar Setup novo e na tela Setup.</summary>
    public const string RotuloAmbiente = "AMBIENTE";

    /// <summary>A linha embaixo das pílulas do ambiente: é opcional.</summary>
    public const string LinhaAmbiente = "Opcional. Toque de novo para deixar sem informar.";

    /// <summary>
    /// O que o leitor de tela diz de uma pílula do ambiente: "Ambiente Sala, escolhido. Toque de novo
    /// para deixar sem informar." ou "Ambiente Sala, não escolhido.".
    /// </summary>
    public static string DescricaoAmbiente(AmbienteSetup ambiente, bool escolhido) => escolhido
        ? $"Ambiente {Ambientes.Rotulo(ambiente)}, escolhido. Toque de novo para deixar sem informar."
        : $"Ambiente {Ambientes.Rotulo(ambiente)}, não escolhido.";

    /// <summary>"O Casa ainda não tem memória." (sem memória para comparar, a lista some).</summary>
    public static string FraseSemMemoria(string setup) => $"O {setup} ainda não tem memória.";

    /// <summary>"O aparelho não está como o Casa deixou da última vez." (com diferenças).</summary>
    public static string FraseComDiferencas(string setup) => $"O aparelho não está como o {setup} deixou da última vez.";

    /// <summary>"O aparelho está no Casa." (com memória e sem diferença, na troca).</summary>
    public static string FraseNoSetup(string setup) => $"O aparelho está no {setup}.";
    public const string TextoSeguir = "Seguir sem Setup";
    public const string FraseSeguir = "Desmuta sem aplicar nada. As memórias do celular ficam travadas.";
    public const string SemSetup = "Sem Setup";

    private readonly GerenteSetups _setups;
    private readonly Sessao _sessao;

    private IReadOnlyList<ItemSetupViewModel> _itens = [];
    private string? _mensagem;
    private bool _listaAberta;
    private bool _criarAberto;
    private string _nomeNovo = "";
    private string? _erroNome;
    private string _nomeMemoriaNova = "";
    private string? _erroNomeMemoria;
    private AmbienteSetup? _ambienteNovo;
    private bool _agindo;

    public SetupViewModel(GerenteSetups setups, Sessao sessao, IDespachante despachante, TimeProvider relogio)
        : base(despachante, relogio)
    {
        _setups = setups;
        _sessao = sessao;
        Trocar = new Comando(TrocarAsync);
        AbrirLista = new Comando(() =>
        {
            ListaAberta = true;
            FecharCriar();
        });
        FecharLista = new Comando(() =>
        {
            ListaAberta = false;
        });
        AbrirCriar = new Comando(() =>
        {
            ListaAberta = false;
            _criarAberto = true;
            NomeNovo = "";
            NomeMemoriaNova = "";
            AmbienteNovo = null;
            ErroNome = null;
            ErroNomeMemoria = null;
            Mensagem = null;
            Avisar(nameof(CriarAberto));
            Avisar(nameof(DicaNomeMemoria));
        });
        ConfirmarCriar = new Comando(ConfirmarCriarAsync);
        CancelarCriar = new Comando(FecharCriar);
        SeguirSemSetup = new Comando(SeguirAsync);
        Desistir = new Comando(DesistirAsync);

        _setups.Mudou += AoMudar;
        _sessao.Mudou += AoMudar;
        Atualizar();
    }

    // ------------------------------------------------------------------ seletor do cabeçalho

    /// <summary>"Setup Casa ›" ou "Sem Setup ›".</summary>
    public string Seletor => _setups.SetupEmUso is { } s ? $"Setup {s.Nome} ›" : $"{SemSetup} ›";

    /// <summary>Tocar no seletor com o app conectado: muta e abre "Trocar de Setup".</summary>
    public Comando Trocar { get; }

    // ------------------------------------------------------------------ a tela

    /// <summary>A tela está aberta: Setup em dúvida ou Trocar de Setup.</summary>
    public bool Aberta => _setups.EmEscolha;

    /// <summary>Na troca há ‹ e Desistir; na dúvida, não.</summary>
    public bool PodeDesistir => _setups.Estado == EstadoConferencia.Trocando;

    /// <summary>"Setup em dúvida" ou "Trocar de Setup".</summary>
    public string? Titulo => _setups.Estado switch
    {
        EstadoConferencia.Duvida => TituloDuvida,
        EstadoConferencia.Trocando => TituloTroca,
        _ => null,
    };

    /// <summary>
    /// "Nenhum Setup cadastrado. …" (nenhum Setup), "O Casa ainda não tem memória." (o Setup sem memória
    /// para comparar), "O aparelho não está como o Casa deixou da última vez." (com diferenças), ou "O
    /// aparelho está no Casa." (com memória e sem diferença).
    /// </summary>
    public string? Frase
    {
        get
        {
            if (!Aberta)
                return null;
            var nome = _setups.Referencia?.Nome;
            if (nome is null && _setups.Setups.Count == 0)
                return FraseNenhumSetup;
            if (nome is null)
                return _setups.Estado == EstadoConferencia.Duvida ? FraseSemReferencia : null;
            if (_setups.MemoriaDaReferencia is null)
                return FraseSemMemoria(nome);
            return _setups.Diferencas.Count > 0 ? FraseComDiferencas(nome) : FraseNoSetup(nome);
        }
    }

    /// <summary>"O QUE NÃO BATE", em cima da lista.</summary>
    public string TituloDiferencas => RotuloDiferencas;

    /// <summary>"lido agora no aparelho", à direita do rótulo.</summary>
    public string SubtituloDiferencas => SubRotuloDiferencas;

    /// <summary>"Som mutado até você escolher." enquanto o mudo da dúvida está no aparelho.</summary>
    public string? FaixaMudo => Aberta && _setups.Mutado ? TextoMudo : null;

    /// <summary>Por que o som não foi mutado ("O som não foi mutado: chave desligada"), ou null.</summary>
    public string? MotivoMudo => Aberta ? _setups.MotivoMudo : null;

    /// <summary>"Atraso · Sub: 80 cm na FILME, 0 cm no aparelho", uma por diferença (contra a memória ativa da referência).</summary>
    public IReadOnlyList<string> Diferencas
    {
        get
        {
            var nome = _setups.MemoriaDaReferencia?.Nome ?? "memória";
            return Aberta ? _setups.Diferencas.Select(d => d.Linha(nome)).ToArray() : [];
        }
    }

    /// <summary>
    /// A lista do que não bate em duas linhas, com a saída dona (a barra na cor dela): "Atraso · Sub"
    /// e "80 cm na FILME, 0 cm no aparelho".
    /// </summary>
    public IReadOnlyList<LinhaDiferencaViewModel> LinhasDiferenca
    {
        get
        {
            if (!Aberta)
                return [];
            var nome = _setups.MemoriaDaReferencia?.Nome ?? "memória";
            return _setups.Diferencas
                .Select(d => new LinhaDiferencaViewModel(d.Rotulo, d.NaMemoria, d.NoAparelho, nome, d.Canal))
                .ToArray();
        }
    }

    /// <summary>Há diferenças para listar (senão a lista e o rótulo somem).</summary>
    public bool TemDiferencas => Aberta && _setups.Diferencas.Count > 0;

    /// <summary>Os Setups para escolher, na ordem em que foram criados.</summary>
    public IReadOnlyList<ItemSetupViewModel> Setups => _itens;

    /// <summary>"Escolher o Setup" aparece: há algum Setup para escolher.</summary>
    public bool PodeEscolher => _itens.Count > 0;

    public ItemSetupViewModel? Item(string nome) =>
        _itens.FirstOrDefault(i => string.Equals(i.Nome, nome, StringComparison.OrdinalIgnoreCase));

    /// <summary>A lista de Setups (aberta por "Escolher o Setup").</summary>
    public bool ListaAberta
    {
        get => _listaAberta;
        private set
        {
            if (_listaAberta == value)
                return;
            _listaAberta = value;
            Avisar();
        }
    }

    public Comando AbrirLista { get; }

    public Comando FecharLista { get; }

    /// <summary>A última frase ("Setup Casa em uso.", "Chave desligada").</summary>
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

    public bool Ocupado => _agindo || _setups.Ocupado;

    /// <summary>Escolhe o Setup pelo id (a linha da lista).</summary>
    public async Task EscolherAsync(string id)
    {
        ResultadoSetup? resultado = null;
        await Agir(async () =>
        {
            resultado = await _setups.EscolherAsync(id);
            return resultado.Mensagem;
        }, depois: () =>
        {
            if (resultado is { Ok: true })
                ListaAberta = false;
        });
    }

    // ------------------------------------------------------------------ criar

    public bool CriarAberto => _criarAberto;

    public string NomeNovo
    {
        get => _nomeNovo;
        set
        {
            if (_nomeNovo == value)
                return;
            _nomeNovo = value;
            Avisar();
            Avisar(nameof(DicaNomeMemoria));
        }
    }

    /// <summary>A dica do campo da memória: o nome que ela ganha se ficar vazio (o do Setup, ou "Carro").</summary>
    public string DicaNomeMemoria => string.IsNullOrWhiteSpace(_nomeNovo) ? DicaNomeSetup : _nomeNovo.Trim();

    /// <summary>"Use de 1 a 24 letras…", "Já existe um Setup com esse nome.", ou null (embaixo do campo do Setup).</summary>
    public string? ErroNome
    {
        get => _erroNome;
        private set
        {
            if (_erroNome == value)
                return;
            _erroNome = value;
            Avisar();
        }
    }

    /// <summary>O nome da primeira memória; vazio = o nome do Setup.</summary>
    public string NomeMemoriaNova
    {
        get => _nomeMemoriaNova;
        set
        {
            if (_nomeMemoriaNova == value)
                return;
            _nomeMemoriaNova = value;
            Avisar();
        }
    }

    /// <summary>"Nome da memória: Use de 1 a 24 letras…", ou null (embaixo do campo da memória).</summary>
    public string? ErroNomeMemoria
    {
        get => _erroNomeMemoria;
        private set
        {
            if (_erroNomeMemoria == value)
                return;
            _erroNomeMemoria = value;
            Avisar();
        }
    }

    /// <summary>O ambiente do Setup novo; null = não informado (não há padrão).</summary>
    public AmbienteSetup? AmbienteNovo
    {
        get => _ambienteNovo;
        set
        {
            if (_ambienteNovo == value)
                return;
            _ambienteNovo = value;
            Avisar();
            Avisar(nameof(AmbientesTela));
        }
    }

    /// <summary>
    /// O ambiente em pílulas (Sala, Carro, Ao ar livre, Outro), o escolhido em branco; tocar no
    /// escolhido deixa sem informar.
    /// </summary>
    public IReadOnlyList<EscolhaViewModel> AmbientesTela =>
        Ambientes.Todos.Select(a => new EscolhaViewModel(Ambientes.Rotulo(a), null, _ambienteNovo == a,
            () => AmbienteNovo = _ambienteNovo == a ? null : a) { Descricao = DescricaoAmbiente(a, _ambienteNovo == a) }).ToArray();

    public Comando AbrirCriar { get; }

    /// <summary>O ✓ dos nomes: cria o Setup com o som do aparelho como a primeira memória dele (vazio = com o nome do Setup) e desmuta.</summary>
    public Comando ConfirmarCriar { get; }

    public Comando CancelarCriar { get; }

    private async Task ConfirmarCriarAsync()
    {
        if (!_criarAberto)
            return;
        ResultadoSetup? resultado = null;
        await Agir(async () =>
        {
            resultado = await _setups.CriarSetupAsync(NomeNovo, NomeMemoriaNova, AmbienteNovo);
            return resultado.Mensagem;
        }, depois: () =>
        {
            if (resultado is { Ok: true })
            {
                FecharCriar();
                return;
            }
            // Os campos ficam, com os nomes digitados; o erro de um nome vai embaixo do campo que errou, o
            // resto (ocupado, sem leitura, não guardou) fica na Mensagem.
            ErroNome = resultado is { ErroNomeSetup: true } ? resultado.Mensagem : null;
            ErroNomeMemoria = resultado is { ErroNomeMemoria: true } ? resultado.Mensagem : null;
        });
    }

    private void FecharCriar()
    {
        _criarAberto = false;
        _nomeNovo = "";
        _erroNome = null;
        _nomeMemoriaNova = "";
        _erroNomeMemoria = null;
        _ambienteNovo = null;
        Avisar(nameof(CriarAberto));
        Avisar(nameof(AmbienteNovo));
        Avisar(nameof(AmbientesTela));
        Avisar(nameof(NomeNovo));
        Avisar(nameof(ErroNome));
        Avisar(nameof(NomeMemoriaNova));
        Avisar(nameof(ErroNomeMemoria));
        Avisar(nameof(DicaNomeMemoria));
    }

    // ------------------------------------------------------------------ seguir, trocar, desistir

    public Comando SeguirSemSetup { get; }

    /// <summary>‹ ou Voltar na troca: desmuta e volta ao estado de antes. Na dúvida, nada (não há ‹).</summary>
    public Comando Desistir { get; }

    private Task SeguirAsync() => Agir(async () => (await _setups.SeguirSemSetupAsync()).Mensagem);

    private Task TrocarAsync() => Agir(async () => (await _setups.TrocarAsync()).Mensagem);

    private async Task DesistirAsync()
    {
        if (!PodeDesistir)
            return;
        await Agir(async () => (await _setups.DesistirAsync()).Mensagem);
    }

    // ------------------------------------------------------------------ apoio

    private async Task Agir(Func<Task<string?>> acao, Action? depois = null)
    {
        _agindo = true;
        NaTela(() => Avisar(nameof(Ocupado)));
        string? frase;
        try
        {
            frase = await acao();
        }
        catch (Exception e)
        {
            frase = $"Erro inesperado ({e.GetType().Name}).";
        }
        var feito = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        NaTela(() =>
        {
            try
            {
                _agindo = false;
                depois?.Invoke();
                Mensagem = frase;
                Atualizar();
            }
            finally
            {
                feito.SetResult();
            }
        });
        await feito.Task;
    }

    private void AoMudar() => NaTela(Atualizar);

    private void Atualizar()
    {
        var ultimo = _setups.Referencia?.Id ?? _setups.SetupEmUso?.Id ?? _setups.UltimoSetup;
        _itens = _setups.Setups.Select(s => new ItemSetupViewModel(s, s.Id == ultimo, this)).ToArray();
        if (!Aberta)
            _listaAberta = false;
        Avisar(nameof(Setups));
        Avisar(nameof(PodeEscolher));
        Avisar(nameof(Seletor));
        Avisar(nameof(Aberta));
        Avisar(nameof(PodeDesistir));
        Avisar(nameof(Titulo));
        Avisar(nameof(Frase));
        Avisar(nameof(FaixaMudo));
        Avisar(nameof(MotivoMudo));
        Avisar(nameof(Diferencas));
        Avisar(nameof(LinhasDiferenca));
        Avisar(nameof(TemDiferencas));
        Avisar(nameof(ListaAberta));
        Avisar(nameof(Ocupado));
    }

    public void Dispose()
    {
        _setups.Mudou -= AoMudar;
        _sessao.Mudou -= AoMudar;
    }
}
