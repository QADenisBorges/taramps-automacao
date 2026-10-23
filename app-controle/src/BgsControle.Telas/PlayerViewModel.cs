using System.Globalization;
using BgsControle.Nucleo.Player;
using BgsControle.Nucleo.Setups;

namespace BgsControle.Telas;

/// <summary>Uma linha da playlist: nome, duração em m:ss, pasta, a marca de "tocando" e a de arquivo sumido.</summary>
public sealed class LinhaPlaylistViewModel : TelaBase
{
    private int _posicao;
    private bool _tocando;
    private bool _indisponivel;

    internal LinhaPlaylistViewModel(Faixa faixa, PlayerViewModel tela, IDespachante despachante, TimeProvider relogio)
        : base(despachante, relogio)
    {
        Id = faixa.Id;
        Nome = faixa.Nome;
        Pasta = faixa.Pasta;
        Duracao = PlayerViewModel.FormatarDuracao(faixa.Duracao);
        Tocar = new Comando(() => tela.TocarFaixaAsync(Id));
        Remover = new Comando(() => tela.RemoverAsync(Id));
    }

    public string Id { get; }

    public string Nome { get; }

    public string Pasta { get; }

    /// <summary>"3:07".</summary>
    public string Duracao { get; }

    /// <summary>A posição na playlist (1 = a primeira).</summary>
    public int Posicao
    {
        get => _posicao;
        internal set
        {
            if (_posicao == value)
                return;
            _posicao = value;
            Avisar();
        }
    }

    /// <summary>É a faixa que toca (ou está pausada).</summary>
    public bool Tocando
    {
        get => _tocando;
        internal set
        {
            if (_tocando == value)
                return;
            _tocando = value;
            Avisar();
        }
    }

    /// <summary>O arquivo sumiu do celular: a linha fica apagada e é pulada ao tocar.</summary>
    public bool Indisponivel
    {
        get => _indisponivel;
        internal set
        {
            if (_indisponivel == value)
                return;
            _indisponivel = value;
            Avisar();
            Avisar(nameof(TextoIndisponivel));
        }
    }

    /// <summary>"Arquivo não encontrado", ou null.</summary>
    public string? TextoIndisponivel => _indisponivel ? Player.TextoIndisponivel : null;

    /// <summary>Tocar na linha: toca a partir desta faixa.</summary>
    public Comando Tocar { get; }

    /// <summary>O − do modo Editar: tira a faixa da playlist (o arquivo continua no celular), sem confirmação.</summary>
    public Comando Remover { get; }
}

/// <summary>
/// A tela Player (CAP-15): a playlist da memória do celular em uso, sem abas (decisão do Denis,
/// 22/10). O título "Playlist da NOME"; sem memória do celular em uso, "Sem memória do celular em
/// uso, não há playlist. Aplique uma memória do celular." e nenhum comando. As linhas (nome, m:ss, pasta, tocando, "Arquivo não encontrado"); Editar e Pronto;
/// Adicionar músicas (o seletor do Android, vários de uma vez, no fim); Remover e Mover; Tocar/pausar
/// e Próxima. Nada daqui manda nada ao processador.
/// </summary>
public sealed class PlayerViewModel : TelaBase, IDisposable
{
    public const string TextoAdicionar = "Adicionar músicas";
    public const string TextoEditar = "Editar";
    public const string TextoPronto = "Pronto";
    public const string TextoTocar = "Tocar";
    public const string TextoPausar = "Pausar";
    public const string TextoProxima = "Próxima";

    /// <summary>"3:07": minutos e segundos (os minutos passam de 59 sem virar hora).</summary>
    public static string FormatarDuracao(TimeSpan d)
    {
        if (d < TimeSpan.Zero)
            d = TimeSpan.Zero;
        var segundos = (long)d.TotalSeconds;
        return string.Create(CultureInfo.InvariantCulture, $"{segundos / 60}:{segundos % 60:00}");
    }

    private readonly Player _player;
    private readonly GerentePlaylists _playlists;
    private readonly GerenteSetups _setups;
    private readonly ISeletorAudio _seletor;

    private readonly Dictionary<string, LinhaPlaylistViewModel> _porId = [];
    private IReadOnlyList<LinhaPlaylistViewModel> _linhas = [];
    private string? _mensagem;
    private string? _ultimoAvisoPlayer;
    private bool _editando;
    private bool _agindo;
    private string? _memoriaVista;

    public PlayerViewModel(
        Player player,
        GerentePlaylists playlists,
        GerenteSetups setups,
        ISeletorAudio seletor,
        IDespachante despachante,
        TimeProvider relogio)
        : base(despachante, relogio)
    {
        _player = player;
        _playlists = playlists;
        _setups = setups;
        _seletor = seletor;

        Adicionar = new Comando(AdicionarAsync);
        Editar = new Comando(() => Editando = true);
        Pronto = new Comando(() => Editando = false);
        TocarPausar = new Comando(TocarPausarAsync);
        Proxima = new Comando(ProximaAsync);

        _ultimoAvisoPlayer = _player.Aviso;
        _memoriaVista = _player.Memoria;
        _player.Mudou += AoMudarAlgo;
        _playlists.Mudou += AoMudarAlgo;
        _setups.Mudou += AoMudarAlgo;
        Atualizar();
    }

    // ------------------------------------------------------------------ o que aparece

    /// <summary>Há memória do celular em uso: a playlist e os comandos aparecem.</summary>
    public bool TemMemoria => _player.Memoria is not null;

    /// <summary>"Playlist da FILME"; null sem memória do celular em uso.</summary>
    public string? Titulo => _player.Memoria is { } n ? Player.TituloPlaylist(_player.NomeMemoria(n)) : null;

    /// <summary>
    /// O aviso fixo: "Sem memória do celular em uso, não há playlist…" sem memória; "Playlist da FILME
    /// vazia. Toque em Adicionar músicas." com a playlist vazia; null com faixas.
    /// </summary>
    public string? TextoAviso => _player.Memoria is not { } n
        ? Player.MsgSemMemoria
        : _linhas.Count == 0 ? Player.MsgVazia(_player.NomeMemoria(n)) : null;

    /// <summary>As faixas da playlist, na ordem; a lista inteira é trocada quando muda.</summary>
    public IReadOnlyList<LinhaPlaylistViewModel> Linhas => _linhas;

    /// <summary>A linha pelo nome da faixa, ou null.</summary>
    public LinhaPlaylistViewModel? Linha(string nome) => _linhas.FirstOrDefault(l => l.Nome == nome);

    /// <summary>O nome da faixa atual, em cima dos botões, ou null com o Player parado.</summary>
    public string? FaixaAtual => _player.Atual?.Nome;

    public EstadoPlayer Estado => _player.Estado;

    /// <summary>"Pausar" enquanto toca; "Tocar" parado ou pausado.</summary>
    public string TextoTocarPausar => _player.Estado == EstadoPlayer.Tocando ? TextoPausar : TextoTocar;

    /// <summary>A última frase: "Nenhuma faixa disponível…", "Não consegui guardar a playlist no celular.".</summary>
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

    /// <summary>Uma edição da playlist está andando.</summary>
    public bool Ocupado => _agindo;

    // ------------------------------------------------------------------ editar

    /// <summary>O modo Editar: mostra − para remover e ≡ para ordenar.</summary>
    public bool Editando
    {
        get => _editando;
        private set
        {
            if (_editando == value)
                return;
            _editando = value;
            Avisar();
        }
    }

    public Comando Editar { get; }

    /// <summary>Sai do modo Editar.</summary>
    public Comando Pronto { get; }

    /// <summary>Abre o seletor do Android; as escolhidas entram no fim, na ordem escolhida.</summary>
    public Comando Adicionar { get; }

    private async Task AdicionarAsync()
    {
        if (SemMemoria() is not { } n)
            return;
        // O seletor aberto já conta como edição: um segundo toque não abre outro.
        await EditarPlaylist(async () =>
        {
            var escolhidos = await _seletor.EscolherAsync();
            if (escolhidos.Count == 0)
                return null; // desistiu: nada muda
            return await _playlists.AdicionarAsync(n, escolhidos);
        });
    }

    /// <summary>Tira a faixa da playlist. Se é a que toca, o Player para.</summary>
    public async Task RemoverAsync(string id)
    {
        if (SemMemoria() is not { } n)
            return;
        await EditarPlaylist(async () => await _playlists.RemoverAsync(n, id));
    }

    /// <summary>A alça ≡: move a faixa da posição <paramref name="de"/> para a <paramref name="para"/> (1 = a primeira).</summary>
    public async Task MoverAsync(int de, int para)
    {
        if (SemMemoria() is not { } n)
            return;
        if (de == para)
            return;
        await EditarPlaylist(async () => await _playlists.MoverAsync(n, de - 1, para - 1));
    }

    // ------------------------------------------------------------------ tocar

    /// <summary>Tocar/pausar: parado, toca da primeira; tocando, pausa; pausado, continua.</summary>
    public Comando TocarPausar { get; }

    /// <summary>Próxima: a faixa seguinte; depois da última, para.</summary>
    public Comando Proxima { get; }

    private Task TocarPausarAsync() => Comandar(_player.TocarPausarAsync);

    private Task ProximaAsync() => Comandar(_player.ProximaAsync);

    /// <summary>Tocar numa linha: toca aquela faixa.</summary>
    public Task TocarFaixaAsync(string id) => Comandar(() => _player.TocarFaixaAsync(id));

    private async Task Comandar(Func<Task> comando)
    {
        if (SemMemoria() is null)
            return;
        Mensagem = null;
        _ultimoAvisoPlayer = null;
        await comando();
        await NaTelaAsync(Atualizar);
    }

    // ------------------------------------------------------------------ apoio

    /// <summary>A memória do celular em uso, ou null com a frase "Sem memória do celular em uso…".</summary>
    private string? SemMemoria()
    {
        if (_player.Memoria is { } n)
            return n;
        Mensagem = Player.MsgSemMemoria;
        return null;
    }

    /// <summary>
    /// Roda uma edição da playlist, uma por vez: com outra andando (ou o seletor aberto), recusa sem
    /// fazer nada. Mostra a frase do resultado; null (desistiu) não muda a frase. A tela se atualiza na sua thread.
    /// </summary>
    private async Task EditarPlaylist(Func<Task<ResultadoPlaylist?>> acao)
    {
        if (_agindo)
            return;
        _agindo = true;
        Avisar(nameof(Ocupado));
        ResultadoPlaylist? resultado = null;
        string? erro = null;
        try
        {
            resultado = await acao();
        }
        catch (Exception e)
        {
            erro = $"Erro inesperado ({e.GetType().Name}).";
        }
        await NaTelaAsync(() =>
        {
            _agindo = false;
            if (erro is not null)
                Mensagem = erro;
            else if (resultado is not null)
                Mensagem = resultado.Mensagem;
            Atualizar();
        });
    }

    private async Task NaTelaAsync(Action acao)
    {
        var feito = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        NaTela(() =>
        {
            try
            {
                acao();
            }
            finally
            {
                feito.SetResult();
            }
        });
        await feito.Task;
    }

    private void AoMudarAlgo() => NaTela(Atualizar);

    private void Atualizar()
    {
        // Outra memória: a frase e o modo Editar da anterior saem.
        if (_player.Memoria != _memoriaVista)
        {
            _memoriaVista = _player.Memoria;
            Mensagem = null;
            Editando = false;
        }

        MontarLinhas();

        var atual = _player.Atual?.Id;
        foreach (var l in _linhas)
        {
            l.Tocando = l.Id == atual;
            l.Indisponivel = _player.Indisponivel(l.Id);
        }

        // Um aviso novo do Player (nenhuma faixa disponível, por exemplo) aparece.
        var aviso = _player.Aviso;
        if (aviso != _ultimoAvisoPlayer)
        {
            _ultimoAvisoPlayer = aviso;
            if (aviso is not null)
                Mensagem = aviso;
        }

        // Sem memória, não há o que editar.
        if (!TemMemoria)
            Editando = false;

        Avisar(nameof(TemMemoria));
        Avisar(nameof(Titulo));
        Avisar(nameof(TextoAviso));
        Avisar(nameof(FaixaAtual));
        Avisar(nameof(Estado));
        Avisar(nameof(TextoTocarPausar));
        Avisar(nameof(Ocupado));
    }

    /// <summary>Refaz as linhas a partir do Player, mantendo o mesmo objeto para o mesmo id.</summary>
    private void MontarLinhas()
    {
        var faixas = _player.Faixas;
        var novas = new List<LinhaPlaylistViewModel>(faixas.Count);
        for (var i = 0; i < faixas.Count; i++)
        {
            var f = faixas[i];
            if (!_porId.TryGetValue(f.Id, out var linha))
                _porId[f.Id] = linha = new LinhaPlaylistViewModel(f, this, Despachante, Relogio);
            linha.Posicao = i + 1;
            novas.Add(linha);
        }
        foreach (var id in _porId.Keys.Where(id => faixas.All(f => f.Id != id)).ToArray())
            _porId.Remove(id);

        if (_linhas.Count == novas.Count && _linhas.Zip(novas).All(par => ReferenceEquals(par.First, par.Second)))
            return;
        _linhas = novas;
        Avisar(nameof(Linhas));
    }

    public void Dispose()
    {
        _player.Mudou -= AoMudarAlgo;
        _playlists.Mudou -= AoMudarAlgo;
        _setups.Mudou -= AoMudarAlgo;
    }
}
