using BgsControle.Nucleo;
using BgsControle.Nucleo.Setups;

namespace BgsControle.Telas;

/// <summary>Uma linha do que não bate: o nome do ajuste pela saída do Setup, o valor no Setup e no aparelho, e o canal mestre da saída dona (a cor; null quando não é de uma saída).</summary>
public sealed record LinhaDiferencaViewModel(string Rotulo, string NoSetup, string NoAparelho, string NomeSetup, int? Canal)
{
    /// <summary>"80 cm no Casa 2.1, 0 cm no aparelho".</summary>
    public string Detalhe => $"{NoSetup} no {NomeSetup}, {NoAparelho} no aparelho";
}

/// <summary>Uma linha da lista de Setups: nome, selo HERDADO, em uso, e o toque que escolhe.</summary>
public sealed class ItemSetupViewModel
{
    internal ItemSetupViewModel(Setup setup, bool emUso, SetupViewModel tela)
    {
        Id = setup.Id;
        Nome = setup.Nome;
        Herdado = setup.Herdado;
        Nota = setup.Nota;
        EmUso = emUso;
        Escolher = new Comando(() => tela.EscolherAsync(Id));
    }

    public string Id { get; }

    public string Nome { get; }

    public bool Herdado { get; }

    /// <summary>O selo HERDADO, ou null.</summary>
    public string? Selo => Herdado ? HerdadoViewModel.TextoSelo : null;

    /// <summary>A nota herdada, ou null.</summary>
    public string? Nota { get; }

    /// <summary>É o último Setup usado (fica em branco na lista).</summary>
    public bool EmUso { get; }

    /// <summary>Aplica a calibração e a memória ativa deste Setup, relê e desmuta.</summary>
    public Comando Escolher { get; }
}

/// <summary>
/// A tela Setup em dúvida e Trocar de Setup (CAP-16, EXPERIENCE "Conectar e Setup"), e o seletor do
/// cabeçalho ("Setup Casa 2.1 ›"). Na dúvida não há ‹: só se sai por Escolher o Setup, Criar Setup
/// novo ou Seguir sem Setup. Na troca, ‹ (ou Voltar) desiste: desmuta e não aplica nada. O som fica
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
    public const string FraseEscolher = "Aplica a calibração e a memória ativa dele, relê e desmuta.";
    public const string TextoCriar = "Criar Setup novo";
    public const string FraseCriar = "Nasce com a calibração que está no aparelho.";
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
            ErroNome = null;
            Avisar(nameof(CriarAberto));
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

    /// <summary>"Setup Casa 2.1 ›" ou "Sem Setup ›".</summary>
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
    /// "O aparelho não está como o Casa 2.1 deixou da última vez." (com diferenças), ou "O aparelho
    /// está no Casa 2.1." (na troca, sem diferença).
    /// </summary>
    public string? Frase
    {
        get
        {
            if (!Aberta)
                return null;
            var nome = _setups.Referencia?.Nome;
            if (nome is null)
                return _setups.Estado == EstadoConferencia.Duvida ? "O app não sabe em que Setup o aparelho está." : null;
            return _setups.Diferencas.Count > 0
                ? $"O aparelho não está como o {nome} deixou da última vez."
                : $"O aparelho está no {nome}.";
        }
    }

    /// <summary>"Som mutado até você escolher." enquanto o mudo da dúvida está no aparelho.</summary>
    public string? FaixaMudo => Aberta && _setups.Mutado ? TextoMudo : null;

    /// <summary>Por que o som não foi mutado ("O som não foi mutado: chave desligada"), ou null.</summary>
    public string? MotivoMudo => Aberta ? _setups.MotivoMudo : null;

    /// <summary>"Atraso da saída 3: 80 cm no Casa 2.1, 0 cm no aparelho", uma por diferença.</summary>
    public IReadOnlyList<string> Diferencas
    {
        get
        {
            var nome = _setups.Referencia?.Nome ?? "Setup";
            return Aberta ? _setups.Diferencas.Select(d => d.Linha(nome)).ToArray() : [];
        }
    }

    /// <summary>
    /// A lista do que não bate em duas linhas, com a saída dona (a barra na cor dela): "Atraso do Sub"
    /// e "80 cm no Casa 2.1, 0 cm no aparelho".
    /// </summary>
    public IReadOnlyList<LinhaDiferencaViewModel> LinhasDiferenca
    {
        get
        {
            if (!Aberta)
                return [];
            var nome = _setups.Referencia?.Nome ?? "Setup";
            return _setups.Diferencas
                .Select(d => new LinhaDiferencaViewModel(d.Rotulo, d.NoSetup, d.NoAparelho, nome, d.Canal))
                .ToArray();
        }
    }

    /// <summary>Há diferenças para listar (senão a lista e o rótulo somem).</summary>
    public bool TemDiferencas => Aberta && _setups.Diferencas.Count > 0;

    /// <summary>Os Setups para escolher, o herdado primeiro.</summary>
    public IReadOnlyList<ItemSetupViewModel> Setups => _itens;

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

    /// <summary>A última frase ("Setup Casa 2.1 em uso.", "Chave desligada").</summary>
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
        }
    }

    /// <summary>"Use de 1 a 24 letras…", "Já existe um Setup com esse nome.", ou null.</summary>
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

    public Comando AbrirCriar { get; }

    /// <summary>O ✓ do nome: cria o Setup com a calibração do aparelho e desmuta.</summary>
    public Comando ConfirmarCriar { get; }

    public Comando CancelarCriar { get; }

    private async Task ConfirmarCriarAsync()
    {
        if (!_criarAberto)
            return;
        ResultadoSetup? resultado = null;
        await Agir(async () =>
        {
            resultado = await _setups.CriarSetupAsync(NomeNovo);
            return resultado.Mensagem;
        }, depois: () =>
        {
            if (resultado is { Ok: true })
                FecharCriar();
            else
                ErroNome = resultado?.Mensagem; // o campo fica, com o nome digitado
        });
    }

    private void FecharCriar()
    {
        _criarAberto = false;
        _nomeNovo = "";
        _erroNome = null;
        Avisar(nameof(CriarAberto));
        Avisar(nameof(NomeNovo));
        Avisar(nameof(ErroNome));
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
