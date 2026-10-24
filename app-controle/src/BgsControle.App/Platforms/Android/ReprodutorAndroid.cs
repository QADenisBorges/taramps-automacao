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

    public async Task<bool> TocarAsync(string uri)
    {
        await PararAsync();
        MediaPlayer? tocador = null;
        try
        {
            tocador = new MediaPlayer();
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

    public Task PararAsync()
    {
        MediaPlayer? tocador;
        lock (_trava)
        {
            tocador = _tocador;
            _tocador = null;
            _uri = null;
        }
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
        lock (_trava)
        {
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

    private static void Largar(MediaPlayer? tocador)
    {
        if (tocador is null)
            return;
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
