using Android.Content;
using Android.Media;
using BgsControle.Nucleo.Player;

namespace BgsControle.App.Plataforma;

/// <summary>
/// O reprodutor real do Player (história 11): o <c>MediaPlayer</c> do Android toca a URI de conteúdo
/// guardada (o app não copia a música) e avisa o fim da faixa. O som sai pelo que o Android estiver usando;
/// com o celular ligado ao processador pelo Bluetooth, vai até ele. A música continua com o app em segundo
/// plano enquanto o Android deixar (sem notificação nem serviço em primeiro plano nesta versão); o wake
/// lock parcial segura a CPU com a tela apagada.
/// <list type="bullet">
/// <item>Pede o foco de áudio ao tocar; perder o foco (uma ligação, outro app) pausa.</item>
/// <item>Se o Bluetooth de áudio cai, o Android avisa ACTION_AUDIO_BECOMING_NOISY: pausa, em vez de o som
/// ir para o alto-falante do celular. O Player mostra Pausado.</item>
/// <item>Um erro no meio da faixa não conta como fim: avisa <see cref="Falhou"/> (o Player a marca indisponível).</item>
/// <item>Nunca lança: o que não abre devolve false; sem acesso à URI, a música fica "sem acesso".</item>
/// </list>
/// </summary>
public sealed class ReprodutorAndroid : IReprodutor
{
    private readonly Context _contexto;
    private readonly AudioDoCelularAndroid _audio;
    private readonly AudioManager? _gerente;
    private readonly AudioAttributes _atributos;
    private readonly AudioFocusRequestClass? _foco;
    private readonly object _trava = new();
    private MediaPlayer? _tocador;
    private string? _uri;
    private TaskCompletionSource<bool>? _fimTrecho;
    private int _instancias;

    public ReprodutorAndroid(Context contexto, AudioDoCelularAndroid audio)
    {
        _contexto = contexto;
        _audio = audio;
        _gerente = (AudioManager?)contexto.GetSystemService(Context.AudioService);
        _atributos = new AudioAttributes.Builder()
            .SetUsage(AudioUsageKind.Media)!
            .SetContentType(AudioContentType.Music)!
            .Build()!;
        _foco = new AudioFocusRequestClass.Builder(AudioFocus.Gain)
            .SetAudioAttributes(_atributos)!
            .SetOnAudioFocusChangeListener(new OuvinteFoco(this))!
            .Build();
        var filtro = new IntentFilter(AudioManager.ActionAudioBecomingNoisy);
        if (OperatingSystem.IsAndroidVersionAtLeast(33))
            contexto.RegisterReceiver(new OuvinteRuido(this), filtro, ReceiverFlags.NotExported);
        else
            contexto.RegisterReceiver(new OuvinteRuido(this), filtro);
    }

    public event Action<string>? Terminou;

    public event Action<string>? Pausou;

    public event Action<string>? Falhou;

    /// <summary>
    /// O foco de áudio foi perdido de vez (outro app de mídia começou a tocar; história 13e, o fiscal do Extra). Pode chegar
    /// em qualquer thread.
    /// </summary>
    internal event Action? PerdeuFoco;

    /// <summary>Quantos MediaPlayer deste reprodutor existem agora (do criar ao soltar): os do app nas configurações de reprodução.</summary>
    internal int Instancias => Volatile.Read(ref _instancias);

    public TimeSpan? Posicao
    {
        get
        {
            lock (_trava)
            {
                try
                {
                    return _tocador is { } t ? TimeSpan.FromMilliseconds(t.CurrentPosition) : null;
                }
                catch (Java.Lang.IllegalStateException)
                {
                    return null;
                }
            }
        }
    }

    public async Task<bool> TocarAsync(string uri)
    {
        await PararAsync();
        MediaPlayer? tocador = null;
        try
        {
            tocador = Criar();
            tocador.SetAudioAttributes(_atributos);
            tocador.SetWakeMode(_contexto, Android.OS.WakeLockFlags.Partial);
            tocador.SetDataSource(_contexto, Android.Net.Uri.Parse(uri)!);
            // Preparar lê o arquivo: fora da thread da tela.
            await Task.Run(tocador.Prepare);
            var dono = tocador;
            tocador.Completion += (_, _) => AoTerminar(dono, uri);
            // Um erro no meio da faixa: tratado aqui (sem o Completion), não é fim de faixa.
            tocador.Error += (_, e) =>
            {
                e.Handled = true;
                AoFalhar(dono, uri);
            };
            PedirFoco();
            lock (_trava)
            {
                _tocador = tocador;
                _uri = uri;
            }
            tocador.Start();
            _audio.MarcarSemAcesso(uri, false);
            return true;
        }
        catch (Java.Lang.SecurityException)
        {
            // O Android negou ler a música (a permissão da URI se perdeu): "Sem acesso ao arquivo".
            Largar(tocador);
            _audio.MarcarSemAcesso(uri, true);
            return false;
        }
        catch (Exception)
        {
            // O arquivo sumiu ou não é áudio: "Arquivo não encontrado".
            Largar(tocador);
            return false;
        }
    }

    public Task PausarAsync()
    {
        lock (_trava)
        {
            try
            {
                if (_tocador is { IsPlaying: true } t)
                    t.Pause();
            }
            catch (Java.Lang.IllegalStateException)
            {
                // Já parado.
            }
        }
        return Task.CompletedTask;
    }

    public Task<bool> ContinuarAsync()
    {
        MediaPlayer? quebrado;
        lock (_trava)
        {
            if (_tocador is not { } t)
                return Task.FromResult(false);
            try
            {
                PedirFoco();
                t.Start();
                return Task.FromResult(true);
            }
            catch (Java.Lang.IllegalStateException)
            {
                // O MediaPlayer não está mais num estado que continua: fica parado.
                quebrado = t;
                _tocador = null;
                _uri = null;
            }
        }
        Largar(quebrado);
        return Task.FromResult(false);
    }

    /// <summary>
    /// O trecho do teste a seco (história 13e): prepara, vai ao início (<c>SeekTo</c>), toca e para no fim do trecho (ou
    /// no fim do arquivo). Parar, outro tocar, uma falha ou o cancelar terminam antes (false).
    /// </summary>
    public async Task<bool> TocarTrechoAsync(string uri, TimeSpan inicio, TimeSpan duracao, CancellationToken cancelar = default)
    {
        await PararAsync();
        if (cancelar.IsCancellationRequested)
            return false;
        MediaPlayer? tocador = null;
        var fim = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            tocador = Criar();
            tocador.SetAudioAttributes(_atributos);
            tocador.SetWakeMode(_contexto, Android.OS.WakeLockFlags.Partial);
            tocador.SetDataSource(_contexto, Android.Net.Uri.Parse(uri)!);
            await Task.Run(tocador.Prepare);
            var dono = tocador;
            // O fim do arquivo dentro do trecho: o trecho acabou.
            tocador.Completion += (_, _) =>
            {
                if (Soltar(dono))
                    fim.TrySetResult(true);
            };
            tocador.Error += (_, e) =>
            {
                e.Handled = true;
                if (Soltar(dono))
                    fim.TrySetResult(false);
            };
            if (inicio > TimeSpan.Zero)
                tocador.SeekTo((long)inicio.TotalMilliseconds, MediaPlayerSeekMode.Closest);
            PedirFoco();
            lock (_trava)
            {
                _tocador = tocador;
                _uri = uri;
                _fimTrecho = fim;
            }
            tocador.Start();
            _audio.MarcarSemAcesso(uri, false);
        }
        catch (Java.Lang.SecurityException)
        {
            Largar(tocador);
            _audio.MarcarSemAcesso(uri, true);
            return false;
        }
        catch (Exception)
        {
            Largar(tocador);
            return false;
        }

        bool tocou;
        using (cancelar.Register(() => fim.TrySetResult(false)))
        {
            var prazo = Task.Delay(duracao);
            var primeiro = await Task.WhenAny(fim.Task, prazo);
            // No prazo, o trecho só conta inteiro se a posição chegou ao fim dele (um seek lento ou uma trava não passam).
            tocou = primeiro == prazo ? ChegouAoFim(tocador!, inicio + duracao) : await fim.Task;
        }
        bool ainda;
        lock (_trava)
        {
            ainda = ReferenceEquals(_tocador, tocador);
            if (ReferenceEquals(_fimTrecho, fim))
                _fimTrecho = null;
        }
        if (ainda)
            await PararAsync();
        return tocou && !cancelar.IsCancellationRequested;
    }

    /// <summary>A posição do tocador (ainda o atual e tocando) chegou a <paramref name="fim"/> − 0,5 s.</summary>
    private bool ChegouAoFim(MediaPlayer tocador, TimeSpan fim)
    {
        lock (_trava)
        {
            try
            {
                return ReferenceEquals(_tocador, tocador) && tocador.IsPlaying
                       && tocador.CurrentPosition >= (fim - TimeSpan.FromSeconds(0.5)).TotalMilliseconds;
            }
            catch (Java.Lang.IllegalStateException)
            {
                return false;
            }
        }
    }

    /// <summary>O MediaPlayer do Player (ou do trecho) está tocando agora (o fiscal do Extra não o conta como outro app).</summary>
    internal bool Tocando
    {
        get
        {
            lock (_trava)
            {
                try
                {
                    return _tocador is { IsPlaying: true };
                }
                catch (Java.Lang.IllegalStateException)
                {
                    return false;
                }
            }
        }
    }

    public Task PararAsync()
    {
        MediaPlayer? tocador;
        TaskCompletionSource<bool>? fimTrecho;
        lock (_trava)
        {
            tocador = _tocador;
            _tocador = null;
            _uri = null;
            fimTrecho = _fimTrecho;
            _fimTrecho = null;
        }
        fimTrecho?.TrySetResult(false);
        if (tocador is not null)
        {
            try
            {
                tocador.Stop();
            }
            catch (Java.Lang.IllegalStateException)
            {
                // Já parado.
            }
            Largar(tocador);
            if (_foco is not null)
                _gerente?.AbandonAudioFocusRequest(_foco);
        }
        return Task.CompletedTask;
    }

    private void PedirFoco()
    {
        try
        {
            if (_foco is not null)
                _gerente?.RequestAudioFocus(_foco);
        }
        catch (Exception)
        {
            // Sem o foco, toca assim mesmo.
        }
    }

    /// <summary>Perdeu o foco ou o Bluetooth caiu: pausa e avisa o Player (que mostra Pausado).</summary>
    private void PausarSozinho()
    {
        string? uri = null;
        TaskCompletionSource<bool>? fimTrecho;
        lock (_trava)
        {
            // Um trecho do teste a seco que pausa sozinho não tocou inteiro.
            fimTrecho = _fimTrecho;
            try
            {
                if (_tocador is { IsPlaying: true } t)
                {
                    t.Pause();
                    uri = _uri;
                }
            }
            catch (Java.Lang.IllegalStateException)
            {
                // Já parado.
            }
        }
        fimTrecho?.TrySetResult(false);
        if (uri is not null)
            Pausou?.Invoke(uri);
    }

    private void AoTerminar(MediaPlayer tocador, string uri)
    {
        if (!Soltar(tocador))
            return;
        Terminou?.Invoke(uri);
    }

    private void AoFalhar(MediaPlayer tocador, string uri)
    {
        if (!Soltar(tocador))
            return;
        Falhou?.Invoke(uri);
    }

    /// <summary>Solta o tocador se ele ainda é o atual. False = outra faixa já tomou o lugar.</summary>
    private bool Soltar(MediaPlayer tocador)
    {
        lock (_trava)
        {
            if (!ReferenceEquals(_tocador, tocador))
                return false;
            _tocador = null;
            _uri = null;
        }
        Largar(tocador);
        return true;
    }

    /// <summary>Um MediaPlayer novo, contado em <see cref="Instancias"/> até o <see cref="Largar"/>.</summary>
    private MediaPlayer Criar()
    {
        Interlocked.Increment(ref _instancias);
        return new MediaPlayer();
    }

    private void Largar(MediaPlayer? tocador)
    {
        if (tocador is null)
            return;
        Interlocked.Decrement(ref _instancias);
        try
        {
            tocador.Release();
            tocador.Dispose();
        }
        catch (Exception)
        {
            // Soltar nunca derruba o Player.
        }
    }

    private sealed class OuvinteFoco(ReprodutorAndroid dono) : Java.Lang.Object, AudioManager.IOnAudioFocusChangeListener
    {
        public void OnAudioFocusChange(AudioFocus focusChange)
        {
            if (focusChange is AudioFocus.Loss or AudioFocus.LossTransient)
                dono.PausarSozinho();
            if (focusChange is AudioFocus.Loss)
                dono.PerdeuFoco?.Invoke();
        }
    }

    private sealed class OuvinteRuido(ReprodutorAndroid dono) : BroadcastReceiver
    {
        public override void OnReceive(Context? context, Intent? intent)
        {
            if (intent?.Action == AudioManager.ActionAudioBecomingNoisy)
                dono.PausarSozinho();
        }
    }
}
