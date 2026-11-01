using Android.Content;
using Android.Media;
using BgsControle.Nucleo.Player;

namespace BgsControle.App.Plataforma;

/// <summary>
/// O volume de mídia do celular (história 13e, o fiscal do Extra): o <see cref="AudioManager"/> lê e põe o volume do
/// <c>STREAM_MUSIC</c>, e o aviso <c>android.media.VOLUME_CHANGED_ACTION</c> desse stream diz quando ele muda. As
/// correções do app não mostram a barra de volume. Se o volume não chega ao pedido (o volume seguro do Android, o "Não
/// perturbe"), o <see cref="DefinirAsync"/> devolve false e o app avisa.
/// </summary>
public sealed class VolumeCelularAndroid : IVolumeCelular
{
    private const string AcaoVolume = "android.media.VOLUME_CHANGED_ACTION";
    private const string ExtraStream = "android.media.EXTRA_VOLUME_STREAM_TYPE";

    private readonly AudioManager? _gerente;
    private int _ultimo;

    public VolumeCelularAndroid(Context contexto)
    {
        _gerente = (AudioManager?)contexto.GetSystemService(Context.AudioService);
        _ultimo = Volume;
        var filtro = new IntentFilter(AcaoVolume);
        if (OperatingSystem.IsAndroidVersionAtLeast(33))
            contexto.RegisterReceiver(new Ouvinte(this), filtro, ReceiverFlags.NotExported);
        else
            contexto.RegisterReceiver(new Ouvinte(this), filtro);
    }

    public event Action? Mudou;

    public int Volume => _gerente?.GetStreamVolume(Android.Media.Stream.Music) ?? 0;

    public int Maximo => _gerente?.GetStreamMaxVolume(Android.Media.Stream.Music) ?? 0;

    public Task<bool> DefinirAsync(int volume)
    {
        var alvo = Math.Clamp(volume, 0, Maximo);
        try
        {
            _gerente?.SetStreamVolume(Android.Media.Stream.Music, alvo, 0);
        }
        catch (Java.Lang.SecurityException)
        {
            // O "Não perturbe" pode impedir: o volume fica, e quem pediu avisa.
        }
        AoMudar();
        return Task.FromResult(Volume >= alvo);
    }

    private void AoMudar()
    {
        var v = Volume;
        if (Interlocked.Exchange(ref _ultimo, v) == v)
            return;
        try
        {
            Mudou?.Invoke();
        }
        catch (Exception)
        {
            // Quem ouve não derruba o aviso do Android.
        }
    }

    private sealed class Ouvinte(VolumeCelularAndroid dono) : BroadcastReceiver
    {
        public override void OnReceive(Context? context, Intent? intent)
        {
            if (intent?.Action == AcaoVolume && intent.GetIntExtra(ExtraStream, -1) == (int)Android.Media.Stream.Music)
                dono.AoMudar();
        }
    }
}
