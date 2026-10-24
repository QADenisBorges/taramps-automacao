using System.ComponentModel;
using BgsControle.Nucleo;
using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Memorias;
using BgsControle.Nucleo.Setups;

namespace BgsControle.Telas;

/// <summary>
/// O cartão de uma saída do Setup no Início: nome na cor do canal mestre, "CH1 · rota B", o ganho lido,
/// o resumo em duas linhas, o aviso do bridge e "Ajustar ›". Sem leitura, "—": nunca um valor que não
/// veio do aparelho.
/// </summary>
public sealed class CartaoSaidaViewModel : TelaBase
{
    private readonly EstadoAparelho _estado;

    internal CartaoSaidaViewModel(SaidaTela saida, EstadoAparelho estado, INavegacao nav, IDespachante despachante, TimeProvider relogio)
        : base(despachante, relogio)
    {
        Saida = saida;
        _estado = estado;
        Ajustar = new Comando(() => nav.AbrirAjuste(saida.Canal));
    }

    public SaidaTela Saida { get; }

    /// <summary>O canal mestre (0 a 3): a cor e onde se lê.</summary>
    public int Canal => Saida.Canal;

    public string Nome => Saida.Nome;

    /// <summary>"CH1 · rota B", "CH3 + CH4 · rota A+B".</summary>
    public string Subtitulo => $"{Saida.Rotulo} · rota {TextosParametro.Rota(_estado, Canal)}";

    /// <summary>O aviso do bridge sem o link, ou null.</summary>
    public string? Aviso => Saida.Aviso;

    /// <summary>O ganho lido ("+10,0"), ou "—".</summary>
    public string Ganho => TextosParametro.Curto(_estado, "081", Canal);

    public string UnidadeGanho => "dB";

    /// <summary>Tem valor lido (a barra na cor cheia; sem leitura, a 40 %).</summary>
    public bool Lido => _estado.Valor("081", Canal) is not null;

    /// <summary>"PA 80 Hz · PB OFF".</summary>
    public string Resumo1 => $"PA {Filtro("040", "042")} · PB {Filtro("041", "043")}";

    /// <summary>"Lim −18,0 dB · Atraso 0,0 cm · Fase 0°".</summary>
    public string Resumo2
    {
        get
        {
            var lim = TextosParametro.Curto(_estado, "071", Canal);
            var limTexto = lim is "—" or "OFF" ? lim : $"{lim} dB";
            var atraso = TextosParametro.Curto(_estado, "050", Canal);
            var atrasoTexto = atraso == "—" ? atraso : $"{atraso} cm";
            return $"Lim {limTexto} · Atraso {atrasoTexto} · Fase {TextosParametro.Curto(_estado, "060", Canal)}";
        }
    }

    /// <summary>Abre o Ajuste desta saída.</summary>
    public Comando Ajustar { get; }

    private string Filtro(string freq, string tipo)
    {
        var f = TextosParametro.Filtro(_estado, freq, tipo, Canal);
        return f is "—" or "OFF" ? f : $"{f} Hz";
    }

    internal void Atualizar()
    {
        Avisar(nameof(Subtitulo));
        Avisar(nameof(Ganho));
        Avisar(nameof(Lido));
        Avisar(nameof(Resumo1));
        Avisar(nameof(Resumo2));
    }
}

/// <summary>
/// O Início (EXPERIENCE, Information Architecture; mock da 3b, quadro 1): o seletor de Setup, o cartão
/// da memória em uso com o chip de preset e os botões de memória, os cartões das saídas do Setup em uso
/// (<see cref="GerenteSetups.SaidasEmUso"/>, de 1 a 4, refeitos quando as saídas mudam) e, sem
/// conexão, o cartão de conexão com Conectar e as caixas da senha. No modo só leitura, o aviso fixo.
/// </summary>
public sealed class InicioViewModel : TelaBase, IDisposable
{
    public const string RotuloMemoria = "MEMÓRIA EM USO";
    public const string RotuloSaidas = "SAÍDAS";
    public const string DicaSaidas = "Toque para ajustar";
    public const string RotuloProcessador = "PROCESSADOR";
    public const string TextoDesconectado = "Desconectado";
    public const string FraseDesconectado = "Desconectado. Ligue o som e fique perto dele.";
    public const string AvisoSoLeitura = "Firmware diferente do R003. O app só lê; nada é enviado.";
    public const string TextoCriarMemoria = "Criar memória do som atual";
    public const string TextoReaplicar = "Reaplicar";

    private readonly GerenteSetups _setups;
    private readonly GerenteMemorias _memorias;
    private readonly Sessao _sessao;
    private readonly EstadoAparelho _estado;
    private readonly LinkCanais _link;
    private readonly ConexaoViewModel _conexao;
    private readonly SetupViewModel _setup;
    private readonly MemoriasViewModel _telaMemorias;
    private readonly INavegacao _nav;
    private IReadOnlyList<CartaoSaidaViewModel> _cartoes = [];

    private EstadoSessao _ultimoEstado;
    private DateTimeOffset? _caiu;
    private string? _mensagem;

    public InicioViewModel(
        GerenteSetups setups,
        GerenteMemorias memorias,
        Sessao sessao,
        EstadoAparelho estado,
        LinkCanais link,
        ConexaoViewModel conexao,
        SetupViewModel setup,
        MemoriasViewModel telaMemorias,
        BotoesMemoriaViewModel botoes,
        INavegacao nav,
        IDespachante despachante,
        TimeProvider relogio)
        : base(despachante, relogio)
    {
        _setups = setups;
        _memorias = memorias;
        _sessao = sessao;
        _estado = estado;
        _link = link;
        _conexao = conexao;
        _setup = setup;
        _telaMemorias = telaMemorias;
        Memorias = botoes;
        Conexao = conexao;
        _ultimoEstado = sessao.Estado;
        _nav = nav;
        RefazerSaidas();

        Reaplicar = new Comando(ReaplicarAsync);
        CriarMemoria = new Comando(async () =>
        {
            await _telaMemorias.AbrirCriar.ExecutarAsync();
            if (_telaMemorias.CriarAberto)
                nav.AbrirMemorias();
            else
                Mensagem = _telaMemorias.Mensagem;
        });
        TrocarSetup = new Comando(TrocarAsync);

        _setups.Mudou += AoMudar;
        _memorias.Mudou += AoMudar;
        _sessao.Mudou += AoMudarSessao;
        _estado.Recarregou += AoMudar;
        _estado.Mudou += AoMudarValor;
        _link.Mudou += AoMudarLink;
        _conexao.PropertyChanged += AoMudarConexao;
        _setup.PropertyChanged += AoMudarSetup;
        Atualizar();
    }

    // ------------------------------------------------------------------ conexão

    /// <summary>A tela de conexão (senha, leitura): o mesmo ViewModel da história 1.</summary>
    public ConexaoViewModel Conexao { get; }

    public bool Conectado => _sessao.Estado == EstadoSessao.Conectado;

    /// <summary>Sem conexão: o cartão de conexão no lugar do cartão da memória.</summary>
    public bool MostrarConexao => !Conectado;

    /// <summary>"Desconectado", "Conectando", "Senha".</summary>
    public string EstadoConexao => _sessao.Estado switch
    {
        EstadoSessao.Desconectado => TextoDesconectado,
        EstadoSessao.PedindoSenha => "Senha",
        _ => "Conectando",
    };

    /// <summary>A frase do cartão: a mensagem da sessão ("Senha errada. Digite de novo (tentativa 2 de 3).") ou a de ajuda.</summary>
    public string FraseConexao => _conexao.Mensagem ?? (_sessao.Estado == EstadoSessao.Desconectado ? FraseDesconectado : _conexao.Resumo);

    /// <summary>"Caiu às 21:14", depois de uma conexão que caiu; senão null.</summary>
    public string? HoraQueCaiu => _sessao.Estado == EstadoSessao.Desconectado && _caiu is { } t ? $"Caiu às {t:HH\\:mm}" : null;

    /// <summary>"Lendo o aparelho · 42 de 84" durante a leitura completa; senão null.</summary>
    public string? TextoLendo => Conectado && _conexao.LendoAgora ? $"Lendo o aparelho · {_conexao.PlacarMarco1}" : null;

    /// <summary>Firmware ≠ R003: "Firmware diferente do R003. O app só lê; nada é enviado."</summary>
    public string? AvisoFixo => Conectado && _sessao.SoLeitura ? AvisoSoLeitura : null;

    // ------------------------------------------------------------------ Setup

    /// <summary>"Setup Casa 2.1 ›" ou "Sem Setup ›".</summary>
    public string Seletor => _setup.Seletor;

    /// <summary>O rótulo pequeno "Setup", ou null com "Sem Setup".</summary>
    public string? SeletorRotulo => _setups.SetupEmUso is not null ? "Setup" : null;

    /// <summary>"Casa 2.1" ou "Sem Setup".</summary>
    public string SeletorNome => _setups.SetupEmUso?.Nome ?? SetupViewModel.SemSetup;

    /// <summary>Tocar no seletor: muta e abre "Trocar de Setup" (a casca abre a tela).</summary>
    public Comando TrocarSetup { get; }

    // ------------------------------------------------------------------ memória em uso

    private SomEmUsoTexto Som => SomEmUsoTexto.De(_setups, _memorias, _sessao);

    /// <summary>O cartão da memória em uso aparece (conectado, com o app sabendo o que dizer).</summary>
    public bool MostrarMemoria => Conectado && Som.Nome is not null;

    /// <summary>"FILME", "Som do aparelho", "Nenhuma memória"…</summary>
    public string? NomeMemoria => Som.Nome;

    /// <summary>"Memória 2 do aparelho (app da Taramps)", "O app não sabe qual memória está tocando.", ou null.</summary>
    public string? LinhaMemoria => Som.Linha;

    /// <summary>O chip: o nome do preset lembrado ("Cinema"), ou null (sem chip). A folha de presets é da história 11.</summary>
    public string? NomePreset => Som.NomePreset;

    public bool MostrarChip => NomePreset is not null;

    /// <summary>"Preset: Cinema".</summary>
    public string? TextoChip => NomePreset is { } p ? $"Preset: {p}" : null;

    /// <summary>"Som diferente da FILME" (com Reaplicar), ou null.</summary>
    public string? SomDiferente => Som.SomDiferente;

    public Comando Reaplicar { get; }

    /// <summary>Setup sem memórias: "Criar memória do som atual".</summary>
    public bool MostrarCriarMemoria => Som.SemMemorias;

    /// <summary>Abre Configurações › Memórias com o campo do nome (ou mostra a trava).</summary>
    public Comando CriarMemoria { get; }

    /// <summary>Os 3 botões de memória e o "…" (os mesmos da Mesa).</summary>
    public BotoesMemoriaViewModel Memorias { get; }

    /// <summary>Os botões de memória aparecem com uma memória do celular em uso, o som do aparelho ou uma memória do aparelho.</summary>
    public bool MostrarBotoes => MostrarMemoria;

    // ------------------------------------------------------------------ saídas

    /// <summary>Um cartão por saída do Setup em uso (1 a 4), na ordem dos canais; refeitos quando as saídas mudam (no Casa 2.1: JBL L, JBL R e Sub).</summary>
    public IReadOnlyList<CartaoSaidaViewModel> CartoesSaida => _cartoes;

    /// <summary>O cartão da saída que tem o canal (0 a 3), ou null.</summary>
    public CartaoSaidaViewModel? Cartao(int canal) => _cartoes.FirstOrDefault(c => c.Saida.App.Canais.Contains(canal));

    /// <summary>A última frase ("Chave desligada", "Sem conexão com o aparelho.").</summary>
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

    // ------------------------------------------------------------------ ações

    private async Task ReaplicarAsync()
    {
        await _telaMemorias.Reaplicar.ExecutarAsync();
        Mensagem = _telaMemorias.Mensagem;
    }

    private async Task TrocarAsync()
    {
        if (!Conectado)
        {
            Mensagem = GerenteSetups.MsgSemConexao;
            return;
        }
        await _setup.Trocar.ExecutarAsync();
        Mensagem = _setup.Aberta ? null : _setup.Mensagem;
    }

    // ------------------------------------------------------------------ eventos

    private void AoMudar() => NaTela(Atualizar);

    private void AoMudarValor(Endereco _) => NaTela(AtualizarSaidas);

    private void AoMudarLink(ParCanais _) => NaTela(() =>
    {
        RefazerSaidas();
        AtualizarSaidas();
    });

    /// <summary>As saídas mudaram (link, equipamento, Setup): os cartões se refazem.</summary>
    private void RefazerSaidas()
    {
        var agora = SaidaTela.EmUso(_setups);
        if (SaidaTela.Iguais(agora, _cartoes.Select(c => c.Saida).ToArray()))
            return;
        _cartoes = agora.Select(s => new CartaoSaidaViewModel(s, _estado, _nav, Despachante, Relogio)).ToArray();
        Avisar(nameof(CartoesSaida));
    }

    private void AoMudarSessao() => NaTela(() =>
    {
        var agora = _sessao.Estado;
        if (_ultimoEstado == EstadoSessao.Conectado && agora != EstadoSessao.Conectado)
            _caiu = Relogio.GetLocalNow();
        else if (agora == EstadoSessao.Conectado)
            _caiu = null;
        _ultimoEstado = agora;
        Atualizar();
    });

    private void AoMudarConexao(object? sender, PropertyChangedEventArgs e)
    {
        Avisar(nameof(FraseConexao));
        Avisar(nameof(TextoLendo));
        Avisar(nameof(EstadoConexao));
    }

    private void AoMudarSetup(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SetupViewModel.Seletor))
            Avisar(nameof(Seletor));
    }

    private void AtualizarSaidas()
    {
        foreach (var s in CartoesSaida)
            s.Atualizar();
    }

    private void Atualizar()
    {
        RefazerSaidas();
        AtualizarSaidas();
        Avisar(nameof(Conectado));
        Avisar(nameof(MostrarConexao));
        Avisar(nameof(EstadoConexao));
        Avisar(nameof(FraseConexao));
        Avisar(nameof(HoraQueCaiu));
        Avisar(nameof(TextoLendo));
        Avisar(nameof(AvisoFixo));
        Avisar(nameof(Seletor));
        Avisar(nameof(SeletorRotulo));
        Avisar(nameof(SeletorNome));
        Avisar(nameof(MostrarMemoria));
        Avisar(nameof(NomeMemoria));
        Avisar(nameof(LinhaMemoria));
        Avisar(nameof(NomePreset));
        Avisar(nameof(MostrarChip));
        Avisar(nameof(TextoChip));
        Avisar(nameof(SomDiferente));
        Avisar(nameof(MostrarCriarMemoria));
        Avisar(nameof(MostrarBotoes));
    }

    public void Dispose()
    {
        _setups.Mudou -= AoMudar;
        _memorias.Mudou -= AoMudar;
        _sessao.Mudou -= AoMudarSessao;
        _estado.Recarregou -= AoMudar;
        _estado.Mudou -= AoMudarValor;
        _link.Mudou -= AoMudarLink;
        _conexao.PropertyChanged -= AoMudarConexao;
        _setup.PropertyChanged -= AoMudarSetup;
    }
}
