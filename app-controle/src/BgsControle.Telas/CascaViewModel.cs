using System.ComponentModel;
using BgsControle.Nucleo;

namespace BgsControle.Telas;

/// <summary>Os destinos da barra inferior: Início · Mesa · Áudio · Player (o Player entrou na história 11).</summary>
public enum Aba
{
    Inicio,
    Mesa,
    Audio,
    Player,
}

/// <summary>O ponto da pílula de status.</summary>
public enum PontoStatus
{
    Desconectado,
    Esperando,
    Conectado,
}

/// <summary>
/// A casca do app (EXPERIENCE, Information Architecture): a barra inferior (Início · Mesa · Áudio · Player),
/// que troca de destino sem perder o estado de cada um; a pílula de status ("Conectado · R003",
/// "Conectando · senha", "Desconectado", "Só leitura · firmware R004"); a engrenagem; e as telas que
/// sobem sozinhas por cima de tudo: a escolha do Setup quando o <c>GerenteSetups</c> entra em dúvida ou
/// troca, e a folha "Aplicado pela metade" ao reconectar com a marca. Acima delas, as camadas que sobem
/// sozinhas: a folha de risco do app (um pedido de risco pela porta, ou o Continuar), a tela toda
/// "REAPLICANDO" da conferência ao conectar e, no topo, a faixa "DESLIGUE O MÓDULO AGORA" do gerador.
/// História 10d: a criação da memória em tela toda, logo abaixo da folha de risco, e o aviso rápido de 2,6 s
/// ("Memória MUSICA ORIG criada no Casa 2.1.") quando ela fecha.
/// Nenhuma tela mostra a IA (decisão do Denis, 30/10/2026).
/// </summary>
public sealed class CascaViewModel : TelaBase, IDisposable
{
    /// <summary>Quanto o aviso rápido fica no pé (DESIGN, motion.toast).</summary>
    public static readonly TimeSpan TempoAviso = TimeSpan.FromSeconds(2.6);

    private readonly Sessao _sessao;
    private readonly SetupViewModel _setup;
    private readonly MemoriasViewModel _memorias;
    private readonly INavegacao _nav;
    private Aba _aba = Aba.Inicio;
    private (bool Setup, bool Metade)? _porCima;
    private readonly SalvarViewModel _salvar;
    private readonly FabricaControles _fabrica;
    private readonly FolhaRiscoViewModel? _folhaRisco;
    private readonly GeradorViewModel? _gerador;
    private (bool Criacao, bool FolhaRisco, bool Reaplicando, bool Alarme)? _camadas;
    private string? _avisoRapido;
    private ITimer? _prazoAviso;

    public CascaViewModel(
        Sessao sessao,
        ConexaoViewModel conexao,
        SetupViewModel setup,
        MemoriasViewModel memorias,
        InicioViewModel inicio,
        MesaViewModel mesa,
        AudioViewModel audio,
        SalvarViewModel salvar,
        FabricaControles fabrica,
        INavegacao nav,
        IDespachante despachante,
        TimeProvider relogio,
        PlayerViewModel? player = null,
        FolhaRiscoViewModel? folhaRisco = null,
        GeradorViewModel? gerador = null,
        ReaplicandoViewModel? reaplicando = null,
        CriacaoMemoriaViewModel? criacao = null)
        : base(despachante, relogio)
    {
        Criacao = criacao;
        Player = player;
        _folhaRisco = folhaRisco;
        _gerador = gerador;
        Reaplicando = reaplicando;
        Inicio = inicio;
        Mesa = mesa;
        Audio = audio;
        _sessao = sessao;
        _setup = setup;
        _memorias = memorias;
        _nav = nav;
        _salvar = salvar;
        _fabrica = fabrica;
        IrInicio = new Comando(() => Ir(Aba.Inicio));
        IrMesa = new Comando(() => Ir(Aba.Mesa));
        IrAudio = new Comando(() => Ir(Aba.Audio));
        IrPlayer = new Comando(() => Ir(Aba.Player));
        AbrirConfiguracoes = new Comando(nav.AbrirConfiguracoes);
        TocarPilula = new Comando(nav.AbrirConexao);

        _sessao.Mudou += AoMudarSessao;
        _setup.PropertyChanged += AoMudarTelas;
        _memorias.PropertyChanged += AoMudarTelas;
        if (_folhaRisco is not null)
            _folhaRisco.PropertyChanged += AoMudarCamadas;
        if (Reaplicando is not null)
            Reaplicando.PropertyChanged += AoMudarCamadas;
        if (_gerador is not null)
            _gerador.PropertyChanged += AoMudarCamadas;
        if (Criacao is not null)
            Criacao.PropertyChanged += AoMudarCriacao;
        // História 13g: a recusa da cerca e o aviso do volume do Extra também vão ao aviso rápido.
        _fabrica.RecusouNaCerca += AoAvisar;
        if (Player is not null)
            Player.AvisoRapido += AoAvisar;
    }

    private void AoAvisar(string texto) => NaTela(() => MostrarAviso(texto));

    /// <summary>A criação da memória em tela toda (história 10d), ou null.</summary>
    public CriacaoMemoriaViewModel? Criacao { get; }

    /// <summary>Os destinos da barra (cada um guarda o próprio estado).</summary>
    public InicioViewModel Inicio { get; }

    public MesaViewModel Mesa { get; }

    public AudioViewModel Audio { get; }

    /// <summary>A aba Player (a playlist da memória do celular em uso), ou null.</summary>
    public PlayerViewModel? Player { get; }

    /// <summary>A tela toda "REAPLICANDO" (os textos dela), ou null.</summary>
    public ReaplicandoViewModel? Reaplicando { get; }

    /// <summary>Os botões de memória e a lista "…" (a folha sobe por cima da casca).</summary>
    public BotoesMemoriaViewModel Memorias => Inicio.Memorias;

    // ------------------------------------------------------------------ barra inferior

    public Aba Aba => _aba;

    public bool NoInicio => _aba == Aba.Inicio;
    public bool NaMesa => _aba == Aba.Mesa;
    public bool NoAudio => _aba == Aba.Audio;
    public bool NoPlayer => _aba == Aba.Player;

    public Comando IrInicio { get; }
    public Comando IrMesa { get; }
    public Comando IrAudio { get; }
    public Comando IrPlayer { get; }

    public void Ir(Aba aba)
    {
        if (_aba == aba)
            return;
        _aba = aba;
        Avisar(nameof(Aba));
        Avisar(nameof(NoInicio));
        Avisar(nameof(NaMesa));
        Avisar(nameof(NoAudio));
        Avisar(nameof(NoPlayer));
    }

    // ------------------------------------------------------------------ topo

    /// <summary>A engrenagem: Configurações.</summary>
    public Comando AbrirConfiguracoes { get; }

    /// <summary>"Conectado · R003", "Conectando", "Conectando · senha", "Desconectado", "Só leitura · firmware R004".</summary>
    public string TextoStatus => _sessao.Estado switch
    {
        EstadoSessao.Conectado when _sessao.SoLeitura => $"Só leitura · firmware {_sessao.Firmware}",
        EstadoSessao.Conectado => $"Conectado · {_sessao.Firmware}",
        EstadoSessao.PedindoSenha => "Conectando · senha",
        EstadoSessao.Desconectado => "Desconectado",
        _ => "Conectando",
    };

    public PontoStatus Ponto => _sessao.Estado switch
    {
        EstadoSessao.Conectado => PontoStatus.Conectado,
        EstadoSessao.Desconectado => PontoStatus.Desconectado,
        _ => PontoStatus.Esperando,
    };

    public bool PontoConectado => Ponto == PontoStatus.Conectado;
    public bool PontoEsperando => Ponto == PontoStatus.Esperando;
    public bool PontoDesconectado => Ponto == PontoStatus.Desconectado;

    /// <summary>
    /// Abre a tela de conexão: desconectado, com Conectar (a senha aparece ali, e não no Início
    /// escondido), o registro e Esquecer a senha; conectado, os detalhes.
    /// </summary>
    public Comando TocarPilula { get; }

    // ------------------------------------------------------------------ por cima de tudo

    /// <summary>A escolha do Setup está por cima (Setup em dúvida ou Trocar de Setup).</summary>
    public bool EscolhaSetupPorCima => _porCima?.Setup == true;

    /// <summary>A folha "Aplicado pela metade" está por cima.</summary>
    public bool PelaMetadePorCima => _porCima?.Metade == true;

    /// <summary>
    /// Confere o que deve ficar por cima e pede à navegação quando muda (ou sempre, com
    /// <paramref name="forcar"/>: a casca apareceu e a navegação pode não ter conseguido antes). Quando a
    /// escolha do Setup sai de cima (escolheu, criou, seguiu sem Setup ou desistiu), a casca volta ao
    /// Início: as telas cheias de antes fecham, porque as saídas e a memória podem ter mudado.
    /// </summary>
    public void Sincronizar(bool forcar = false)
    {
        var agora = (_setup.Aberta, _memorias.PelaMetade);
        if (_porCima == agora && !forcar)
            return;
        var saiuDaEscolha = _porCima?.Setup == true && !agora.Item1;
        _porCima = agora;
        if (saiuDaEscolha)
        {
            // As folhas e os campos abertos eram do Setup de antes: fecham, sem guardar nem mandar nada.
            if (_salvar.Aberta)
                _salvar.Cancelar.Execute(null);
            if (Memorias.ListaAberta)
                Memorias.FecharLista.Execute(null);
            _fabrica.FecharCampos();
            _nav.VoltarAoInicio();
            Ir(Aba.Inicio);
        }
        _nav.MostrarPorCima(agora.Item1, agora.Item2);
        Avisar(nameof(EscolhaSetupPorCima));
        Avisar(nameof(PelaMetadePorCima));
    }

    /// <summary>A criação da memória está por cima (abaixo da folha de risco).</summary>
    public bool CriacaoPorCima => _camadas?.Criacao == true;

    /// <summary>A folha de risco do app está por cima (um pedido de risco pela porta, ou o Continuar).</summary>
    public bool FolhaRiscoPorCima => _camadas?.FolhaRisco == true;

    /// <summary>A tela toda "REAPLICANDO" está por cima (a conferência reaplica a memória ativa).</summary>
    public bool ReaplicandoPorCima => _camadas?.Reaplicando == true;

    /// <summary>A faixa "DESLIGUE O MÓDULO AGORA" está por cima de tudo.</summary>
    public bool AlarmePorCima => _camadas?.Alarme == true;

    /// <summary>
    /// Confere as camadas e pede à navegação quando mudam (ou sempre, com <paramref name="forcar"/>). A
    /// ordem, de baixo para cima: a criação da memória, a folha de risco, o "REAPLICANDO" e o alarme do
    /// gerador; todas ficam acima da escolha do Setup e da folha pela metade.
    /// </summary>
    public void SincronizarCamadas(bool forcar = false)
    {
        var agora = (Criacao?.Aberta == true, _folhaRisco?.FolhaAberta == true, Reaplicando?.Ativa == true, _gerador?.Alarme == true);
        if (_camadas == agora && !forcar)
            return;
        _camadas = agora;
        _nav.MostrarCamadas(agora.Item1, agora.Item2, agora.Item3, agora.Item4);
        Avisar(nameof(CriacaoPorCima));
        Avisar(nameof(FolhaRiscoPorCima));
        Avisar(nameof(ReaplicandoPorCima));
        Avisar(nameof(AlarmePorCima));
    }

    private void AoMudarCamadas(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(FolhaRiscoViewModel.FolhaAberta) or nameof(ReaplicandoViewModel.Ativa) or nameof(GeradorViewModel.Alarme))
            SincronizarCamadas();
    }

    // ------------------------------------------------------------------ aviso rápido (história 10d)

    /// <summary>O aviso rápido no pé ("Memória MUSICA ORIG criada no Casa 2.1."), por 2,6 s; ou null.</summary>
    public string? AvisoRapido
    {
        get => _avisoRapido;
        private set
        {
            if (_avisoRapido == value)
                return;
            _avisoRapido = value;
            Avisar();
        }
    }

    /// <summary>Mostra o aviso por <see cref="TempoAviso"/>; um aviso novo recomeça a conta.</summary>
    public void MostrarAviso(string texto)
    {
        _prazoAviso?.Dispose();
        AvisoRapido = texto;
        ITimer? prazo = null;
        prazo = Relogio.CreateTimer(_ => NaTela(() =>
        {
            if (!ReferenceEquals(_prazoAviso, prazo))
                return;
            _prazoAviso?.Dispose();
            _prazoAviso = null;
            AvisoRapido = null;
        }), null, TempoAviso, Timeout.InfiniteTimeSpan);
        _prazoAviso = prazo;
    }

    private void AoMudarCriacao(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CriacaoMemoriaViewModel.Aberta))
            SincronizarCamadas();
        else if (e.PropertyName == nameof(CriacaoMemoriaViewModel.AvisoFinal) && Criacao?.AvisoFinal is { Length: > 0 } aviso)
            MostrarAviso(aviso);
    }

    private void AoMudarTelas(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SetupViewModel.Aberta) or nameof(MemoriasViewModel.PelaMetade))
            Sincronizar();
    }

    private void AoMudarSessao() => NaTela(() =>
    {
        Avisar(nameof(TextoStatus));
        Avisar(nameof(Ponto));
        Avisar(nameof(PontoConectado));
        Avisar(nameof(PontoEsperando));
        Avisar(nameof(PontoDesconectado));
    });

    public void Dispose()
    {
        _sessao.Mudou -= AoMudarSessao;
        _setup.PropertyChanged -= AoMudarTelas;
        _memorias.PropertyChanged -= AoMudarTelas;
        if (_folhaRisco is not null)
            _folhaRisco.PropertyChanged -= AoMudarCamadas;
        if (Reaplicando is not null)
            Reaplicando.PropertyChanged -= AoMudarCamadas;
        if (_gerador is not null)
            _gerador.PropertyChanged -= AoMudarCamadas;
        if (Criacao is not null)
            Criacao.PropertyChanged -= AoMudarCriacao;
        _fabrica.RecusouNaCerca -= AoAvisar;
        if (Player is not null)
            Player.AvisoRapido -= AoAvisar;
        _prazoAviso?.Dispose();
    }
}
