using Android.Content;
using Android.Content.PM;
using Android.Media;
using BgsControle.Telas;

namespace BgsControle.App.Plataforma;

/// <summary>
/// O acesso às músicas do celular: READ_MEDIA_AUDIO no Android 13+, READ_EXTERNAL_STORAGE até o 12. O
/// seletor do Android já dá acesso a cada música escolhida (a permissão persistente da URI); esta é a
/// reserva quando ela se perde.
/// </summary>
public sealed class PermissaoMusicas : Permissions.BasePlatformPermission
{
    private static string Necessaria =>
        OperatingSystem.IsAndroidVersionAtLeast(33)
            ? Android.Manifest.Permission.ReadMediaAudio
            : Android.Manifest.Permission.ReadExternalStorage;

    public override (string androidPermission, bool isRuntime)[] RequiredPermissions => [(Necessaria, true)];
}

/// <summary>
/// O som do celular no Android (história 11, Player): se o áudio sai por um aparelho Bluetooth de áudio
/// (A2DP ou LE) ou pelo celular, ouvindo o <see cref="AudioManager"/>; o "Sem permissão" que o reprodutor
/// marca; o atalho para as configurações de Bluetooth; e o pedido de acesso às músicas.
/// </summary>
public sealed class AudioDoCelularAndroid : IAudioDoCelular
{
    private readonly Context _contexto;
    private readonly AudioManager? _audio;
    private bool _semPermissao;

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

    public bool SemPermissao => Volatile.Read(ref _semPermissao);

    /// <summary>O reprodutor conta se o Android negou (true) ou deixou (false) ler uma música.</summary>
    public void MarcarSemPermissao(bool sem)
    {
        if (SemPermissao == sem)
            return;
        Volatile.Write(ref _semPermissao, sem);
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

    public async Task<bool> PedirPermissaoAsync()
    {
        var status = await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var agora = await Permissions.CheckStatusAsync<PermissaoMusicas>();
            return agora == PermissionStatus.Granted ? agora : await Permissions.RequestAsync<PermissaoMusicas>();
        });
        var permitido = status == PermissionStatus.Granted;
        if (permitido)
            MarcarSemPermissao(false);
        return permitido;
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
