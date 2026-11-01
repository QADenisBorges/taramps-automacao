using BgsControle.Nucleo;
using BgsControle.Nucleo.Setups;

namespace BgsControle.Telas;

/// <summary>Uma linha da lista de Setups: nome, quantas memórias, em uso, Renomear e Apagar.</summary>
public sealed class LinhaSetupViewModel : TelaBase
{
    internal LinhaSetupViewModel(Setup setup, int memorias, bool emUso, SetupsViewModel tela, IDespachante despachante, TimeProvider relogio)
        : base(despachante, relogio)
    {
        Id = setup.Id;
        Nome = setup.Nome;
        EmUso = emUso;
        Detalhe = (emUso ? "Em uso · " : "") + (memorias == 1 ? "1 memória" : $"{memorias} memórias");
        MotivoTravaApagar = emUso ? GerenteSetups.MsgSetupEmUso : null;
        Abrir = new Comando(() => tela.AbrirSetup(Id));
        Renomear = new Comando(() => tela.AbrirRenomear(Id));
        Apagar = new Comando(() => tela.AbrirApagar(Id));
    }

    public string Id { get; }

    public string Nome { get; }

    /// <summary>"Em uso · 2 memórias".</summary>
    public string Detalhe { get; }

    /// <summary>O Setup em uso fica em branco.</summary>
    public bool EmUso { get; }

    /// <summary>"Este Setup está em uso.", ou null.</summary>
    public string? MotivoTravaApagar { get; }

    public bool PodeApagar => MotivoTravaApagar is null;

    /// <summary>Tocar no Setup abre a tela Setup (os equipamentos dele).</summary>
    public Comando Abrir { get; }

    public Comando Renomear { get; }

    public Comando Apagar { get; }
}

/// <summary>
/// Configurações › Setups (EXPERIENCE, Information Architecture): os Setups; renomear (o campo vale no
/// ✓) e apagar com folha ("Apagar o Setup Carro?" + "O Setup, as memórias e as playlists dele
/// somem."). O Setup em uso não se apaga. Sem nenhum Setup, o texto de vazio diz como criar. Criar e
/// trocar de Setup só pela tela Trocar de Setup ou Setup em dúvida. Nada aqui manda algo ao aparelho.
/// </summary>
public sealed class SetupsViewModel : TelaBase, IDisposable
{
    public const string AvisoApagar = "O Setup, as memórias e as playlists dele somem.";
    public const string Lembrete = "Para criar ou trocar de Setup, toque no nome do Setup no Início.";

    /// <summary>Sem nenhum Setup (o app nasce zerado).</summary>
    public const string TextoVazio = "Nenhum Setup. Conecte o aparelho e toque em \"Sem Setup ›\" no Início para criar.";

    private readonly GerenteSetups _setups;
    private readonly Sessao _sessao;
    private readonly INavegacao _nav;

    private IReadOnlyList<LinhaSetupViewModel> _linhas = [];
    private string? _renomear;
    private string _nomeRenomear = "";
    private string? _erroRenomear;
    private string? _apagar;
    private string? _mensagem;
    private bool _agindo;

    public SetupsViewModel(GerenteSetups setups, Sessao sessao, INavegacao nav, IDespachante despachante, TimeProvider relogio)
        : base(despachante, relogio)
    {
        _setups = setups;
        _sessao = sessao;
        _nav = nav;
        ConfirmarRenomear = new Comando(ConfirmarRenomearAsync);
        CancelarRenomear = new Comando(FecharRenomear);
        ConfirmarApagar = new Comando(ConfirmarApagarAsync);
        CancelarApagar = new Comando(FecharApagar);
        Voltar = new Comando(nav.Voltar);
        _setups.Mudou += AoMudar;
        _sessao.Mudou += AoMudar;
        Atualizar();
    }

    /// <summary>Abre a tela Setup de um Setup (Configurações › Setups › tocar num Setup).</summary>
    internal void AbrirSetup(string id) => _nav.AbrirSetup(id);

    public IReadOnlyList<LinhaSetupViewModel> Linhas => _linhas;

    public LinhaSetupViewModel? Linha(string nome) =>
        _linhas.FirstOrDefault(l => string.Equals(l.Nome, nome, StringComparison.OrdinalIgnoreCase));

    public string TextoLembrete => Lembrete;

    /// <summary>"Nenhum Setup. Conecte o aparelho…" sem nenhum Setup; senão null.</summary>
    public string? Vazio => _linhas.Count == 0 ? TextoVazio : null;

    /// <summary>O lembrete de como criar ou trocar aparece só com algum Setup (sem nenhum, vale o <see cref="Vazio"/>).</summary>
    public bool MostraLembrete => _linhas.Count > 0;

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

    public Comando Voltar { get; }

    // ------------------------------------------------------------------ renomear

    public bool RenomearAberto => _renomear is not null;

    /// <summary>"Renomear o Casa 2.1".</summary>
    public string? RenomearTitulo => _renomear is { } id && _setups.Setup(id) is { } s ? $"Renomear o {s.Nome}" : null;

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

    public void AbrirRenomear(string id)
    {
        if (_setups.Setup(id) is not { } setup)
            return;
        FecharApagar();
        _renomear = id;
        NomeRenomear = setup.Nome;
        ErroRenomear = null;
        Mensagem = null;
        Avisar(nameof(RenomearAberto));
        Avisar(nameof(RenomearTitulo));
    }

    private async Task ConfirmarRenomearAsync()
    {
        if (_agindo || _renomear is not { } id)
            return; // um segundo toque com o primeiro rodando não faz nada
        ResultadoSetup? r = null;
        await Agir(async () =>
        {
            r = await _setups.RenomearSetupAsync(id, NomeRenomear);
            return r.Mensagem;
        }, depois: () =>
        {
            if (r is { Ok: true })
                FecharRenomear();
            else
                ErroRenomear = r?.Mensagem;
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

    /// <summary>"Apagar o Setup Carro?"</summary>
    public string? ApagarTitulo => _apagar is { } id && _setups.Setup(id) is { } s ? $"Apagar o Setup {s.Nome}?" : null;

    public string? ApagarAvisoTexto => ApagarAberto ? AvisoApagar : null;

    public Comando ConfirmarApagar { get; }

    public Comando CancelarApagar { get; }

    /// <summary>Abre a folha. No Setup em uso, só o motivo.</summary>
    public void AbrirApagar(string id)
    {
        if (Linhas.FirstOrDefault(l => l.Id == id) is not { } linha)
            return;
        if (linha.MotivoTravaApagar is { } motivo)
        {
            Mensagem = motivo;
            return;
        }
        FecharRenomear();
        _apagar = id;
        Mensagem = null;
        AvisarApagar();
    }

    private async Task ConfirmarApagarAsync()
    {
        if (_agindo || _apagar is not { } id)
            return; // um segundo toque com o primeiro rodando não faz nada
        await Agir(async () => (await _setups.ApagarSetupAsync(id)).Mensagem, depois: FecharApagar);
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
        Avisar(nameof(ApagarAvisoTexto));
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
        var emUso = _setups.SetupEmUso?.Id;
        _linhas = _setups.Setups
            .Select(s => new LinhaSetupViewModel(s, _setups.MemoriasDe(s.Id).Count, s.Id == emUso, this, Despachante, Relogio))
            .ToArray();
        Avisar(nameof(Linhas));
        Avisar(nameof(Vazio));
        Avisar(nameof(MostraLembrete));
        Avisar(nameof(Ocupado));
        Avisar(nameof(RenomearTitulo));
        Avisar(nameof(ApagarTitulo));
    }

    public void Dispose()
    {
        _setups.Mudou -= AoMudar;
        _sessao.Mudou -= AoMudar;
    }
}

/// <summary>
/// Configurações (a engrenagem; EXPERIENCE, Information Architecture): os grupos SOM (Setups, Memórias,
/// Presets), APARELHO (remote out e Bluetooth de áudio na própria linha, o nome do produto que vale no ✓,
/// a chave "Controle pela rede" com o código embaixo, Senha › e Voltímetro e clip ›), ACESSO (Chaves de
/// acesso) e o Avançado recolhido, com o reset de áudio e o reset de fábrica. O grupo Aparelho é o
/// <see cref="AparelhoViewModel"/>; a chave da porta, o <see cref="ControlePelaRedeViewModel"/>. A tela
/// IA pela rede e o Histórico saíram (decisão do Denis, 30/10/2026): o histórico é lido pela porta. Do app: a
/// chave "Animação ao criar o som" (<see cref="PreferenciasApp"/>, história 10d), no par Desl./Lig.
/// </summary>
public sealed class ConfiguracoesViewModel : TelaBase
{
    public const string TituloAnimacaoTexto = "Animação ao criar o som";
    public const string DescricaoAnimacaoTexto =
        "Mostra cada ajuste na tela, um por vez, enquanto a memória é montada. Desligada, aparece só o \"Aplicando\".";
    public const string MsgNaoGuardouPreferencia = "Não consegui guardar a escolha. Tente de novo.";

    private readonly PreferenciasApp? _preferencias;
    private bool _avancadoAberto;
    private string? _erroAnimacao;

    public ConfiguracoesViewModel(
        INavegacao nav,
        IDespachante despachante,
        TimeProvider relogio,
        AparelhoViewModel? aparelho = null,
        ControlePelaRedeViewModel? controlePelaRede = null,
        PreferenciasApp? preferencias = null)
        : base(despachante, relogio)
    {
        Aparelho = aparelho;
        ControlePelaRede = controlePelaRede;
        _preferencias = preferencias;
        LigarAnimacao = new Comando(() => DefinirAnimacaoAsync(true));
        DesligarAnimacao = new Comando(() => DefinirAnimacaoAsync(false));
        if (_preferencias is not null)
            _preferencias.Mudou += () => NaTela(AvisarAnimacao);
        AbrirSetups = new Comando(nav.AbrirSetups);
        AbrirMemorias = new Comando(nav.AbrirMemorias);
        AbrirPresets = new Comando(nav.AbrirPresets);
        AbrirSenha = new Comando(nav.AbrirSenha);
        AbrirMonitoracao = new Comando(nav.AbrirMonitoracao);
        AbrirChaves = new Comando(nav.AbrirChaves);
        AbrirCalibracao = new Comando(() => nav.AbrirCalibracao(null));
        AlternarAvancado = new Comando(() => AvancadoAberto = !AvancadoAberto);
        Voltar = new Comando(nav.Voltar);
    }

    public string TituloSetups => "Setups";
    public string DescricaoSetups => "Ver, renomear e apagar";
    public string TituloMemorias => "Memórias";
    public string DescricaoMemorias => "Do celular e do aparelho";
    public string TituloPresets => "Presets";
    public string DescricaoPresets => "15 bandas: aplicar, renomear, apagar, exportar";
    public string TituloSenha => "Senha";
    public string DescricaoSenha => "Trocar a senha do aparelho";
    public string TituloMonitoracao => "Voltímetro e clip";
    public string DescricaoMonitoracao => "Tensão e clip ao vivo";
    public string TituloChaves => "Chaves de acesso";
    public string DescricaoChaves => "Ligar e desligar as chaves de cada operação";

    /// <summary>A calibração guiada (história 13g; [ASSUMPTION] "Ajustes › Medir › Calibrar" vira Configurações até a Roda existir).</summary>
    public const string TituloCalibrarTexto = "Calibrar saídas";

    public const string DescricaoCalibrarTexto = "O ganho do módulo e o nível do Bluetooth, com o multímetro";

    public string TituloCalibrar => TituloCalibrarTexto;
    public string DescricaoCalibrar => DescricaoCalibrarTexto;

    /// <summary>O grupo Aparelho (remote out, Bluetooth de áudio, nome, resets), ou null.</summary>
    public AparelhoViewModel? Aparelho { get; }

    /// <summary>A chave "Controle pela rede" no grupo APARELHO, com o código embaixo, ou null.</summary>
    public ControlePelaRedeViewModel? ControlePelaRede { get; }

    public Comando AbrirSetups { get; }
    public Comando AbrirMemorias { get; }
    public Comando AbrirPresets { get; }
    public Comando AbrirSenha { get; }
    public Comando AbrirMonitoracao { get; }
    public Comando AbrirChaves { get; }

    /// <summary>"Calibrar saídas": a calibração guiada, na escolha da saída.</summary>
    public Comando AbrirCalibracao { get; }

    /// <summary>O Avançado (reset de áudio e de fábrica) vem recolhido; lembra enquanto o app está aberto.</summary>
    public bool AvancadoAberto
    {
        get => _avancadoAberto;
        private set
        {
            if (_avancadoAberto == value)
                return;
            _avancadoAberto = value;
            Avisar();
            Avisar(nameof(SetaAvancado));
        }
    }

    /// <summary>"⌄" recolhido, "⌃" aberto.</summary>
    public string SetaAvancado => AvancadoAberto ? "⌃" : "⌄";

    public Comando AlternarAvancado { get; }

    public Comando Voltar { get; }

    // ------------------------------------------------------------------ do app (história 10d)

    /// <summary>A linha "Animação ao criar o som" existe (com as preferências do app).</summary>
    public bool ComPreferencias => _preferencias is not null;

    public string TituloAnimacao => TituloAnimacaoTexto;

    public string DescricaoAnimacao => DescricaoAnimacaoTexto;

    /// <summary>A animação está ligada (o padrão).</summary>
    public bool AnimacaoLigada => _preferencias?.AnimacaoCriacao ?? true;

    /// <summary>"Ligada" ou "Desligada" (o TalkBack do par).</summary>
    public string TextoEstadoAnimacao => AnimacaoLigada ? "Ligada" : "Desligada";

    /// <summary>Não conseguiu guardar a escolha, ou null.</summary>
    public string? ErroAnimacao
    {
        get => _erroAnimacao;
        private set
        {
            if (_erroAnimacao == value)
                return;
            _erroAnimacao = value;
            Avisar();
        }
    }

    public Comando LigarAnimacao { get; }

    public Comando DesligarAnimacao { get; }

    private async Task DefinirAnimacaoAsync(bool ligada)
    {
        if (_preferencias is null)
            return;
        var ok = await _preferencias.DefinirAnimacaoCriacaoAsync(ligada);
        NaTela(() =>
        {
            ErroAnimacao = ok ? null : MsgNaoGuardouPreferencia;
            AvisarAnimacao();
        });
    }

    private void AvisarAnimacao()
    {
        Avisar(nameof(AnimacaoLigada));
        Avisar(nameof(TextoEstadoAnimacao));
    }
}
