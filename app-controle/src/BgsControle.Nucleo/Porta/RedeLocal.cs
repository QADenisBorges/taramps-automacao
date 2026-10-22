using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace BgsControle.Nucleo.Porta;

/// <summary>A trava 3 (<c>porta-de-comandos.md</c>): só a rede local da casa.</summary>
public interface IRedeLocal
{
    /// <summary>O endereço de quem pede é da rede local (privado, na mesma sub-rede do celular, e não o loopback).</summary>
    bool Aceita(IPAddress remoto);

    /// <summary>O IP do celular na rede local, para a tela mostrar (<c>192.168.0.5</c>), ou null.</summary>
    string? Endereco { get; }
}

/// <summary>
/// A rede local de verdade, pelas interfaces do celular (<see cref="NetworkInterface"/>):
/// <list type="bullet">
/// <item>privado: IPv4 10/8, 172.16/12, 192.168/16 ou 169.254/16; IPv6 fe80::/10 ou fc00::/7; o
/// IPv4 mapeado em IPv6 conta como IPv4;</item>
/// <item>na mesma sub-rede de uma interface ligada do celular;</item>
/// <item>o loopback (127/8, ::1) é recusado.</item>
/// </list>
/// </summary>
public sealed class RedeLocal : IRedeLocal
{
    // Os dados móveis e as VPNs do Android: a sub-rede deles não é a casa.
    private static readonly string[] PrefixosFora = ["rmnet", "ccmni", "tun", "ppp", "clat", "v4-rmnet"];

    private readonly Func<IEnumerable<(IPAddress Endereco, int Prefixo)>> _interfaces;

    public RedeLocal() : this(Interfaces)
    {
    }

    /// <summary>Para os testes: as interfaces (endereço e tamanho da sub-rede) vêm daqui.</summary>
    internal RedeLocal(Func<IEnumerable<(IPAddress Endereco, int Prefixo)>> interfaces) => _interfaces = interfaces;

    public bool Aceita(IPAddress remoto)
    {
        var ip = Normalizar(remoto);
        if (IPAddress.IsLoopback(ip) || !Privado(ip))
            return false;
        try
        {
            return _interfaces().Any(e => MesmaSubRede(ip, e.Endereco, e.Prefixo));
        }
        catch (Exception e) when (e is NetworkInformationException or PlatformNotSupportedException or NotImplementedException)
        {
            return false; // sem saber a sub-rede, o lado seguro
        }
    }

    public string? Endereco
    {
        get
        {
            try
            {
                return _interfaces()
                    .Where(e => e.Endereco.AddressFamily == AddressFamily.InterNetwork && Privado(e.Endereco))
                    .Select(e => e.Endereco.ToString())
                    .FirstOrDefault();
            }
            catch (Exception e) when (e is NetworkInformationException or PlatformNotSupportedException or NotImplementedException)
            {
                return null;
            }
        }
    }

    /// <summary>O IPv4 mapeado em IPv6 (<c>::ffff:192.168.0.10</c>) vira IPv4.</summary>
    public static IPAddress Normalizar(IPAddress ip) => ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4() : ip;

    /// <summary>As faixas privadas (sem olhar a sub-rede). O loopback não é privado.</summary>
    public static bool Privado(IPAddress endereco)
    {
        var ip = Normalizar(endereco);
        var b = ip.GetAddressBytes();
        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            return b[0] == 10
                   || (b[0] == 172 && (b[1] & 0xF0) == 16)
                   || (b[0] == 192 && b[1] == 168)
                   || (b[0] == 169 && b[1] == 254);
        }
        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return (b[0] == 0xFE && (b[1] & 0xC0) == 0x80) // fe80::/10
                   || (b[0] & 0xFE) == 0xFC; // fc00::/7
        }
        return false;
    }

    /// <summary>Os dois endereços têm os mesmos <paramref name="prefixo"/> primeiros bits.</summary>
    public static bool MesmaSubRede(IPAddress a, IPAddress b, int prefixo)
    {
        a = Normalizar(a);
        b = Normalizar(b);
        if (a.AddressFamily != b.AddressFamily || prefixo <= 0)
            return false;
        var x = a.GetAddressBytes();
        var y = b.GetAddressBytes();
        prefixo = Math.Min(prefixo, x.Length * 8);
        var inteiros = prefixo / 8;
        for (var i = 0; i < inteiros; i++)
        {
            if (x[i] != y[i])
                return false;
        }
        var resto = prefixo % 8;
        if (resto == 0)
            return true;
        var mascara = (byte)(0xFF << (8 - resto));
        return (x[inteiros] & mascara) == (y[inteiros] & mascara);
    }

    /// <summary>Os endereços das interfaces ligadas (sem o loopback), com o tamanho da sub-rede.</summary>
    private static IEnumerable<(IPAddress Endereco, int Prefixo)> Interfaces()
    {
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up || !DaCasa(nic))
                continue;
            foreach (var u in nic.GetIPProperties().UnicastAddresses)
            {
                if (IPAddress.IsLoopback(u.Address) || Prefixo(u) is not { } prefixo)
                    continue;
                yield return (u.Address, prefixo);
            }
        }
    }

    /// <summary>
    /// Fora o loopback, os túneis e os dados móveis (pelo tipo e pelos nomes do Android). Não filtra
    /// só o Wi-Fi: o Android pode dar o wlan0 como Ethernet ou Unknown.
    /// </summary>
    private static bool DaCasa(NetworkInterface nic)
    {
        if (nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel
            or NetworkInterfaceType.Ppp or NetworkInterfaceType.Wwanpp or NetworkInterfaceType.Wwanpp2)
            return false;
        var nome = nic.Name ?? "";
        return !PrefixosFora.Any(p => nome.StartsWith(p, StringComparison.OrdinalIgnoreCase));
    }

    private static int? Prefixo(UnicastIPAddressInformation u)
    {
        try
        {
            if (u.PrefixLength > 0)
                return u.PrefixLength;
        }
        catch (Exception e) when (e is PlatformNotSupportedException or NotImplementedException)
        {
            // Cai para a máscara.
        }
        try
        {
            if (u.Address.AddressFamily == AddressFamily.InterNetwork && u.IPv4Mask is { } mascara)
                return mascara.GetAddressBytes().Sum(b => System.Numerics.BitOperations.PopCount(b));
        }
        catch (Exception e) when (e is PlatformNotSupportedException or NotImplementedException)
        {
            // Sem sub-rede: a interface não conta.
        }
        return null;
    }
}
