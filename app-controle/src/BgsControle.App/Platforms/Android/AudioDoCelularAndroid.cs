using Android.Content;
using Android.Media;
using BgsControle.Telas;

namespace BgsControle.App.Plataforma;

/// <summary>
/// O som do celular no Android (história 11, Player): se o áudio sai por um aparelho Bluetooth de áudio
/// (A2DP ou LE) ou pelo celular, ouvindo o <see cref="AudioManager"/>; as músicas que o reprodutor achou
/// sem acesso (a permissão da URI do seletor se perdeu; só escolher de novo no seletor devolve); e o atalho
/// para as configurações de Bluetooth.
/// </summary>
public sealed class AudioDoCelularAndroid : IAudioDoCelular
{
    private readonly Context _contexto;
    private readonly AudioManager? _audio;
    private readonly object _trava = new();
    private readonly HashSet<string> _semAcesso = new(StringComparer.Ordinal);

    public AudioDoCelularAndroid(Context contexto)
    {
        _contexto = contexto;
        _audio = (AudioManager?)contexto.GetSystemService(Context.AudioService);
        _audio?.RegisterAudioDeviceCallback(new Aviso(this), null);
    }

    public event Action? Mudou;

    public bool? SaindoPeloBluetooth
    {
        get
        {
            if (_audio?.GetDevices(GetDevicesTargets.Outputs) is not { } saidas)
                return null;
            return saidas.Any(d => d.Type == AudioDeviceType.BluetoothA2dp
                                   || (OperatingSystem.IsAndroidVersionAtLeast(31) && d.Type is AudioDeviceType.BleHeadset or AudioDeviceType.BleSpeaker));
        }
    }

    public bool SemAcesso(string uri)
    {
        lock (_trava)
            return _semAcesso.Contains(uri);
    }

    public void LimparSemAcesso()
    {
        lock (_trava)
        {
            if (_semAcesso.Count == 0)
                return;
            _semAcesso.Clear();
        }
        Avisar();
    }

    /// <summary>O reprodutor conta se o Android negou (true) ou deixou (false) ler a música da URI.</summary>
    public void MarcarSemAcesso(string uri, bool sem)
    {
        lock (_trava)
        {
            if (!(sem ? _semAcesso.Add(uri) : _semAcesso.Remove(uri)))
                return;
        }
        Avisar();
    }

    public void AbrirBluetooth()
    {
        var intent = new Intent(Android.Provider.Settings.ActionBluetoothSettings);
        intent.AddFlags(ActivityFlags.NewTask);
        try
        {
            _contexto.StartActivity(intent);
        }
        catch (ActivityNotFoundException)
        {
            // Sem a tela de Bluetooth no aparelho: nada a abrir.
        }
    }

    private void Avisar()
    {
        try
        {
            Mudou?.Invoke();
        }
        catch (Exception)
        {
            // Um aviso nunca derruba o app.
        }
    }

    /// <summary>Um aparelho de som entrou ou saiu (o Bluetooth do processador ligou ou desligou).</summary>
    private sealed class Aviso(AudioDoCelularAndroid dono) : AudioDeviceCallback
    {
        public override void OnAudioDevicesAdded(AudioDeviceInfo[]? addedDevices) => dono.Avisar();

        public override void OnAudioDevicesRemoved(AudioDeviceInfo[]? removedDevices) => dono.Avisar();
    }
}
