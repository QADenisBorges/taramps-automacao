using BgsControle.Nucleo;
using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Memorias;
using BgsControle.Nucleo.Seguranca;

namespace BgsControle.Telas;

/// <summary>O cartão de uma memória na tela Memórias.</summary>
public sealed class CartaoMemoriaViewModel : TelaBase
{
    private string _nome = MemoriasViewModel.SemNome;
    private bool _emUso;

    internal CartaoMemoriaViewModel(int indice, MemoriasViewModel tela, IDespachante despachante, TimeProvider relogio)
        : base(despachante, relogio)
    {
        Indice = indice;
        Carregar = new Comando(() => tela.CarregarAsync(indice));
        SalvarAqui = new Comando(() => tela.AbrirSalvar(indice));
        Renomear = new Comando(() => tela.AbrirRenomear(indice));
    }

    /// <summary>A memória no fio (0 a 2).</summary>
    public int Indice { get; }

    /// <summary>A memória na tela (1 a 3).</summary>
    public int Numero => Indice + 1;

    public string Titulo => $"Memória {Numero}";

    /// <summary>O nome lido do aparelho, ou "—" antes da leitura.</summary>
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

    /// <summary>É a memória em uso (a última carregada ou salva pelo app).</summary>
    public bool EmUso
    {
        get => _emUso;
        internal set
        {
            if (_emUso == value)
                return;
            _emUso = value;
            Avisar();
        }
    }

    /// <summary>Carrega direto, sem confirmação (a cópia antes é guardada).</summary>
    public Comando Carregar { get; }

    /// <summary>Abre a folha de sobrescrever, com o nome editável.</summary>
    public Comando SalvarAqui { get; }

    /// <summary>Abre o campo do nome.</summary>
    public Comando Renomear { get; }
}

/// <summary>
/// A tela Memórias (Configurações › Memórias): as 3 memórias com os nomes lidos e a que está em
/// uso. "Carregar" age direto; "Salvar aqui" abre a folha "Salvar sobre a MUSICA ORIG? A versão
/// atual some.", com o nome editável, Confirmar ou Cancelar; "Renomear" envia só o nome. Cada ação
/// fica travada, com o motivo, sem conexão, em modo só leitura ou com a chave desligada. Depois de
/// uma queda no meio de um carregar, mostra "aplicado pela metade" com Terminar e Voltar à cópia.
/// </summary>
public sealed class MemoriasViewModel : TelaBase, IDisposable
{
    public const string SemNome = "—";
    public const string AvisoSobrescrever = "A versão atual some.";
    public const string TextoCancelar = "Cancelar";
    public const string TextoSalvando = "Salvando…";
    public const string TextoRenomear = "Renomear";
    public const string TituloPelaMetade = "Aplicado pela metade";
    public const string TextoTerminar = "Terminar";
    public const string TextoVoltar = "Voltar à cópia";
    public const string MsgEmAndamento = "Uma operação está em andamento";
    public const string MsgVoltou = "O som voltou à cópia.";

    private readonly GerenteMemorias _gerente;
    private readonly CopiaAntes _copia;
    private readonly Sessao _sessao;
    private readonly IChaves _chaves;
    private readonly EstadoAparelho _estado;

    private string? _mensagem;
    private string? _ultimoAvisoGerente;
    private int? _folha;
    private string _nomeFolha = "";
    private string? _erroFolha;
    private bool _salvando;
    private int? _renomear;
    private string _nomeRenomear = "";
    private string? _erroRenomear;
    private bool _agindo;

    public MemoriasViewModel(
        GerenteMemorias gerente,
        CopiaAntes copia,
        Sessao sessao,
        IChaves chaves,
        EstadoAparelho estado,
        IDespachante despachante,
        TimeProvider relogio)
        : base(despachante, relogio)
    {
        _gerente = gerente;
        _copia = copia;
        _sessao = sessao;
        _chaves = chaves;
        _estado = estado;
        Memorias = Enumerable.Range(0, Catalogo.Memorias).Select(n => new CartaoMemoriaViewModel(n, this, despachante, relogio)).ToList();

        ConfirmarFolha = new Comando(ConfirmarSalvarAsync);
        CancelarFolha = new Comando(FecharFolha);
        ConfirmarRenomear = new Comando(ConfirmarRenomearAsync);
        CancelarRenomear = new Comando(FecharRenomear);
        Terminar = new Comando(TerminarAsync);
        VoltarACopia = new Comando(VoltarAsync);

        _ultimoAvisoGerente = _gerente.Aviso;
        _gerente.Mudou += AoMudarAlgo;
        _copia.Mudou += AoMudarAlgo;
        _sessao.Mudou += AoMudarAlgo;
        _chaves.Mudou += AoMudarChave;
        _estado.Recarregou += AoMudarAlgo;
        _estado.Mudou += AoMudarValor;
        Atualizar();
    }

    public IReadOnlyList<CartaoMemoriaViewModel> Memorias { get; }

    public CartaoMemoriaViewModel Memoria(int numero) => Memorias[numero - 1];

    /// <summary>A última frase: "Memória 2 salva", "Chave desligada", "Use até 12 letras…".</summary>
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

    // ------------------------------------------------------------------ travas

    /// <summary>Por que "Carregar" está travado, ou null.</summary>
    public string? MotivoTravaCarregar => MotivoTrava(Chave.CarregarMemoria);

    /// <summary>Por que "Salvar aqui" e "Renomear" estão travados, ou null.</summary>
    public string? MotivoTravaSalvar => MotivoTrava(Chave.SalvarMemoria);

    public bool PodeCarregar => MotivoTravaCarregar is null;
    public bool PodeSalvar => MotivoTravaSalvar is null;

    /// <summary>Uma operação de memória (ou o Voltar à cópia) está andando.</summary>
    public bool Ocupado => _agindo || _gerente.Ocupado || _copia.EmAndamento;

    private string? MotivoTrava(Chave chave)
    {
        if (_sessao.Estado != EstadoSessao.Conectado || !_sessao.SegurandoConexao)
            return ControleParametroViewModel.MsgSemConexao;
        if (_sessao.SoLeitura)
            return ControleParametroViewModel.MsgSoLeitura;
        if (!_chaves.Ligada(chave))
            return ControleParametroViewModel.MsgChaveDesligada;
        if (_estado.Lendo)
            return ControleParametroViewModel.MsgLendo;
        if (!_estado.Pronto)
            return ControleParametroViewModel.MsgNaoLeu;
        if (Ocupado)
            return MsgEmAndamento;
        return null;
    }

    // ------------------------------------------------------------------ carregar

    /// <summary>Carregar: age direto, sem confirmação. Travado, nada sai e o motivo aparece.</summary>
    public async Task CarregarAsync(int indice)
    {
        if (MotivoTravaCarregar is { } motivo)
        {
            Mensagem = motivo;
            return;
        }
        await Agir(async () =>
        {
            var r = await _gerente.CarregarAsync(indice);
            return r.Mensagem;
        });
    }

    // ------------------------------------------------------------------ folha de sobrescrever

    public bool FolhaAberta => _folha is not null;

    /// <summary>"Salvar sobre a MUSICA ORIG?"</summary>
    public string? FolhaTitulo => _folha is { } n ? $"Salvar sobre a {Alvo(n)}?" : null;

    /// <summary>"A versão atual some."</summary>
    public string? FolhaAviso => _folha is null ? null : AvisoSobrescrever;

    /// <summary>O botão branco: "Salvar sobre a MUSICA ORIG", ou "Salvando…" até o fim do salvar.</summary>
    public string? TextoConfirmarFolha => _folha is not { } n ? null : _salvando ? TextoSalvando : $"Salvar sobre a {Alvo(n)}";

    public bool Salvando => _salvando;

    /// <summary>O nome editável (vai em maiúsculas).</summary>
    public string NomeFolha
    {
        get => _nomeFolha;
        set
        {
            if (_nomeFolha == value)
                return;
            _nomeFolha = value;
            Avisar();
        }
    }

    /// <summary>"Use até 12 letras, números, espaço ou _", ou null.</summary>
    public string? ErroFolha
    {
        get => _erroFolha;
        private set
        {
            if (_erroFolha == value)
                return;
            _erroFolha = value;
            Avisar();
        }
    }

    /// <summary>
    /// O botão branco: salva (240 e depois o nome). "Memória 2 salva" com a resposta ao 240;
    /// "Memória 2 enviada (o aparelho não confirma o salvar)" sem ela. Se falhar, a folha fica
    /// aberta com o erro e o nome digitado.
    /// </summary>
    public Comando ConfirmarFolha { get; }

    /// <summary>O botão contornado, o véu ou Voltar: nada sai.</summary>
    public Comando CancelarFolha { get; }

    /// <summary>"Salvar aqui": abre a folha de sobrescrever com o nome atual. Travado, só o motivo.</summary>
    public void AbrirSalvar(int indice)
    {
        if (MotivoTravaSalvar is { } motivo)
        {
            Mensagem = motivo;
            return;
        }
        FecharRenomear();
        _folha = indice;
        NomeFolha = _gerente.Nome(indice) ?? "";
        ErroFolha = null;
        Mensagem = null;
        AvisarFolha();
    }

    private async Task ConfirmarSalvarAsync()
    {
        if (_folha is not { } n || _salvando)
            return;
        if (Catalogo.NormalizarNomeMemoria(NomeFolha) is not { } nome)
        {
            ErroFolha = GerenteMemorias.MsgNomeInvalido; // a folha fica aberta; nada sai
            return;
        }
        if (MotivoTravaSalvar is { } motivo)
        {
            ErroFolha = motivo;
            return;
        }
        ErroFolha = null;
        _salvando = true;
        AvisarFolha();
        ResultadoMemoria? resultado = null;
        await Agir(async () =>
        {
            resultado = await _gerente.SalvarAsync(n, nome);
            return resultado.Mensagem;
        }, depois: () =>
        {
            _salvando = false;
            if (resultado is { Ok: true })
                FecharFolha();
            else
            {
                // A folha fica aberta, com o nome digitado e o erro, para tentar de novo.
                _erroFolha = resultado?.Mensagem ?? resultado?.Motivo ?? "Não salvou.";
                Avisar(nameof(ErroFolha));
                AvisarFolha();
            }
        });
    }

    private void FecharFolha()
    {
        if (_salvando)
            return; // o salvar já saiu: a folha fecha quando ele termina
        _folha = null;
        _nomeFolha = "";
        _erroFolha = null;
        Avisar(nameof(NomeFolha));
        Avisar(nameof(ErroFolha));
        AvisarFolha();
    }

    private void AvisarFolha()
    {
        Avisar(nameof(FolhaAberta));
        Avisar(nameof(FolhaTitulo));
        Avisar(nameof(FolhaAviso));
        Avisar(nameof(TextoConfirmarFolha));
        Avisar(nameof(Salvando));
    }

    // ------------------------------------------------------------------ renomear

    public bool RenomearAberto => _renomear is not null;

    /// <summary>"Renomear a memória 2".</summary>
    public string? RenomearTitulo => _renomear is { } n ? $"{TextoRenomear} a memória {n + 1}" : null;

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

    /// <summary>Envia só o nome.</summary>
    public Comando ConfirmarRenomear { get; }

    public Comando CancelarRenomear { get; }

    /// <summary>"Renomear": abre o campo do nome. Travado, só o motivo.</summary>
    public void AbrirRenomear(int indice)
    {
        if (MotivoTravaSalvar is { } motivo)
        {
            Mensagem = motivo;
            return;
        }
        FecharFolha();
        _renomear = indice;
        NomeRenomear = _gerente.Nome(indice) ?? "";
        ErroRenomear = null;
        Mensagem = null;
        AvisarRenomear();
    }

    private async Task ConfirmarRenomearAsync()
    {
        if (_renomear is not { } n)
            return;
        if (Catalogo.NormalizarNomeMemoria(NomeRenomear) is not { } nome)
        {
            ErroRenomear = GerenteMemorias.MsgNomeInvalido;
            return;
        }
        if (MotivoTravaSalvar is { } motivo)
        {
            ErroRenomear = motivo;
            return;
        }
        ErroRenomear = null;
        ResultadoMemoria? resultado = null;
        await Agir(async () =>
        {
            resultado = await _gerente.RenomearAsync(n, nome);
            return resultado.Mensagem;
        }, depois: () =>
        {
            if (resultado is { Ok: true })
                FecharRenomear();
            else
                ErroRenomear = resultado?.Mensagem ?? resultado?.Motivo; // o campo fica, com o nome digitado
        });
    }

    private void FecharRenomear()
    {
        _renomear = null;
        _nomeRenomear = "";
        _erroRenomear = null;
        Avisar(nameof(NomeRenomear));
        Avisar(nameof(ErroRenomear));
        AvisarRenomear();
    }

    private void AvisarRenomear()
    {
        Avisar(nameof(RenomearAberto));
        Avisar(nameof(RenomearTitulo));
    }

    // ------------------------------------------------------------------ aplicado pela metade

    /// <summary>A folha "Aplicado pela metade" está aberta.</summary>
    public bool PelaMetade => _copia.PelaMetade is not null;

    /// <summary>"Carregar a memória 3 (FILME) caiu no meio."</summary>
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
            return r.Ok ? _gerente.Aviso : r.Motivo;
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
        Atualizar();
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
                _ultimoAvisoGerente = _gerente.Aviso;
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

    private string Alvo(int n) => _gerente.Nome(n) is { Length: > 0 } nome ? nome : $"memória {n + 1}";

    private static string? Frase(string? motivo) =>
        string.IsNullOrEmpty(motivo) ? null : char.ToUpperInvariant(motivo[0]) + motivo[1..];

    private void AoMudarAlgo() => NaTela(Atualizar);

    private void AoMudarChave(Chave chave)
    {
        if (chave is Chave.CarregarMemoria or Chave.SalvarMemoria or Chave.AjustesAudio)
            NaTela(Atualizar);
    }

    private void AoMudarValor(Endereco e)
    {
        if (e.Indice is null && e.Codigo.StartsWith("23", StringComparison.Ordinal))
            NaTela(Atualizar);
    }

    private void Atualizar()
    {
        var emUso = _gerente.EmUso;
        foreach (var m in Memorias)
        {
            m.Nome = _gerente.Nome(m.Indice) is { Length: > 0 } nome ? nome : SemNome;
            m.EmUso = emUso == m.Indice;
        }

        // Um aviso novo do núcleo (o resultado de um salvar que caiu, por exemplo) aparece.
        var aviso = _gerente.Aviso;
        if (aviso != _ultimoAvisoGerente)
        {
            _ultimoAvisoGerente = aviso;
            if (aviso is not null)
                Mensagem = aviso;
        }

        Avisar(nameof(MotivoTravaCarregar));
        Avisar(nameof(MotivoTravaSalvar));
        Avisar(nameof(PodeCarregar));
        Avisar(nameof(PodeSalvar));
        Avisar(nameof(Ocupado));
        Avisar(nameof(PelaMetade));
        Avisar(nameof(TextoPelaMetade));
        AvisarFolha();
    }

    public void Dispose()
    {
        _gerente.Mudou -= AoMudarAlgo;
        _copia.Mudou -= AoMudarAlgo;
        _sessao.Mudou -= AoMudarAlgo;
        _chaves.Mudou -= AoMudarChave;
        _estado.Recarregou -= AoMudarAlgo;
        _estado.Mudou -= AoMudarValor;
    }
}
