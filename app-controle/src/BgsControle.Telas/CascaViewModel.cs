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
/// troca, e a folha "Aplicado pela metade" ao reconectar com a marca. Acima delas (história 11), as camadas
/// que sobem sozinhas: a IA ativa quando a IA começa uma tarefa, a folha de confirmação de um pedido de risco
/// da IA e, no topo, a faixa "DESLIGUE O MÓDULO AGORA" do gerador.
/// </summary>
public sealed class CascaViewModel : TelaBase, IDisposable
{
    private readonly Sessao _sessao;
    private readonly SetupViewModel _setup;
    private readonly MemoriasViewModel _memorias;
    private readonly INavegacao _nav;
    private Aba _aba = Aba.Inicio;
    private (bool Setup, bool Metade)? _porCima;
    private readonly SalvarViewModel _salvar;
    private readonly LinkViewModel _link;
    private readonly FabricaControles _fabrica;
    private readonly IaAtivaViewModel? _iaAtiva;
    private readonly ConfirmacaoIaViewModel? _confirmacao;
    private readonly GeradorViewModel? _gerador;
    private (bool Ia, bool Confirmacao, bool Alarme)? _camadas;

    public CascaViewModel(
        Sessao sessao,
        ConexaoViewModel conexao,
        SetupViewModel setup,
        MemoriasViewModel memorias,
        InicioViewModel inicio,
        MesaViewModel mesa,
        AudioViewModel audio,
        SalvarViewModel salvar,
        LinkViewModel link,
        FabricaControles fabrica,
        INavegacao nav,
        IDespachante despachante,
        TimeProvider relogio,
        PlayerViewModel? player = null,
        IaAtivaViewModel? iaAtiva = null,
        ConfirmacaoIaViewModel? confirmacaoIa = null,
        GeradorViewModel? gerador = null)
        : base(despachante, relogio)
    {
        Player = player;
        _iaAtiva = iaAtiva;
        _confirmacao = confirmacaoIa;
        _gerador = gerador;
        Inicio = inicio;
        Mesa = mesa;
        Audio = audio;
        _sessao = sessao;
        _setup = setup;
        _memorias = memorias;
        _nav = nav;
        _salvar = salvar;
        _link = link;
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
        if (_iaAtiva is not null)
            _iaAtiva.PropertyChanged += AoMudarCamadas;
        if (_confirmacao is not null)
            _confirmacao.PropertyChanged += AoMudarCamadas;
        if (_gerador is not null)
            _gerador.PropertyChanged += AoMudarCamadas;
    }

    /// <summary>Os destinos da barra (cada um guarda o próprio estado).</summary>
    public InicioViewModel Inicio { get; }

    public MesaViewModel Mesa { get; }

    public AudioViewModel Audio { get; }

    /// <summary>A aba Player (a playlist da memória do celular em uso), ou null.</summary>
    public PlayerViewModel? Player { get; }

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
            if (_link.FolhaAberta)
                _link.CancelarFolha.Execute(null);
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

    /// <summary>A IA ativa está por cima (abre sozinha quando a IA começa e fecha ao parar).</summary>
    public bool IaAtivaPorCima => _camadas?.Ia == true;

    /// <summary>A folha de confirmação de um pedido da IA está por cima.</summary>
    public bool ConfirmacaoIaPorCima => _camadas?.Confirmacao == true;

    /// <summary>A faixa "DESLIGUE O MÓDULO AGORA" está por cima de tudo.</summary>
    public bool AlarmePorCima => _camadas?.Alarme == true;

    /// <summary>
    /// Confere as camadas da história 11 e pede à navegação quando mudam (ou sempre, com
    /// <paramref name="forcar"/>). A ordem, de baixo para cima: a IA ativa, a confirmação da IA e o alarme
    /// do gerador; as duas últimas ficam acima de tudo, até da escolha do Setup.
    /// </summary>
    public void SincronizarCamadas(bool forcar = false)
    {
        var agora = (_iaAtiva?.Ativa == true, _confirmacao?.FolhaAberta == true, _gerador?.Alarme == true);
        if (_camadas == agora && !forcar)
            return;
        _camadas = agora;
        _nav.MostrarCamadas(agora.Item1, agora.Item2, agora.Item3);
        Avisar(nameof(IaAtivaPorCima));
        Avisar(nameof(ConfirmacaoIaPorCima));
        Avisar(nameof(AlarmePorCima));
    }

    private void AoMudarCamadas(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(IaAtivaViewModel.Ativa) or nameof(ConfirmacaoIaViewModel.FolhaAberta) or nameof(GeradorViewModel.Alarme))
            SincronizarCamadas();
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
        if (_iaAtiva is not null)
            _iaAtiva.PropertyChanged -= AoMudarCamadas;
        if (_confirmacao is not null)
            _confirmacao.PropertyChanged -= AoMudarCamadas;
        if (_gerador is not null)
            _gerador.PropertyChanged -= AoMudarCamadas;
    }
}
