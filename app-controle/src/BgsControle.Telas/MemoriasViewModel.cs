using BgsControle.Nucleo;
using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Seguranca;
using BgsControle.Nucleo.Setups;

namespace BgsControle.Telas;

/// <summary>O cartão de uma memória do celular na tela Memórias: nome, o preset lembrado, EM USO, Aplicar, Renomear e Apagar.</summary>
public sealed class CartaoMemoriaCelularViewModel : TelaBase
{
    private string _nome;
    private string? _textoPreset;
    private bool _emUso;

    internal CartaoMemoriaCelularViewModel(MemoriaCelular memoria, MemoriasViewModel tela, IDespachante despachante, TimeProvider relogio)
        : base(despachante, relogio)
    {
        Id = memoria.Id;
        _nome = memoria.Nome;
        Nivel = tela.Nivel;
        Aplicar = new Comando(() => tela.AplicarAsync(Id));
        Renomear = new Comando(() => tela.AbrirRenomear(Id));
        Apagar = new Comando(() => tela.AbrirApagar(Id));
    }

    public string Id { get; }

    /// <summary>A folha do nível (história 13g): o chip "Nível: Alto ›" aparece no cartão da memória em uso. Ou null.</summary>
    public NivelViewModel? Nivel { get; }

    public string Nome
    {
        get => _nome;
        internal set
        {
            if (_nome == value)
                return;
            _nome = value;
            Avisar();
        }
    }

    /// <summary>"Preset: Rock" (o preset que ela lembra e aplica junto), ou null sem preset lembrado.</summary>
    public string? TextoPreset
    {
        get => _textoPreset;
        internal set
        {
            if (_textoPreset == value)
                return;
            _textoPreset = value;
            Avisar();
        }
    }

    /// <summary>É a memória em uso (selo EM USO). Apagar fica travado nela.</summary>
    public bool EmUso
    {
        get => _emUso;
        internal set
        {
            if (_emUso == value)
                return;
            _emUso = value;
            Avisar();
            Avisar(nameof(SeloEmUso));
            Avisar(nameof(MotivoTravaApagar));
        }
    }

    /// <summary>"EM USO" na que toca, ou null.</summary>
    public string? SeloEmUso => EmUso ? MemoriasViewModel.TextoEmUso : null;

    /// <summary>"Esta memória está em uso." na que toca, ou null.</summary>
    public string? MotivoTravaApagar => EmUso ? GerenteSetups.MsgMemoriaEmUso : null;

    /// <summary>Aplica, sem confirmação (cópia antes, escreve, relê).</summary>
    public Comando Aplicar { get; }

    /// <summary>Abre o campo do nome (vale no ✓). Não manda nada ao aparelho.</summary>
    public Comando Renomear { get; }

    /// <summary>Abre a folha "Apagar a X?". Na memória em uso, mostra o motivo.</summary>
    public Comando Apagar { get; }
}

/// <summary>
/// A tela Memórias (Configurações › Memórias, EXPERIENCE): os cartões das memórias do celular do
/// Setup em uso (Aplicar, Renomear, Apagar; selo EM USO) e "Criar memória do som atual" (que
/// abre a folha do Salvar quando o som em uso não é de uma memória do celular). As memórias do
/// processador não aparecem (decisão do Denis, 29/10/2026). Sem Setup, as memórias do celular ficam
/// travadas com "Escolha um Setup para usar as memórias do celular.". Depois de uma queda no meio de
/// um aplicar, mostra "aplicado pela metade" com Terminar e Voltar à cópia.
/// </summary>
public sealed class MemoriasViewModel : TelaBase, IDisposable
{
    public const string TextoEmUso = "EM USO";
    public const string TextoNenhuma = "Nenhuma memória";
    public const string TextoCriar = "Criar memória do som atual";
    public const string TextoNovaMemoria = "+ Nova memória";
    public const string AvisoApagar = "A memória e a playlist dela somem.";
    public const string TextoCancelar = "Cancelar";
    public const string TituloPelaMetade = "Aplicado pela metade";
    public const string TextoTerminar = "Terminar";
    public const string TextoVoltar = "Voltar à cópia";
    public const string MsgEmAndamento = "Uma operação está em andamento";
    public const string MsgVoltou = "O som voltou à cópia.";

    /// <summary>"Preset: Cinema".</summary>
    public static string TextoPresetDe(string nome) => $"Preset: {nome}";

    private readonly GerenteSetups _setups;
    private readonly CopiaAntes _copia;
    private readonly Sessao _sessao;
    private readonly IChaves _chaves;
    private readonly EstadoAparelho _estado;
    private readonly SalvarViewModel _salvar;
    private readonly CriacaoMemoria? _criacao;

    private IReadOnlyList<CartaoMemoriaCelularViewModel> _cartoes = [];
    private string? _mensagem;
    private bool _criarAberto;
    private string _nomeNova = "";
    private string? _erroNova;
    private string? _renomear;
    private string _nomeRenomear = "";
    private string? _erroRenomear;
    private string? _apagar;
    private bool _agindo;

    public MemoriasViewModel(
        GerenteSetups setups,
        CopiaAntes copia,
        Sessao sessao,
        IChaves chaves,
        EstadoAparelho estado,
        SalvarViewModel salvar,
        IDespachante despachante,
        TimeProvider relogio,
        CriacaoMemoria? criacao = null,
        NivelViewModel? nivel = null)
        : base(despachante, relogio)
    {
        Nivel = nivel;
        _setups = setups;
        _criacao = criacao;
        _copia = copia;
        _sessao = sessao;
        _chaves = chaves;
        _estado = estado;
        _salvar = salvar;

        AbrirCriar = new Comando(AbrirCriarAsync);
        ConfirmarCriar = new Comando(ConfirmarCriarAsync);
        CancelarCriar = new Comando(FecharCriar);
        ConfirmarRenomear = new Comando(ConfirmarRenomearAsync);
        CancelarRenomear = new Comando(FecharRenomear);
        ConfirmarApagar = new Comando(ConfirmarApagarAsync);
        CancelarApagar = new Comando(FecharApagar);
        Reaplicar = new Comando(ReaplicarAsync);
        Terminar = new Comando(TerminarAsync);
        VoltarACopia = new Comando(VoltarAsync);
        NovaMemoria = new Comando(PedirNovaMemoria);

        _setups.Mudou += AoMudarAlgo;
        _copia.Mudou += AoMudarAlgo;
        _sessao.Mudou += AoMudarAlgo;
        _chaves.Mudou += AoMudarChave;
        _estado.Recarregou += AoMudarAlgo;
        if (_criacao is not null)
            _criacao.Mudou += AoMudarAlgo;
        Atualizar();
    }

    // ------------------------------------------------------------------ as memórias do celular

    /// <summary>A folha do nível (história 13g), ou null.</summary>
    public NivelViewModel? Nivel { get; }

    /// <summary>O Setup das memórias da lista: o em uso ou, sem ele, o último usado (só para mostrar).</summary>
    private Setup? SetupDaLista => _setups.SetupEmUso ?? _setups.Setup(_setups.UltimoSetup);

    /// <summary>"Memórias do Casa 2.1", ou null.</summary>
    public string? TituloCelular => SetupDaLista is { } s ? $"Memórias do {s.Nome}" : null;

    /// <summary>Os cartões das memórias do celular do Setup (na ordem em que foram criadas).</summary>
    public IReadOnlyList<CartaoMemoriaCelularViewModel> Cartoes => _cartoes;

    /// <summary>O cartão pelo nome (sem distinguir maiúsculas), ou null.</summary>
    public CartaoMemoriaCelularViewModel? Cartao(string nome) =>
        _cartoes.FirstOrDefault(c => string.Equals(c.Nome, nome, StringComparison.OrdinalIgnoreCase));

    /// <summary>"Nenhuma memória" quando o Setup não tem memórias; senão null.</summary>
    public string? TextoVazio => _cartoes.Count == 0 ? TextoNenhuma : null;

    /// <summary>"Som diferente da FILME" quando o caráter lido difere da memória ativa; senão null.</summary>
    public string? AvisoSomDiferente =>
        _setups.Estado == EstadoConferencia.SomDiferente && _setups.Ativa(_setups.SetupEmUso?.Id) is { } ativa
            ? GerenteSetups.MsgSomDiferente(ativa.Nome)
            : null;

    /// <summary>Reaplica a memória ativa (o botão do aviso "Som diferente").</summary>
    public Comando Reaplicar { get; }

    /// <summary>A última frase: "Memória MUSICA ORIG criada no Casa 2.1.", "Chave desligada"…</summary>
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

    public bool Ocupado => _agindo || _setups.Ocupado || _copia.EmAndamento;

    // ------------------------------------------------------------------ travas

    /// <summary>Por que as memórias do celular não podem ser usadas agora (sem Setup, dúvida), ou null.</summary>
    public string? MotivoTravaCelular
    {
        get
        {
            if (_setups.EmEscolha)
                return GerenteSetups.MsgDuvida;
            if (_setups.SetupEmUso is null)
                return GerenteSetups.MsgSemSetup;
            return null;
        }
    }

    /// <summary>Por que "Aplicar" está travado, ou null.</summary>
    public string? MotivoTravaAplicar => MotivoTravaCelular ?? MotivoTravaAparelho(Chave.AplicarMemoriaCelular);

    /// <summary>Por que "Criar memória do som atual" está travado, ou null. Não precisa de chave.</summary>
    public string? MotivoTravaCriar => MotivoTravaCelular ?? MotivoTravaLeitura();

    public bool PodeAplicar => MotivoTravaAplicar is null;
    public bool PodeCriar => MotivoTravaCriar is null;

    /// <summary>
    /// Por que "+ Nova memória" está travado, ou null: sem Setup em uso, na dúvida, no só leitura ou com uma
    /// criação já aberta (história 10d).
    /// </summary>
    public string? MotivoTravaNovaMemoria
    {
        get
        {
            if (MotivoTravaCelular is { } celular)
                return celular;
            if (_sessao.Estado == EstadoSessao.Conectado && _sessao.SoLeitura)
                return ControleParametroViewModel.MsgSoLeitura;
            if (_criacao is { Aberta: true })
                return CriacaoMemoria.MsgJaAberta;
            return null;
        }
    }

    public bool PodeNovaMemoria => _criacao is not null && MotivoTravaNovaMemoria is null;

    private string? MotivoTravaAparelho(Chave chave)
    {
        if (_sessao.Estado != EstadoSessao.Conectado || !_sessao.SegurandoConexao)
            return ControleParametroViewModel.MsgSemConexao;
        if (_sessao.SoLeitura)
            return ControleParametroViewModel.MsgSoLeitura;
        if (!_chaves.Ligada(chave))
            return ControleParametroViewModel.MsgChaveDesligada;
        if (MotivoTravaLeitura() is { } leitura)
            return leitura;
        if (_copia.PelaMetade is not null)
            return CopiaAntes.MsgPelaMetadePendente;
        return null;
    }

    private string? MotivoTravaLeitura()
    {
        if (_sessao.Estado != EstadoSessao.Conectado || !_sessao.SegurandoConexao)
            return ControleParametroViewModel.MsgSemConexao;
        if (_estado.Lendo)
            return ControleParametroViewModel.MsgLendo;
        if (!_estado.Pronto)
            return ControleParametroViewModel.MsgNaoLeu;
        if (Ocupado)
            return MsgEmAndamento;
        return null;
    }

    // ------------------------------------------------------------------ aplicar

    /// <summary>Aplicar: age direto, sem confirmação. Travado, nada sai e o motivo aparece.</summary>
    public async Task AplicarAsync(string id)
    {
        if (MotivoTravaAplicar is { } motivo)
        {
            Mensagem = motivo;
            return;
        }
        await Agir(async () => (await _setups.AplicarAsync(id)).Mensagem);
    }

    private async Task ReaplicarAsync()
    {
        if (MotivoTravaAplicar is { } motivo)
        {
            Mensagem = motivo;
            return;
        }
        await Agir(async () => (await _setups.ReaplicarAsync()).Mensagem);
    }

    // ------------------------------------------------------------------ + Nova memória (história 10d)

    /// <summary>
    /// "+ Nova memória": pede a criação (o evento <c>criacao-pedida</c> na porta). A tela não abre a criação; ela
    /// aparece quando o cliente pareado abre. Travado, nada sai e o motivo aparece.
    /// </summary>
    public Comando NovaMemoria { get; }

    private void PedirNovaMemoria()
    {
        if (_criacao is null)
            return;
        if (MotivoTravaNovaMemoria is { } motivo)
        {
            Mensagem = motivo;
            return;
        }
        var r = _criacao.Pedir();
        Mensagem = r.Ok ? null : r.Mensagem ?? r.Motivo;
    }

    // ------------------------------------------------------------------ criar memória do som atual

    public bool CriarAberto => _criarAberto;

    public string NomeNova
    {
        get => _nomeNova;
        set
        {
            if (_nomeNova == value)
                return;
            _nomeNova = value;
            Avisar();
        }
    }

    public string? ErroNova
    {
        get => _erroNova;
        private set
        {
            if (_erroNova == value)
                return;
            _erroNova = value;
            Avisar();
        }
    }

    /// <summary>
    /// "Criar memória do som atual": abre o campo do nome. Com o som do aparelho em uso, abre a folha
    /// do Salvar (a memória nova sai de lá). Travado, só o motivo.
    /// </summary>
    public Comando AbrirCriar { get; }

    /// <summary>O ✓ do nome: cria a memória, que vira a em uso.</summary>
    public Comando ConfirmarCriar { get; }

    public Comando CancelarCriar { get; }

    private async Task AbrirCriarAsync()
    {
        if (MotivoTravaCriar is { } motivo)
        {
            Mensagem = motivo;
            return;
        }
        if (_setups.EmUso.Tipo is TipoSomEmUso.SomDoAparelho)
        {
            await _salvar.Abrir.ExecutarAsync();
            return;
        }
        FecharRenomear();
        FecharApagar();
        _criarAberto = true;
        NomeNova = "";
        ErroNova = null;
        Mensagem = null;
        Avisar(nameof(CriarAberto));
    }

    private async Task ConfirmarCriarAsync()
    {
        if (!_criarAberto)
            return;
        ResultadoSetup? resultado = null;
        await Agir(async () =>
        {
            resultado = await _setups.CriarMemoriaAsync(NomeNova);
            return resultado.Mensagem;
        }, depois: () =>
        {
            if (resultado is { Ok: true })
                FecharCriar();
            else
                _erroNova = resultado?.Mensagem; // o campo fica, com o nome digitado
            Avisar(nameof(ErroNova));
        });
        if (resultado is { PedeFolhaSalvar: true })
        {
            NaTela(FecharCriar);
            await _salvar.Abrir.ExecutarAsync();
        }
    }

    private void FecharCriar()
    {
        _criarAberto = false;
        _nomeNova = "";
        _erroNova = null;
        Avisar(nameof(CriarAberto));
        Avisar(nameof(NomeNova));
        Avisar(nameof(ErroNova));
    }

    // ------------------------------------------------------------------ renomear

    public bool RenomearAberto => _renomear is not null;

    /// <summary>"Renomear a FILME".</summary>
    public string? RenomearTitulo => _renomear is { } id && _setups.Memoria(id) is { } m ? $"Renomear a {m.Nome}" : null;

    public string NomeRenomear
    {
        get => _nomeRenomear;
        set
        {
            if (_nomeRenomear == value)
                return;
            _nomeRenomear = value;
            Avisar();
        }
    }

    public string? ErroRenomear
    {
        get => _erroRenomear;
        private set
        {
            if (_erroRenomear == value)
                return;
            _erroRenomear = value;
            Avisar();
        }
    }

    public Comando ConfirmarRenomear { get; }

    public Comando CancelarRenomear { get; }

    /// <summary>"Renomear": abre o campo com o nome atual. Não manda nada ao aparelho.</summary>
    public void AbrirRenomear(string id)
    {
        if (_setups.Memoria(id) is not { } memoria)
            return;
        FecharCriar();
        FecharApagar();
        _renomear = id;
        NomeRenomear = memoria.Nome;
        ErroRenomear = null;
        Mensagem = null;
        Avisar(nameof(RenomearAberto));
        Avisar(nameof(RenomearTitulo));
    }

    private async Task ConfirmarRenomearAsync()
    {
        if (_renomear is not { } id)
            return;
        ResultadoSetup? resultado = null;
        await Agir(async () =>
        {
            resultado = await _setups.RenomearMemoriaAsync(id, NomeRenomear);
            return resultado.Mensagem;
        }, depois: () =>
        {
            if (resultado is { Ok: true })
                FecharRenomear();
            else
                _erroRenomear = resultado?.Mensagem;
            Avisar(nameof(ErroRenomear));
        });
    }

    private void FecharRenomear()
    {
        _renomear = null;
        _nomeRenomear = "";
        _erroRenomear = null;
        Avisar(nameof(RenomearAberto));
        Avisar(nameof(RenomearTitulo));
        Avisar(nameof(NomeRenomear));
        Avisar(nameof(ErroRenomear));
    }

    // ------------------------------------------------------------------ apagar

    public bool ApagarAberto => _apagar is not null;

    /// <summary>"Apagar a MUSICA ORIG?"</summary>
    public string? ApagarTitulo => _apagar is { } id && _setups.Memoria(id) is { } m ? $"Apagar a {m.Nome}?" : null;

    /// <summary>"A memória e a playlist dela somem."</summary>
    public string? ApagarAviso => ApagarAberto ? AvisoApagar : null;

    public Comando ConfirmarApagar { get; }

    public Comando CancelarApagar { get; }

    /// <summary>"Apagar": abre a folha. Na memória em uso, só o motivo ("Esta memória está em uso.").</summary>
    public void AbrirApagar(string id)
    {
        if (_setups.Memoria(id) is null)
            return;
        if (_setups.EmUso is { Tipo: TipoSomEmUso.MemoriaCelular } uso && uso.IdMemoria == id)
        {
            Mensagem = GerenteSetups.MsgMemoriaEmUso;
            return;
        }
        FecharCriar();
        FecharRenomear();
        _apagar = id;
        Mensagem = null;
        AvisarApagar();
    }

    private async Task ConfirmarApagarAsync()
    {
        if (_apagar is not { } id)
            return;
        ResultadoSetup? resultado = null;
        await Agir(async () =>
        {
            resultado = await _setups.ApagarMemoriaAsync(id);
            return resultado.Mensagem;
        }, depois: FecharApagar);
    }

    private void FecharApagar()
    {
        _apagar = null;
        AvisarApagar();
    }

    private void AvisarApagar()
    {
        Avisar(nameof(ApagarAberto));
        Avisar(nameof(ApagarTitulo));
        Avisar(nameof(ApagarAviso));
    }

    // ------------------------------------------------------------------ aplicado pela metade

    public bool PelaMetade => _copia.PelaMetade is not null;

    /// <summary>"Aplicar a memória FILME caiu no meio."</summary>
    public string? TextoPelaMetade => _copia.PelaMetade is { } op ? $"{op.Descricao} caiu no meio." : null;

    /// <summary>O botão branco: refaz a operação.</summary>
    public Comando Terminar { get; }

    /// <summary>O botão contornado: escreve de volta os ajustes que mudaram (chave "Ajustes de áudio").</summary>
    public Comando VoltarACopia { get; }

    private async Task TerminarAsync()
    {
        if (!PelaMetade)
            return;
        await Agir(async () =>
        {
            var r = await _copia.TerminarAsync();
            return r.Ok ? _setups.Aviso : r.Motivo;
        });
    }

    private async Task VoltarAsync()
    {
        if (!PelaMetade)
            return;
        if (!_chaves.Ligada(Chave.AjustesAudio))
        {
            Mensagem = ControleParametroViewModel.MsgChaveDesligada;
            return;
        }
        await Agir(async () =>
        {
            var r = await _copia.VoltarAsync();
            return r.Ok ? MsgVoltou : Frase(r.Motivo);
        });
    }

    // ------------------------------------------------------------------ apoio

    /// <summary>Roda a ação do núcleo e mostra a frase dela; a tela se atualiza na sua thread.</summary>
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

    private static string? Frase(string? motivo) =>
        string.IsNullOrEmpty(motivo) ? null : char.ToUpperInvariant(motivo[0]) + motivo[1..];

    private void AoMudarAlgo() => NaTela(Atualizar);

    private void AoMudarChave(Chave chave)
    {
        if (chave is Chave.AplicarMemoriaCelular or Chave.AjustesAudio)
            NaTela(Atualizar);
    }

    private void Atualizar()
    {
        var setup = SetupDaLista;
        var memorias = _setups.MemoriasDe(setup?.Id);
        var emUso = _setups.EmUso;
        var porId = _cartoes.ToDictionary(c => c.Id);
        var novos = memorias.Select(m => porId.GetValueOrDefault(m.Id) ?? new CartaoMemoriaCelularViewModel(m, this, Despachante, Relogio)).ToArray();
        foreach (var (cartao, memoria) in novos.Zip(memorias))
        {
            cartao.Nome = memoria.Nome;
            cartao.TextoPreset = _setups.PresetDe(memoria) is { } preset ? TextoPresetDe(preset.Nome) : null;
            cartao.EmUso = emUso.Tipo == TipoSomEmUso.MemoriaCelular && emUso.IdMemoria == memoria.Id;
        }
        var mudouLista = !novos.SequenceEqual(_cartoes);
        _cartoes = novos;

        if (mudouLista)
            Avisar(nameof(Cartoes));
        Avisar(nameof(TituloCelular));
        Avisar(nameof(TextoVazio));
        Avisar(nameof(AvisoSomDiferente));
        Avisar(nameof(MotivoTravaCelular));
        Avisar(nameof(MotivoTravaAplicar));
        Avisar(nameof(MotivoTravaCriar));
        Avisar(nameof(PodeAplicar));
        Avisar(nameof(PodeCriar));
        Avisar(nameof(MotivoTravaNovaMemoria));
        Avisar(nameof(PodeNovaMemoria));
        Avisar(nameof(Ocupado));
        Avisar(nameof(PelaMetade));
        Avisar(nameof(TextoPelaMetade));
        Avisar(nameof(RenomearTitulo));
        Avisar(nameof(ApagarTitulo));
    }

    public void Dispose()
    {
        _setups.Mudou -= AoMudarAlgo;
        _copia.Mudou -= AoMudarAlgo;
        _sessao.Mudou -= AoMudarAlgo;
        _chaves.Mudou -= AoMudarChave;
        _estado.Recarregou -= AoMudarAlgo;
        if (_criacao is not null)
            _criacao.Mudou -= AoMudarAlgo;
    }
}
