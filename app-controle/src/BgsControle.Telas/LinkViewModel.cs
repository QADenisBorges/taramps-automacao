using BgsControle.Nucleo;
using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Seguranca;

namespace BgsControle.Telas;

/// <summary>Um par Lig./Desl. do link (1–2 ou 3–4).</summary>
public sealed class ParLinkViewModel : TelaBase
{
    private bool _ligado;

    internal ParLinkViewModel(ParCanais par, LinkViewModel tela, IDespachante despachante, TimeProvider relogio)
        : base(despachante, relogio)
    {
        Par = par;
        Ligar = new Comando(() => tela.Ligar(par));
        Desligar = new Comando(() => tela.DesligarAsync(par));
    }

    public ParCanais Par { get; }

    /// <summary>"Link 1–2".</summary>
    public string Rotulo => $"Link {LinkCanais.Rotulo(Par)}";

    public bool Ligado
    {
        get => _ligado;
        internal set
        {
            if (_ligado == value)
                return;
            _ligado = value;
            Avisar();
            Avisar(nameof(TextoEstado));
        }
    }

    public string TextoEstado => Ligado ? "Lig." : "Desl.";

    /// <summary>
    /// "Link 1–2: as saídas estão diferentes" quando a última leitura completa (também a de
    /// depois de carregar uma memória) achou o par ligado com as saídas diferentes; senão null.
    /// Só avisa: nada é escrito sozinho.
    /// </summary>
    public string? AvisoDiferenca
    {
        get => _avisoDiferenca;
        internal set
        {
            if (_avisoDiferenca == value)
                return;
            _avisoDiferenca = value;
            Avisar();
        }
    }

    private string? _avisoDiferenca;

    /// <summary>"Lig.": abre o aviso; só copia depois do Confirmar.</summary>
    public Comando Ligar { get; }

    /// <summary>"Desl.": desliga na hora (não mexe no aparelho).</summary>
    public Comando Desligar { get; }
}

/// <summary>
/// O link 1–2 e 3–4 (só do app, MAPA §6): dois pares Lig./Desl. Ao ligar, aparece o aviso "Os
/// ajustes da saída 1 vão para a saída 2", e a cópia só sai depois do Confirmar. Desligar age
/// direto. Ligar fica travado, com o motivo, sem conexão, em modo só leitura, com a chave
/// "Ajustes de áudio" desligada ou antes da leitura.
/// </summary>
public sealed class LinkViewModel : TelaBase, IDisposable
{
    public const string TextoConfirmar = "Confirmar";
    public const string TextoCancelar = "Cancelar";

    private readonly LinkCanais _link;
    private readonly Sessao _sessao;
    private readonly IChaves _chaves;
    private readonly EstadoAparelho _estado;
    private ParCanais? _folha;
    private string? _mensagem;
    private bool _copiando;

    public LinkViewModel(LinkCanais link, Sessao sessao, IChaves chaves, EstadoAparelho estado, IDespachante despachante, TimeProvider relogio)
        : base(despachante, relogio)
    {
        _link = link;
        _sessao = sessao;
        _chaves = chaves;
        _estado = estado;
        Pares = LinkCanais.Pares.Select(p => new ParLinkViewModel(p, this, despachante, relogio)).ToList();
        ConfirmarFolha = new Comando(ConfirmarAsync);
        CancelarFolha = new Comando(Cancelar);

        _link.Mudou += AoMudarLink;
        _sessao.Mudou += AoMudarAlgo;
        _chaves.Mudou += AoMudarChave;
        _estado.Recarregou += AoMudarAlgo;
        Atualizar();
        _ = link.Carregada.ContinueWith(_ => NaTela(Atualizar), TaskScheduler.Default);
    }

    public IReadOnlyList<ParLinkViewModel> Pares { get; }

    public ParLinkViewModel Par(ParCanais par) => Pares.First(p => p.Par == par);

    /// <summary>"Link 1–2 ligado.", o motivo de uma trava ou de uma cópia que falhou.</summary>
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

    /// <summary>Copiando os ajustes da mestre para a outra saída.</summary>
    public bool Copiando => _copiando;

    /// <summary>Por que ligar um link está travado, ou null.</summary>
    public string? MotivoTrava
    {
        get
        {
            if (_sessao.Estado != EstadoSessao.Conectado || !_sessao.SegurandoConexao)
                return ControleParametroViewModel.MsgSemConexao;
            if (_sessao.SoLeitura)
                return ControleParametroViewModel.MsgSoLeitura;
            if (!_chaves.Ligada(Chave.AjustesAudio))
                return ControleParametroViewModel.MsgChaveDesligada;
            if (_estado.Lendo)
                return ControleParametroViewModel.MsgLendo;
            if (!_estado.Pronto)
                return ControleParametroViewModel.MsgNaoLeu;
            return null;
        }
    }

    // ------------------------------------------------------------------ folha

    public bool FolhaAberta => _folha is not null;

    /// <summary>"Os ajustes da saída 1 vão para a saída 2".</summary>
    public string? FolhaAviso => _folha is { } par ? Aviso(par) : null;

    public static string Aviso(ParCanais par)
    {
        var (mestre, outro) = LinkCanais.Canais(par);
        return $"Os ajustes da saída {mestre + 1} vão para a saída {outro + 1}";
    }

    /// <summary>O botão branco: copia e liga.</summary>
    public Comando ConfirmarFolha { get; }

    /// <summary>O botão contornado: nada sai.</summary>
    public Comando CancelarFolha { get; }

    /// <summary>"Lig.": abre a folha com o aviso. Já ligado, nada; travado, só o motivo.</summary>
    public void Ligar(ParCanais par)
    {
        if (_link.Ligado(par))
            return;
        if (_copiando)
        {
            Mensagem = LinkCanais.MsgOcupado; // uma cópia anda: a folha não abriria nada
            return;
        }
        if (MotivoTrava is { } motivo)
        {
            Mensagem = motivo;
            return;
        }
        _folha = par;
        Mensagem = null;
        AvisarFolha();
    }

    /// <summary>"Desl.": desliga já, sem confirmar.</summary>
    public async Task DesligarAsync(ParCanais par)
    {
        var r = await _link.DesvincularAsync(par);
        NaTela(() =>
        {
            Mensagem = r.Motivo;
            Atualizar();
        });
    }

    private async Task ConfirmarAsync()
    {
        if (_folha is not { } par || _copiando)
            return;
        if (MotivoTrava is { } motivo)
        {
            _folha = null;
            AvisarFolha();
            Mensagem = motivo;
            return;
        }
        _folha = null;
        _copiando = true;
        AvisarFolha();
        Avisar(nameof(Copiando));

        ResultadoLink r;
        try
        {
            r = await _link.VincularAsync(par);
        }
        catch (Exception e)
        {
            r = new ResultadoLink(false, $"Erro inesperado ({e.GetType().Name}).");
        }
        var feito = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        NaTela(() =>
        {
            try
            {
                _copiando = false;
                Mensagem = r.Ok ? $"Link {LinkCanais.Rotulo(par)} ligado." : Frase(r.Motivo);
                Avisar(nameof(Copiando));
                Atualizar();
            }
            finally
            {
                feito.SetResult();
            }
        });
        await feito.Task;
    }

    private void Cancelar()
    {
        _folha = null;
        AvisarFolha();
    }

    private void AvisarFolha()
    {
        Avisar(nameof(FolhaAberta));
        Avisar(nameof(FolhaAviso));
    }

    private static string? Frase(string? motivo) =>
        string.IsNullOrEmpty(motivo) ? null : char.ToUpperInvariant(motivo[0]) + motivo[1..];

    private void AoMudarLink(ParCanais _) => NaTela(Atualizar);

    private void AoMudarAlgo() => NaTela(Atualizar);

    private void AoMudarChave(Chave chave)
    {
        if (chave == Chave.AjustesAudio)
            NaTela(Atualizar);
    }

    private void Atualizar()
    {
        foreach (var p in Pares)
        {
            p.Ligado = _link.Ligado(p.Par);
            p.AvisoDiferenca = _link.AvisoDiferenca(p.Par);
        }
        Avisar(nameof(MotivoTrava));
    }

    public void Dispose()
    {
        _link.Mudou -= AoMudarLink;
        _sessao.Mudou -= AoMudarAlgo;
        _chaves.Mudou -= AoMudarChave;
        _estado.Recarregou -= AoMudarAlgo;
    }
}
