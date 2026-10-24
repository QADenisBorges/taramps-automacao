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
/// </summary>
public sealed class ReprodutorAndroid(Context contexto, AudioDoCelularAndroid audio) : IReprodutor
{
    private readonly object _trava = new();
    private MediaPlayer? _tocador;

    public event Action<string>? Terminou;

    public async Task<bool> TocarAsync(string uri)
    {
        await PararAsync();
        var tocador = new MediaPlayer();
        try
        {
            var atributos = new AudioAttributes.Builder()
                .SetUsage(AudioUsageKind.Media)!
                .SetContentType(AudioContentType.Music)!
                .Build()!;
            tocador.SetAudioAttributes(atributos);
            tocador.SetWakeMode(contexto, Android.OS.WakeLockFlags.Partial);
            tocador.SetDataSource(contexto, Android.Net.Uri.Parse(uri)!);
            // Preparar lê o arquivo: fora da thread da tela.
            await Task.Run(tocador.Prepare);
        }
        catch (Java.Lang.SecurityException)
        {
            // O Android negou ler a música (a permissão da URI se perdeu): "Sem permissão".
            Soltar(tocador);
            audio.MarcarSemPermissao(true);
            return false;
        }
        catch (Exception)
        {
            // O arquivo sumiu ou não é áudio: "Arquivo não encontrado".
            Soltar(tocador);
            return false;
        }

        audio.MarcarSemPermissao(false);
        tocador.Completion += (_, _) => AoTerminar(tocador, uri);
        lock (_trava)
        {
            _tocador = tocador;
        }
        tocador.Start();
        return true;
    }

    public Task PausarAsync()
    {
        lock (_trava)
        {
            if (_tocador is { IsPlaying: true } t)
                t.Pause();
        }
        return Task.CompletedTask;
    }

    public Task ContinuarAsync()
    {
        lock (_trava)
            _tocador?.Start();
        return Task.CompletedTask;
    }

    public Task PararAsync()
    {
        MediaPlayer? tocador;
        lock (_trava)
        {
            tocador = _tocador;
            _tocador = null;
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
            Soltar(tocador);
        }
        return Task.CompletedTask;
    }

    private void AoTerminar(MediaPlayer tocador, string uri)
    {
        lock (_trava)
        {
            if (!ReferenceEquals(_tocador, tocador))
                return; // outra faixa já tomou o lugar
            _tocador = null;
        }
        Soltar(tocador);
        Terminou?.Invoke(uri);
    }

    private static void Soltar(MediaPlayer tocador)
    {
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
}
