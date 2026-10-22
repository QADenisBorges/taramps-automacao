using BgsControle.Nucleo.Memorias;
using BgsControle.Nucleo.Registro;

namespace BgsControle.Nucleo.Player;

/// <summary>O que o Player está fazendo.</summary>
public enum EstadoPlayer
{
    Parado,
    Tocando,
    Pausado,
}

/// <summary>
/// O Player (CAP-15): toca a playlist da memória carregada (<see cref="GerenteMemorias.EmUso"/>) pela
/// porta <see cref="IReprodutor"/>. O som sai do celular pelo Bluetooth de áudio; nada vai ao
/// processador pelo BLE.
/// <list type="bullet">
/// <item>Sem memória carregada, nada de playlist nem de comandos.</item>
/// <item>Se a memória em uso muda (inclusive para nenhuma), o Player para na hora e passa a mostrar
/// a playlist da nova memória, parado (decisão do Denis, 22/10).</item>
/// <item>No fim de uma faixa, segue para a próxima; depois da última, para, e o próximo Tocar
/// recomeça pela primeira.</item>
/// <item>Uma faixa que o reprodutor recusa (o arquivo sumiu) fica marcada "Arquivo não encontrado"
/// e é pulada. Se nenhuma da playlist toca, para com "Nenhuma faixa disponível na playlist da NOME.".</item>
/// <item>Remover a faixa que está tocando (ou pausada) para o Player.</item>
/// </list>
/// Os comandos andam um de cada vez, na ordem.
/// </summary>
public sealed class Player : IDisposable
{
    public const string MsgSemMemoria = "Carregue uma memória para usar o Player.";
    public const string TextoIndisponivel = "Arquivo não encontrado";

    /// <summary>"Playlist da FILME".</summary>
    public static string TituloPlaylist(string nome) => $"Playlist da {nome}";

    /// <summary>"Playlist da FILME vazia. Toque em Adicionar músicas."</summary>
    public static string MsgVazia(string nome) => $"Playlist da {nome} vazia. Toque em Adicionar músicas.";

    /// <summary>"Nenhuma faixa disponível na playlist da FILME."</summary>
    public static string MsgNenhumaDisponivel(string nome) => $"Nenhuma faixa disponível na playlist da {nome}.";

    private readonly GerentePlaylists _playlists;
    private readonly GerenteMemorias _memorias;
    private readonly IReprodutor _reprodutor;
    private readonly LogBle _log;

    private readonly SemaphoreSlim _porta = new(1, 1);
    private readonly object _trava = new();
    private readonly HashSet<string> _indisponiveis = [];
    private int? _memoria;
    private EstadoPlayer _estado = EstadoPlayer.Parado;
    private Faixa? _atual;
    private string? _aviso;

    public Player(GerentePlaylists playlists, GerenteMemorias memorias, IReprodutor reprodutor, LogBle log)
    {
        _playlists = playlists;
        _memorias = memorias;
        _reprodutor = reprodutor;
        _log = log;
        _memoria = memorias.EmUso;

        _memorias.Mudou += AoMudarMemoria;
        _playlists.Mudou += AoMudarPlaylists;
        _reprodutor.Terminou += AoTerminar;
    }

    /// <summary>O estado, a faixa atual, a memória ou o aviso mudou. Pode chegar em qualquer thread.</summary>
    public event Action? Mudou;

    /// <summary>A memória cuja playlist o Player mostra (0 a 2): a carregada. Null = nenhuma.</summary>
    public int? Memoria
    {
        get
        {
            lock (_trava)
                return _memoria;
        }
    }

    public EstadoPlayer Estado
    {
        get
        {
            lock (_trava)
                return _estado;
        }
    }

    /// <summary>A faixa que toca ou está pausada, ou null com o Player parado.</summary>
    public Faixa? Atual
    {
        get
        {
            lock (_trava)
                return _atual;
        }
    }

    /// <summary>A última frase do Player ("Nenhuma faixa disponível…", "Carregue uma memória…"), ou null.</summary>
    public string? Aviso
    {
        get
        {
            lock (_trava)
                return _aviso;
        }
    }

    /// <summary>As faixas da playlist da memória carregada, na ordem; vazia sem memória.</summary>
    public IReadOnlyList<Faixa> Faixas => Memoria is { } n ? _playlists.Playlist(n) : [];

    /// <summary>O reprodutor recusou o arquivo desta faixa ("Arquivo não encontrado").</summary>
    public bool Indisponivel(string id)
    {
        lock (_trava)
            return _indisponiveis.Contains(id);
    }

    /// <summary>O nome da memória n (0 a 2) para os textos: o lido do aparelho, ou "Memória N".</summary>
    public string NomeMemoria(int n) => _memorias.Nome(n) is { Length: > 0 } nome ? nome : $"Memória {n + 1}";

    // ------------------------------------------------------------------ comandos

    /// <summary>Tocar/pausar: parado, toca a partir da primeira; tocando, pausa; pausado, continua.</summary>
    public Task TocarPausarAsync() => Executar(async () =>
    {
        if (ComMemoria() is not { } n)
            return;
        switch (Estado)
        {
            case EstadoPlayer.Tocando:
                await _reprodutor.PausarAsync();
                Definir(EstadoPlayer.Pausado, Atual);
                break;
            case EstadoPlayer.Pausado:
                await _reprodutor.ContinuarAsync();
                Definir(EstadoPlayer.Tocando, Atual);
                break;
            default:
                var lista = _playlists.Playlist(n);
                if (lista.Count == 0)
                {
                    Avisar(MsgVazia(NomeMemoria(n)));
                    return;
                }
                await TocarAPartirAsync(n, lista, 0);
                break;
        }
    });

    /// <summary>Próxima: toca a faixa seguinte à atual. Parado, não faz nada.</summary>
    public Task ProximaAsync() => Executar(async () =>
    {
        if (ComMemoria() is not { } n || Atual is not { } atual)
            return;
        var lista = _playlists.Playlist(n);
        await TocarAPartirAsync(n, lista, Posicao(lista, atual.Id) + 1);
    });

    /// <summary>Tocar numa linha: toca aquela faixa (se o arquivo sumiu, a próxima que tocar).</summary>
    public Task TocarFaixaAsync(string id) => Executar(async () =>
    {
        if (ComMemoria() is not { } n)
            return;
        var lista = _playlists.Playlist(n);
        var posicao = Posicao(lista, id);
        if (posicao < 0)
            return;
        await TocarAPartirAsync(n, lista, posicao);
    });

    // ------------------------------------------------------------------ fila

    /// <summary>
    /// Toca a primeira faixa que o reprodutor aceitar, de <paramref name="inicio"/> em diante; as
    /// recusadas ficam marcadas. Se nenhuma tocar, para (com o aviso se nenhuma da playlist está disponível).
    /// </summary>
    private async Task TocarAPartirAsync(int n, IReadOnlyList<Faixa> lista, int inicio)
    {
        Avisar(null);
        for (var i = Math.Max(inicio, 0); i < lista.Count; i++)
        {
            var faixa = lista[i];
            if (await _reprodutor.TocarAsync(faixa.Uri))
            {
                lock (_trava)
                    _indisponiveis.Remove(faixa.Id);
                Definir(EstadoPlayer.Tocando, faixa);
                _log.Evento($"player: tocando a faixa {i + 1} da playlist da memória {n + 1}");
                return;
            }
            lock (_trava)
                _indisponiveis.Add(faixa.Id);
            _log.Evento($"player: a faixa {i + 1} da playlist da memória {n + 1} não abriu (arquivo não encontrado)");
            Notificar();
        }

        await PararAsync();
        bool nenhuma;
        lock (_trava)
            nenhuma = lista.Count > 0 && lista.All(f => _indisponiveis.Contains(f.Id));
        if (nenhuma)
            Avisar(MsgNenhumaDisponivel(NomeMemoria(n)));
    }

    /// <summary>Para o reprodutor (se algo tocava) e fica parado, sem faixa atual.</summary>
    private async Task PararAsync()
    {
        if (Estado != EstadoPlayer.Parado)
            await _reprodutor.PararAsync();
        Definir(EstadoPlayer.Parado, null);
    }

    /// <summary>
    /// Antes de cada comando: se a memória em uso mudou, para e passa à playlist da nova, parado; se a
    /// faixa atual saiu da playlist, para.
    /// </summary>
    private async Task SincronizarAsync()
    {
        var emUso = _memorias.EmUso;
        bool trocou;
        lock (_trava)
        {
            trocou = emUso != _memoria;
            _memoria = emUso;
        }
        if (trocou)
        {
            await PararAsync();
            Avisar(null);
            _log.Evento(emUso is { } m ? $"player: memória {m + 1} em uso; parado" : "player: nenhuma memória em uso; parado");
            return;
        }

        if (Atual is { } atual && (emUso is not { } n || Posicao(_playlists.Playlist(n), atual.Id) < 0))
        {
            await PararAsync();
            _log.Evento("player: a faixa atual saiu da playlist; parado");
        }
    }

    // ------------------------------------------------------------------ eventos

    private void AoMudarMemoria()
    {
        if (_memorias.EmUso != Memoria)
            _ = Executar(() => Task.CompletedTask); // o Sincronizar para o Player na hora
    }

    private void AoMudarPlaylists()
    {
        Notificar();
        _ = Executar(() => Task.CompletedTask); // a faixa atual pode ter saído
    }

    private void AoTerminar(string uri) => _ = Executar(async () =>
    {
        if (Estado != EstadoPlayer.Tocando || Atual is not { } atual || atual.Uri != uri || Memoria is not { } n)
            return;
        var lista = _playlists.Playlist(n);
        await TocarAPartirAsync(n, lista, Posicao(lista, atual.Id) + 1);
    });

    // ------------------------------------------------------------------ apoio

    /// <summary>Roda o comando depois dos anteriores, já com a memória em uso conferida.</summary>
    private async Task Executar(Func<Task> acao)
    {
        await _porta.WaitAsync();
        try
        {
            await SincronizarAsync();
            await acao();
        }
        catch (Exception e)
        {
            _log.Evento($"player: erro inesperado ({e.GetType().Name})");
        }
        finally
        {
            _porta.Release();
        }
    }

    /// <summary>
    /// Início de um comando do Denis: a memória carregada (o aviso anterior sai), ou null com o aviso
    /// "Carregue uma memória para usar o Player.".
    /// </summary>
    private int? ComMemoria()
    {
        if (Memoria is { } n)
        {
            Avisar(null);
            return n;
        }
        Avisar(MsgSemMemoria);
        return null;
    }

    private static int Posicao(IReadOnlyList<Faixa> lista, string id)
    {
        for (var i = 0; i < lista.Count; i++)
        {
            if (lista[i].Id == id)
                return i;
        }
        return -1;
    }

    private void Definir(EstadoPlayer estado, Faixa? atual)
    {
        lock (_trava)
        {
            _estado = estado;
            _atual = atual;
        }
        Notificar();
    }

    private void Avisar(string? aviso)
    {
        lock (_trava)
            _aviso = aviso;
        Notificar();
    }

    private void Notificar()
    {
        try
        {
            Mudou?.Invoke();
        }
        catch (Exception e)
        {
            _log.Evento($"falha no aviso do player ({e.GetType().Name})");
        }
    }

    public void Dispose()
    {
        _memorias.Mudou -= AoMudarMemoria;
        _playlists.Mudou -= AoMudarPlaylists;
        _reprodutor.Terminou -= AoTerminar;
    }
}
