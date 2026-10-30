using BgsControle.Nucleo.Seguranca;

namespace BgsControle.Telas;

/// <summary>Uma linha da tela Chaves de acesso (ou a linha "Controle pela rede" no grupo APARELHO).</summary>
public sealed class ItemChaveViewModel : TelaBase
{
    private bool _ligada;

    internal ItemChaveViewModel(DefinicaoChave definicao, ChavesViewModel tela, IDespachante despachante, TimeProvider relogio)
        : base(despachante, relogio)
    {
        Definicao = definicao;
        Ligar = new Comando(() => tela.LigarAsync(definicao.Chave));
        Desligar = new Comando(() => tela.DesligarAsync(definicao.Chave));
    }

    public DefinicaoChave Definicao { get; }
    public Chave Chave => Definicao.Chave;
    public string Nome => Definicao.Nome;

    /// <summary>O risco mostrado ao ligar, ou null (liga sem aviso).</summary>
    public string? Risco => Definicao.Risco;

    /// <summary>A chave vem desligada por padrão: o risco escrito na linha fica em erro-texto (DESIGN, linha-de-chave).</summary>
    public bool PadraoDesligada => !Definicao.Padrao;

    public bool Ligada
    {
        get => _ligada;
        internal set
        {
            if (_ligada == value)
                return;
            _ligada = value;
            Avisar();
            Avisar(nameof(TextoEstado));
        }
    }

    public string TextoEstado => Ligada ? "Ligada" : "Desligada";

    /// <summary>"Lig.": com risco, abre a folha; sem risco, liga.</summary>
    public Comando Ligar { get; }

    /// <summary>"Desl.": desliga na hora, sem confirmar.</summary>
    public Comando Desligar { get; }
}

/// <summary>
/// A tela Chaves de acesso (CAP-11): as chaves com o estado de cada uma. Ligar uma chave com
/// risco abre a folha com o aviso (confirmar ou desistir); desligar age direto. Só a tela muda
/// uma chave; a porta nunca. A "Controle pela rede" não aparece nesta lista: a linha dela fica no
/// grupo APARELHO das Configurações (<see cref="Item"/> a devolve, com a mesma folha).
/// </summary>
public sealed class ChavesViewModel : TelaBase, IDisposable
{
    public const string TextoConfirmar = "Ligar a chave";
    public const string TextoDesistir = "Deixar desligada";

    private readonly IChaves _chaves;
    private readonly IReadOnlyList<ItemChaveViewModel> _todos;
    private DefinicaoChave? _folha;
    private string? _mensagem;

    public ChavesViewModel(IChaves chaves, IDespachante despachante, TimeProvider relogio)
        : base(despachante, relogio)
    {
        _chaves = chaves;
        _todos = chaves.Definicoes.Select(d => new ItemChaveViewModel(d, this, despachante, relogio)).ToList();
        Itens = _todos.Where(i => i.Chave != Chave.ControlePelaRede).ToList();
        ConfirmarFolha = new Comando(ConfirmarAsync);
        DesistirFolha = new Comando(Desistir);
        _chaves.Mudou += AoMudar;
        AtualizarItens();
        _ = chaves.Carregada.ContinueWith(_ => NaTela(AtualizarItens), TaskScheduler.Default);
    }

    /// <summary>As linhas da tela Chaves de acesso (sem a "Controle pela rede").</summary>
    public IReadOnlyList<ItemChaveViewModel> Itens { get; }

    /// <summary>A linha de qualquer chave, inclusive a "Controle pela rede".</summary>
    public ItemChaveViewModel Item(Chave chave) => _todos.First(i => i.Chave == chave);

    /// <summary>Uma linha de aviso (falha ao guardar), ou null.</summary>
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

    // ------------------------------------------------------------------ folha de risco

    public bool FolhaAberta => _folha is not null;

    /// <summary>"Ligar a chave “Gerador: tom fixo e sweep”?"</summary>
    public string? FolhaTitulo => _folha is { } d ? $"Ligar a chave “{d.Nome}”?" : null;

    /// <summary>O risco, sem mudar o texto de <c>chaves-de-acesso.md</c>.</summary>
    public string? FolhaRisco => _folha?.Risco;

    /// <summary>O botão branco: liga a chave.</summary>
    public Comando ConfirmarFolha { get; }

    /// <summary>O botão contornado, o véu ou Voltar: desiste sem ligar.</summary>
    public Comando DesistirFolha { get; }

    public async Task LigarAsync(Chave chave)
    {
        if (_chaves.Ligada(chave))
            return; // já ligada: nada a confirmar
        var d = ChavesPadrao.De(chave);
        if (d.Risco is not null)
        {
            _folha = d;
            Mensagem = null;
            AvisarFolha();
            return;
        }
        await DefinirAsync(chave, true);
    }

    public Task DesligarAsync(Chave chave) => DefinirAsync(chave, false);

    private async Task ConfirmarAsync()
    {
        if (_folha is not { } d)
            return;
        _folha = null;
        AvisarFolha();
        await DefinirAsync(d.Chave, true);
    }

    private void Desistir()
    {
        _folha = null;
        AvisarFolha();
    }

    private async Task DefinirAsync(Chave chave, bool ligada)
    {
        var r = await _chaves.DefinirAsync(chave, ligada, OrigemComando.Tela);
        NaTela(() =>
        {
            Mensagem = r.Motivo;
            AtualizarItens();
        });
    }

    private void AvisarFolha()
    {
        Avisar(nameof(FolhaAberta));
        Avisar(nameof(FolhaTitulo));
        Avisar(nameof(FolhaRisco));
    }

    private void AoMudar(Chave _) => NaTela(AtualizarItens);

    private void AtualizarItens()
    {
        foreach (var i in _todos)
            i.Ligada = _chaves.Ligada(i.Chave);
    }

    public void Dispose() => _chaves.Mudou -= AoMudar;
}
