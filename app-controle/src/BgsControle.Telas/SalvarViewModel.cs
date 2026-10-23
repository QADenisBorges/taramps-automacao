using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Setups;

namespace BgsControle.Telas;

/// <summary>Uma linha da folha do Salvar: "Calibração → Setup Casa 2.1" e o que mudou, com o antes e o depois.</summary>
public sealed record LinhaSalvarViewModel(string Titulo, IReadOnlyList<string> Mudancas);

/// <summary>
/// A folha do Salvar por camadas (EXPERIENCE, "Salvar por camadas", mock da 3b, quadro 3): "Salvar o
/// que mudou?", uma linha por camada com o antes → depois, a pergunta Manter / Atualizar quando a
/// calibração do aparelho difere da do Setup e a memória em uso não é do celular, o nome da memória
/// nova, e Confirmar travado até escolher e dar o nome. Nada é guardado antes do botão branco. A
/// linha das 15 bandas é da história 8d.
/// </summary>
public sealed class SalvarViewModel : TelaBase, IDisposable
{
    public const string TituloFolha = "Salvar o que mudou?";
    public const string SubtituloFolha = "Cada mudança vai para o lugar dela.";
    public const string TextoConfirmar = "Salvar";
    public const string TextoSalvando = "Salvando…";
    public const string TextoCancelar = "Cancelar";
    public const string MsgFalteNome = "Dê um nome à memória nova.";

    private readonly GerenteSetups _setups;
    private readonly EstadoAparelho _estado;
    private readonly LinkCanais _link;

    private FolhaSalvar? _folha;
    private EscolhaCalibracao _escolha = EscolhaCalibracao.Nenhuma;
    private string _nome = "";
    private string? _erro;
    private string? _mensagem;
    private bool _salvando;

    public SalvarViewModel(GerenteSetups setups, EstadoAparelho estado, LinkCanais link, IDespachante despachante, TimeProvider relogio)
        : base(despachante, relogio)
    {
        _setups = setups;
        _estado = estado;
        _link = link;
        Abrir = new Comando(AbrirFolha);
        EscolherManter = new Comando(() => Escolher(EscolhaCalibracao.Manter));
        EscolherAtualizar = new Comando(() => Escolher(EscolhaCalibracao.Atualizar));
        Confirmar = new Comando(ConfirmarAsync);
        Cancelar = new Comando(Fechar);
        _setups.Mudou += AoMudar;
        // Um valor mexido no aparelho (ou o link) muda o que há para salvar.
        _estado.Mudou += AoMudarValor;
        _estado.Recarregou += AoMudar;
        _link.Mudou += AoMudarLink;
    }

    // ------------------------------------------------------------------ o botão Salvar

    /// <summary>Por que o botão Salvar está travado ("Nada para salvar.", "Escolha um Setup para salvar."), ou null.</summary>
    public string? MotivoTrava => _setups.PrepararSalvar().Trava;

    public bool PodeSalvar => MotivoTrava is null;

    /// <summary>O botão Salvar: abre a folha. Travado, só mostra o motivo.</summary>
    public Comando Abrir { get; }

    // ------------------------------------------------------------------ a folha

    public bool Aberta => _folha is not null;

    public string? Titulo => Aberta ? TituloFolha : null;

    public string? Subtitulo => Aberta ? SubtituloFolha : null;

    /// <summary>Uma por camada: "Calibração → Setup Casa 2.1" com "Atraso da saída 3: 80 → 85 cm".</summary>
    public IReadOnlyList<LinhaSalvarViewModel> Linhas =>
        _folha?.Linhas.Select(l => new LinhaSalvarViewModel(l.Titulo, l.Mudancas.Select(m => m.Linha).ToArray())).ToArray() ?? [];

    /// <summary>A calibração do aparelho difere da do Setup: Manter ou Atualizar.</summary>
    public bool Pergunta => _folha?.PerguntaCalibracao == true;

    /// <summary>"Manter a calibração do Casa 2.1".</summary>
    public string? TextoManter => Pergunta ? $"Manter a calibração do {_folha!.NomeSetup}" : null;

    /// <summary>"O app reaplica a do Setup; a memória guarda só o caráter."</summary>
    public string? FraseManter => Pergunta ? "O app reaplica a do Setup; a memória guarda só o caráter." : null;

    /// <summary>"Atualizar o Casa 2.1 com a do aparelho".</summary>
    public string? TextoAtualizar => Pergunta ? $"Atualizar o {_folha!.NomeSetup} com a do aparelho" : null;

    /// <summary>"Vale para todas as memórias do Casa 2.1."</summary>
    public string? AvisoAtualizar => Pergunta ? $"Vale para todas as memórias do {_folha!.NomeSetup}." : null;

    public EscolhaCalibracao Escolha => _escolha;

    public Comando EscolherManter { get; }

    public Comando EscolherAtualizar { get; }

    /// <summary>O caráter vai para uma memória nova: o campo do nome.</summary>
    public bool PedeNome => _folha?.PedeNome == true;

    public string NomeMemoria
    {
        get => _nome;
        set
        {
            if (_nome == value)
                return;
            _nome = value;
            Avisar();
            Avisar(nameof(PodeConfirmar));
        }
    }

    /// <summary>O erro do salvar (nome inválido ou repetido, chave), ou null.</summary>
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

    /// <summary>O botão branco destrava depois de escolher (quando pergunta) e de dar o nome (quando pede).</summary>
    public bool PodeConfirmar =>
        Aberta && !_salvando
        && (!Pergunta || _escolha != EscolhaCalibracao.Nenhuma)
        && (!PedeNome || !string.IsNullOrWhiteSpace(_nome));

    /// <summary>"Salvar", ou "Salvando…" até o fim.</summary>
    public string? TextoBotao => Aberta ? (_salvando ? TextoSalvando : TextoConfirmar) : null;

    public Comando Confirmar { get; }

    public Comando Cancelar { get; }

    /// <summary>O aviso final ("Salvo: calibração no Casa 2.1, caráter na MUSICA ORIG.") ou o motivo da trava.</summary>
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

    private void AbrirFolha()
    {
        var folha = _setups.PrepararSalvar();
        if (folha.Trava is { } trava)
        {
            Mensagem = trava;
            return;
        }
        _folha = folha;
        _escolha = EscolhaCalibracao.Nenhuma;
        _nome = "";
        _erro = null;
        Mensagem = null;
        AvisarTudo();
    }

    private void Escolher(EscolhaCalibracao escolha)
    {
        if (!Pergunta)
            return;
        _escolha = escolha;
        Avisar(nameof(Escolha));
        Avisar(nameof(PodeConfirmar));
    }

    private async Task ConfirmarAsync()
    {
        if (!Aberta || _salvando)
            return;
        if (Pergunta && _escolha == EscolhaCalibracao.Nenhuma)
        {
            Erro = GerenteSetups.MsgEscolha;
            return;
        }
        if (PedeNome && string.IsNullOrWhiteSpace(_nome))
        {
            Erro = MsgFalteNome;
            return;
        }
        _salvando = true;
        AvisarTudo();
        ResultadoSetup r;
        try
        {
            r = await _setups.SalvarAsync(_escolha, PedeNome ? _nome : null);
        }
        catch (Exception e)
        {
            r = ResultadoSetup.Falha($"Erro inesperado ({e.GetType().Name}).");
        }
        var feito = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        NaTela(() =>
        {
            try
            {
                _salvando = false;
                if (r.Ok)
                {
                    _folha = null;
                    _erro = null;
                    Mensagem = r.Mensagem;
                }
                else
                {
                    // A folha fica aberta, com o nome digitado e o erro, para tentar de novo.
                    _erro = r.Mensagem ?? r.Motivo;
                }
                AvisarTudo();
            }
            finally
            {
                feito.SetResult();
            }
        });
        await feito.Task;
    }

    private void Fechar()
    {
        if (_salvando)
            return;
        _folha = null;
        _escolha = EscolhaCalibracao.Nenhuma;
        _nome = "";
        _erro = null;
        AvisarTudo();
    }

    private void AoMudar() => NaTela(() =>
    {
        Avisar(nameof(MotivoTrava));
        Avisar(nameof(PodeSalvar));
    });

    private void AoMudarValor(Endereco _) => AoMudar();

    private void AoMudarLink(ParCanais _) => AoMudar();

    private void AvisarTudo()
    {
        Avisar(nameof(Aberta));
        Avisar(nameof(Titulo));
        Avisar(nameof(Subtitulo));
        Avisar(nameof(Linhas));
        Avisar(nameof(Pergunta));
        Avisar(nameof(TextoManter));
        Avisar(nameof(FraseManter));
        Avisar(nameof(TextoAtualizar));
        Avisar(nameof(AvisoAtualizar));
        Avisar(nameof(Escolha));
        Avisar(nameof(PedeNome));
        Avisar(nameof(NomeMemoria));
        Avisar(nameof(Erro));
        Avisar(nameof(PodeConfirmar));
        Avisar(nameof(TextoBotao));
        Avisar(nameof(MotivoTrava));
        Avisar(nameof(PodeSalvar));
    }

    public void Dispose()
    {
        _setups.Mudou -= AoMudar;
        _estado.Mudou -= AoMudarValor;
        _estado.Recarregou -= AoMudar;
        _link.Mudou -= AoMudarLink;
    }
}
