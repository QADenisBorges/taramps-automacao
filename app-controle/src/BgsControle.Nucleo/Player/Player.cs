using BgsControle.Nucleo.Registro;
using BgsControle.Nucleo.Setups;

namespace BgsControle.Nucleo.Player;

/// <summary>O que o Player está fazendo.</summary>
public enum EstadoPlayer
{
    Parado,
    Tocando,
    Pausado,
}

/// <summary>
/// O Player (CAP-15): toca a playlist da memória do celular em uso (<see cref="GerenteSetups.EmUso"/>)
/// pela porta <see cref="IReprodutor"/>. O som sai do celular pelo Bluetooth de áudio; nada vai ao
/// processador pelo BLE.
/// <list type="bullet">
/// <item>Sem memória do celular em uso (memória do aparelho, "Som do aparelho", sem Setup,
/// desconectado), nada de playlist nem de comandos.</item>
/// <item>Se a memória em uso muda (inclusive para nenhuma), o Player para na hora e passa a mostrar
/// a playlist da nova memória, parado (decisão do Denis, 22/10).</item>
/// <item>A <b>queda do BLE</b> não conta como troca (decisão do Denis, 24/10/2026): o Player continua
/// tocando a playlist da memória que estava em uso, e ela fica na tela, também durante a conferência,
/// com "som diferente" e na dúvida. Uma mudança do som em uso depois de reconectar a solta: a mesma
/// memória segue tocando; outra memória do celular, uma memória do aparelho carregada, Seguir sem
/// Setup ou um Setup sem memórias param o Player e trocam a playlist.</item>
/// <item>No fim de uma faixa, segue para a próxima; depois da última, para, e o próximo Tocar
/// recomeça pela primeira.</item>
/// <item>Uma faixa que o reprodutor recusa (o arquivo sumiu) fica marcada "Arquivo não encontrado"
/// e é pulada. Se nenhuma da playlist toca, para com "Nenhuma faixa disponível na playlist da NOME.".</item>
/// <item>Remover a faixa que está tocando (ou pausada) para o Player.</item>
/// <item>O reprodutor que pausa sozinho (Bluetooth de áudio caiu, foco de áudio perdido) deixa o Player
/// Pausado; uma faixa que falha no meio fica "Arquivo não encontrado" e o Player segue para a próxima.</item>
/// <item>Uma faixa em análise (história 13d, <see cref="EmAnalise"/>) não toca: a sequência a pula, e tocá-la direto dá
/// "Esta faixa ainda está em análise.".</item>
/// </list>
/// Os comandos andam um de cada vez, na ordem.
/// </summary>
public sealed class Player : IDisposable
{
    public const string MsgSemMemoria = "Sem memória do celular em uso, não há playlist. Aplique uma memória do celular.";
    public const string TextoIndisponivel = "Arquivo não encontrado";

    /// <summary>Tocar direto uma faixa em análise (história 13d).</summary>
    public const string MsgEmAnalise = "Esta faixa ainda está em análise.";

    /// <summary>"Playlist da FILME".</summary>
    public static string TituloPlaylist(string nome) => $"Playlist da {nome}";

    /// <summary>"Playlist da FILME vazia. Toque em Adicionar músicas."</summary>
    public static string MsgVazia(string nome) => $"Playlist da {nome} vazia. Toque em Adicionar músicas.";

    /// <summary>"Nenhuma faixa disponível na playlist da FILME."</summary>
    public static string MsgNenhumaDisponivel(string nome) => $"Nenhuma faixa disponível na playlist da {nome}.";

    private readonly GerentePlaylists _playlists;
    private readonly GerenteSetups _setups;
    private readonly IReprodutor _reprodutor;
    private readonly LogBle _log;

    private readonly SemaphoreSlim _porta = new(1, 1);
    private readonly object _trava = new();
    private readonly HashSet<string> _indisponiveis = [];
    private string? _memoria;
    private bool _retida;
    private EstadoPlayer _estado = EstadoPlayer.Parado;
    private Faixa? _atual;
    private string? _aviso;

    public Player(GerentePlaylists playlists, GerenteSetups setups, IReprodutor reprodutor, LogBle log)
    {
        _playlists = playlists;
        _setups = setups;
        _reprodutor = reprodutor;
        _log = log;
        _memoria = MemoriaEmUso();

        _setups.Mudou += AoMudarMemoria;
        _playlists.Mudou += AoMudarPlaylists;
        _reprodutor.Terminou += AoTerminar;
        _reprodutor.Pausou += AoPausarSozinho;
        _reprodutor.Falhou += AoFalhar;
    }

    /// <summary>O estado, a faixa atual, a memória ou o aviso mudou. Pode chegar em qualquer thread.</summary>
    public event Action? Mudou;

    /// <summary>
    /// A memória do celular cuja playlist o Player mostra (o id): a em uso, ou, depois de uma queda, a
    /// que estava em uso (<see cref="Retida"/>). Null = nenhuma.
    /// </summary>
    public string? Memoria
    {
        get
        {
            lock (_trava)
                return _memoria;
        }
    }

    /// <summary>
    /// O Player ficou com a memória de antes de a conexão cair (decisão do Denis, 24/10/2026), até uma
    /// memória do celular entrar em uso depois de reconectar.
    /// </summary>
    public bool Retida
    {
        get
        {
            lock (_trava)
                return _retida;
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

    /// <summary>A última frase do Player ("Nenhuma faixa disponível…", "Sem memória do celular em uso…"), ou null.</summary>
    public string? Aviso
    {
        get
        {
            lock (_trava)
                return _aviso;
        }
    }

    /// <summary>As faixas da playlist da memória em uso, na ordem; vazia sem memória.</summary>
    public IReadOnlyList<Faixa> Faixas => _playlists.Playlist(Memoria);

    /// <summary>
    /// A faixa (pelo id) está em análise e não toca (história 13d; no app, a análise da playlist). Null: nenhuma está.
    /// </summary>
    public Func<string, bool>? EmAnalise { get; set; }

    /// <summary>
    /// Por que o Player não toca agora (história 13e: o teste a seco usa o reprodutor), ou null. Tocar, continuar e a
    /// Próxima dão o aviso; pausar sempre vale.
    /// </summary>
    public Func<string?>? Bloqueio { get; set; }

    /// <summary>
    /// O descanso do Extra (história 13f): por que o Player não toca agora ("Descansando: 12:34"), ou null. Vale como o
    /// <see cref="Bloqueio"/>, com quem ele se soma (os dois têm donos diferentes).
    /// </summary>
    public Func<string?>? Descanso { get; set; }

    /// <summary>
    /// Antes de começar ou continuar a tocar por um comando (história 13e: o fiscal do Extra tira o mudo do outro player
    /// e põe o volume do celular no máximo). Uma falha fica no log e não impede o tocar.
    /// </summary>
    public Func<Task>? AntesDeTocar { get; set; }

    /// <summary>Não deu para conferir o descanso do Extra (o Player não toca: o lado seguro).</summary>
    public const string MsgDescansoFalhou = "Não consegui conferir o descanso do Extra.";

    /// <summary>
    /// O motivo do bloqueio agora: o <see cref="Bloqueio"/> (uma falha da pergunta conta como nenhum) e, em separado, o
    /// <see cref="Descanso"/> (uma falha dele bloqueia).
    /// </summary>
    private string? Bloqueado()
    {
        string? motivo = null;
        try
        {
            motivo = Bloqueio?.Invoke();
        }
        catch (Exception e)
        {
            _log.Evento($"player: falha ao conferir o bloqueio ({e.GetType().Name})");
        }
        if (motivo is not null)
            return motivo;
        try
        {
            return Descanso?.Invoke();
        }
        catch (Exception e)
        {
            _log.Evento($"player: falha ao conferir o descanso do Extra ({e.GetType().Name})");
            return MsgDescansoFalhou;
        }
    }

    /// <summary>Roda o <see cref="AntesDeTocar"/>.</summary>
    private async Task PrepararTocarAsync()
    {
        if (AntesDeTocar is not { } antes)
            return;
        try
        {
            await antes();
        }
        catch (Exception e)
        {
            _log.Evento($"player: falha antes de tocar ({e.GetType().Name})");
        }
    }

    /// <summary>A faixa está em análise agora (uma falha da pergunta conta como não).</summary>
    private bool NaAnalise(string id)
    {
        try
        {
            return EmAnalise?.Invoke(id) == true;
        }
        catch (Exception e)
        {
            _log.Evento($"player: falha ao conferir a análise ({e.GetType().Name})");
            return false;
        }
    }

    /// <summary>O reprodutor recusou o arquivo desta faixa ("Arquivo não encontrado").</summary>
    public bool Indisponivel(string id)
    {
        lock (_trava)
            return _indisponiveis.Contains(id);
    }

    /// <summary>O nome da memória do celular para os textos ("FILME"), ou "memória" se ela sumiu.</summary>
    public string NomeMemoria(string id) => _setups.Memoria(id)?.Nome ?? "memória";

    /// <summary>A memória do celular em uso agora (o id), ou null.</summary>
    private string? MemoriaEmUso() => _setups.EmUso is { Tipo: TipoSomEmUso.MemoriaCelular } uso ? uso.IdMemoria : null;

    // ------------------------------------------------------------------ comandos

    /// <summary>Tocar/pausar: parado, toca a partir da primeira; tocando, pausa; pausado, continua.</summary>
    public Task TocarPausarAsync() => Executar(async () =>
    {
        if (ComMemoria() is not { } n)
            return;
        if (Estado != EstadoPlayer.Tocando && Bloqueado() is { } bloqueio)
        {
            Avisar(bloqueio);
            return;
        }
        if (Estado != EstadoPlayer.Tocando)
            await PrepararTocarAsync();
        switch (Estado)
        {
            case EstadoPlayer.Tocando:
                await _reprodutor.PausarAsync();
                Definir(EstadoPlayer.Pausado, Atual);
                break;
            case EstadoPlayer.Pausado:
                if (await _reprodutor.ContinuarAsync())
                {
                    Definir(EstadoPlayer.Tocando, Atual);
                }
                else
                {
                    Definir(EstadoPlayer.Parado, null);
                    _log.Evento("player: não deu para continuar a faixa; parado");
                }
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

    /// <summary>
    /// Pausar (história 13b, o tom do celular na calibração): tocando, pausa onde está; parado ou pausado, nada
    /// muda. O Tocar/pausar continua de onde parou.
    /// </summary>
    public Task PausarAsync(string motivo = "para o tom do celular") => Executar(async () =>
    {
        if (Estado != EstadoPlayer.Tocando)
            return;
        await _reprodutor.PausarAsync();
        Definir(EstadoPlayer.Pausado, Atual);
        _log.Evento($"player: pausado {motivo}");
    });

    /// <summary>
    /// Parar (história 13e, o teste a seco): tocando ou pausado, para e solta o reprodutor, sem faixa atual; o próximo
    /// Tocar recomeça pela primeira.
    /// </summary>
    public Task PararAsync(string motivo) => Executar(async () =>
    {
        if (Estado == EstadoPlayer.Parado)
            return;
        await PararAsync();
        _log.Evento($"player: parado {motivo}");
    });

    /// <summary>Próxima: toca a faixa seguinte à atual. Parado, não faz nada.</summary>
    public Task ProximaAsync() => Executar(async () =>
    {
        if (ComMemoria() is not { } n || Atual is not { } atual)
            return;
        if (Bloqueado() is { } bloqueio)
        {
            Avisar(bloqueio);
            return;
        }
        // Do pausado (o mudo do outro player, por exemplo), a Próxima começa a tocar: o antes de tocar vale.
        if (Estado != EstadoPlayer.Tocando)
            await PrepararTocarAsync();
        var lista = _playlists.Playlist(n);
        await TocarAPartirAsync(n, lista, Posicao(lista, atual.Id) + 1);
    });

    /// <summary>
    /// Tocar numa linha: toca aquela faixa (se o arquivo sumiu, a próxima que tocar). Em análise, não toca: "Esta faixa
    /// ainda está em análise." (o que tocava segue).
    /// </summary>
    public Task TocarFaixaAsync(string id) => Executar(async () =>
    {
        if (ComMemoria() is not { } n)
            return;
        var lista = _playlists.Playlist(n);
        var posicao = Posicao(lista, id);
        if (posicao < 0)
            return;
        if (NaAnalise(id))
        {
            Avisar(MsgEmAnalise);
            _log.Evento($"player: a faixa {posicao + 1} da playlist da {NomeMemoria(n)} está em análise; não toca");
            return;
        }
        if (Bloqueado() is { } bloqueio)
        {
            Avisar(bloqueio);
            return;
        }
        await PrepararTocarAsync();
        await TocarAPartirAsync(n, lista, posicao);
    });

    // ------------------------------------------------------------------ fila

    /// <summary>
    /// Toca a primeira faixa que o reprodutor aceitar, de <paramref name="inicio"/> em diante; as
    /// recusadas ficam marcadas, e as em análise são puladas. Se nenhuma tocar, para (com o aviso se nenhuma da
    /// playlist está disponível).
    /// </summary>
    private async Task TocarAPartirAsync(string n, IReadOnlyList<Faixa> lista, int inicio)
    {
        Avisar(null);
        for (var i = Math.Max(inicio, 0); i < lista.Count; i++)
        {
            var faixa = lista[i];
            if (NaAnalise(faixa.Id))
            {
                _log.Evento($"player: a faixa {i + 1} da playlist da {NomeMemoria(n)} está em análise; pulada");
                continue;
            }
            if (await _reprodutor.TocarAsync(faixa.Uri))
            {
                lock (_trava)
                    _indisponiveis.Remove(faixa.Id);
                Definir(EstadoPlayer.Tocando, faixa);
                _log.Evento($"player: tocando a faixa {i + 1} da playlist da {NomeMemoria(n)}");
                return;
            }
            lock (_trava)
                _indisponiveis.Add(faixa.Id);
            _log.Evento($"player: a faixa {i + 1} da playlist da {NomeMemoria(n)} não abriu (arquivo não encontrado)");
            Notificar();
        }

        await PararAsync();
        var emAnalise = lista.Where(f => NaAnalise(f.Id)).Select(f => f.Id).ToHashSet();
        bool nenhuma;
        lock (_trava)
            nenhuma = lista.Count > 0 && lista.All(f => _indisponiveis.Contains(f.Id) || emAnalise.Contains(f.Id));
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
        var uso = _setups.EmUso;
        var emUso = uso is { Tipo: TipoSomEmUso.MemoriaCelular } ? uso.IdMemoria : null;
        RegistrarQueda();
        bool trocou;
        bool manteve;
        lock (_trava)
        {
            manteve = _retida && MantemRetida(uso, emUso);
            if (manteve)
            {
                trocou = false;
            }
            else
            {
                if (_retida)
                    _log.Evento("player: depois de reconectar, o som em uso mudou; a playlist retida sai");
                _retida = false;
                trocou = emUso != _memoria;
                _memoria = emUso;
            }
        }
        if (manteve)
        {
            if (Atual is { } faixa && Memoria is { } m && Posicao(_playlists.Playlist(m), faixa.Id) < 0)
            {
                await PararAsync();
                _log.Evento("player: a faixa atual saiu da playlist; parado");
            }
            return;
        }
        if (trocou)
        {
            await PararAsync();
            Avisar(null);
            _log.Evento(emUso is { } m ? $"player: {NomeMemoria(m)} em uso; parado" : "player: nenhuma memória do celular em uso; parado");
            return;
        }

        if (Atual is { } atual && (emUso is not { } n || Posicao(_playlists.Playlist(n), atual.Id) < 0))
        {
            await PararAsync();
            _log.Evento("player: a faixa atual saiu da playlist; parado");
        }
    }

    /// <summary>
    /// A conexão caiu com uma memória do celular no Player: ele fica com ela (decisão do Denis,
    /// 24/10/2026). Chamado já no aviso da queda, para uma reconexão rápida não perder a marca.
    /// </summary>
    private void RegistrarQueda()
    {
        if (_setups.ConexaoAberta)
            return;
        string? memoria;
        lock (_trava)
        {
            if (_retida || _memoria is null)
                return;
            _retida = true;
            memoria = _memoria;
        }
        _log.Evento($"player: a conexão caiu; continua com a playlist da {NomeMemoria(memoria)}");
        Notificar();
    }

    /// <summary>
    /// Com a memória retida, o que a solta: uma mudança do som em uso feita com a conexão aberta e já
    /// conferida (outra memória do celular, ou a mesma, que só tira a marca; Seguir sem Setup; um Setup
    /// sem memórias). Ficam com ela: a queda, a conferência ainda
    /// por vir, "som diferente", a dúvida e a troca de Setup. Chamar com a trava.
    /// </summary>
    private bool MantemRetida(SomEmUso uso, string? emUso)
    {
        if (!_setups.ConexaoAberta || !_setups.Conferido)
            return true;
        if (emUso is not null)
            return false;
        return _setups.Estado is EstadoConferencia.SomDiferente or EstadoConferencia.Duvida or EstadoConferencia.Trocando;
    }

    // ------------------------------------------------------------------ eventos

    private void AoMudarMemoria()
    {
        // A queda fica marcada na hora (antes de qualquer reconexão).
        RegistrarQueda();
        // O Sincronizar para o Player na hora; com a memória retida, confere se ela sai.
        if (MemoriaEmUso() != Memoria || Retida)
            _ = Executar(() => Task.CompletedTask);
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

    private void AoPausarSozinho(string uri) => _ = Executar(() =>
    {
        if (Estado == EstadoPlayer.Tocando && Atual is { } atual && atual.Uri == uri)
        {
            Definir(EstadoPlayer.Pausado, atual);
            _log.Evento("player: o reprodutor pausou sozinho (Bluetooth de áudio ou foco perdido)");
        }
        return Task.CompletedTask;
    });

    /// <summary>A faixa falhou no meio: fica "Arquivo não encontrado" e o Player segue para a próxima.</summary>
    private void AoFalhar(string uri) => _ = Executar(async () =>
    {
        if (Estado == EstadoPlayer.Parado || Atual is not { } atual || atual.Uri != uri || Memoria is not { } n)
            return;
        lock (_trava)
            _indisponiveis.Add(atual.Id);
        Definir(EstadoPlayer.Parado, null);
        _log.Evento($"player: uma faixa da playlist da {NomeMemoria(n)} falhou no meio");
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
    /// Início de um comando do Denis: a memória do celular em uso (o aviso anterior sai), ou null com o
    /// aviso "Sem memória do celular em uso, não há playlist…".
    /// </summary>
    private string? ComMemoria()
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
        _setups.Mudou -= AoMudarMemoria;
        _playlists.Mudou -= AoMudarPlaylists;
        _reprodutor.Terminou -= AoTerminar;
        _reprodutor.Pausou -= AoPausarSozinho;
        _reprodutor.Falhou -= AoFalhar;
    }
}
