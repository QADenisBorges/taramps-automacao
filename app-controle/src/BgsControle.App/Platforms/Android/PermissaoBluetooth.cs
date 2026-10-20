using Android.Bluetooth;
using Android.Content;
using Android.Content.PM;

namespace BgsControle.App.Plataforma;

/// <summary>
/// As permissões de Bluetooth que o app pede ao Android: "Dispositivos por perto"
/// (BLUETOOTH_SCAN e BLUETOOTH_CONNECT) no Android 12+, e a localização no 8 a 11,
/// que é como o Android antigo libera o scan BLE.
/// </summary>
public sealed class PermissaoBluetooth : Permissions.BasePlatformPermission
{
    private static string[] Necessarias =>
        OperatingSystem.IsAndroidVersionAtLeast(31)
            ? [Android.Manifest.Permission.BluetoothScan, Android.Manifest.Permission.BluetoothConnect]
            : [Android.Manifest.Permission.AccessFineLocation];

    public override (string androidPermission, bool isRuntime)[] RequiredPermissions =>
        Necessarias.Select(p => (p, true)).ToArray();

    public static bool Concedida(Context contexto) =>
        Necessarias.All(p => contexto.CheckSelfPermission(p) == Permission.Granted);
}

/// <summary>Pede ao Android para ligar o Bluetooth (ACTION_REQUEST_ENABLE).</summary>
public static class PedidoLigarBluetooth
{
    private const int Codigo = 4207;
    private static TaskCompletionSource<bool>? _pedido;

    public static bool Ligado(Context contexto)
    {
        var gerente = (BluetoothManager?)contexto.GetSystemService(Context.BluetoothService);
        return gerente?.Adapter?.IsEnabled == true;
    }

    /// <summary>Abre o pedido do Android. Devolve true se o usuário ligou o Bluetooth.</summary>
    public static Task<bool> PedirAsync()
    {
        var atividade = Platform.CurrentActivity;
        if (atividade is null)
            return Task.FromResult(false);
        _pedido?.TrySetResult(false);
        var pedido = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pedido = pedido;
#pragma warning disable CA1422 // StartActivityForResult segue funcionando em todas as versões
        atividade.StartActivityForResult(new Intent(BluetoothAdapter.ActionRequestEnable), Codigo);
#pragma warning restore CA1422
        return pedido.Task;
    }

    /// <summary>Chamado pela MainActivity.</summary>
    public static void Resultado(int requestCode, Android.App.Result resultCode)
    {
        if (requestCode != Codigo)
            return;
        var pedido = _pedido;
        _pedido = null;
        pedido?.TrySetResult(resultCode == Android.App.Result.Ok);
    }
}
