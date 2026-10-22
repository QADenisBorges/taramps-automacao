using BgsControle.Nucleo;
using BgsControle.Telas;

namespace BgsControle.App.Plataforma;

/// <summary>Pede a permissão e o Bluetooth ligado ao Android, e explica o que falta.</summary>
public sealed class PreparoBluetoothAndroid : IPreparoBluetooth
{
    public async Task<string?> PrepararAsync()
    {
        var status = await Permissions.CheckStatusAsync<PermissaoBluetooth>();
        if (status != PermissionStatus.Granted)
            status = await Permissions.RequestAsync<PermissaoBluetooth>();
        if (status != PermissionStatus.Granted)
            return Sessao.MsgSemPermissao;

        var contexto = Android.App.Application.Context;
        if (!PedidoLigarBluetooth.Ligado(contexto) && !await PedidoLigarBluetooth.PedirAsync())
            return Sessao.MsgBluetoothDesligado;
        return null;
    }
}
