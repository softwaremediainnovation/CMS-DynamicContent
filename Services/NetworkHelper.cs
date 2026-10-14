namespace CmsBillboard.DynamicContent.Services;

public static class NetworkHelper
{
    /// <summary>
    /// Gets the machine's active local LAN IPv4 address (e.g. 192.168.x.x)
    /// </summary>
    public static string GetLocalLanIp()
    {
        try
        {
            var interfaces = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up
                            && n.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Loopback)
                .OrderByDescending(n => n.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Wireless80211 ? 2 : 1) // Prioritize Wi-Fi
                .ToList();

            foreach (var iface in interfaces)
            {
                var uniAddrs = iface.GetIPProperties().UnicastAddresses;
                var ipv4 = uniAddrs.FirstOrDefault(a => 
                    a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
                    && !System.Net.IPAddress.IsLoopback(a.Address)
                    && !a.Address.ToString().StartsWith("169.254."));

                if (ipv4 != null)
                {
                    return ipv4.Address.ToString();
                }
            }
        }
        catch { }

        return "127.0.0.1";
    }

    /// <summary>
    /// Builds an accessible base URL. If accessed from localhost / 127.0.0.1, automatically replaces with local LAN IP
    /// so devices on the same Wi-Fi network can scan and connect.
    /// </summary>
    public static string ResolveAccessibleBaseUrl(HttpRequest request, string? overrideIp = null)
    {
        var scheme = request.Scheme;
        var host = request.Host.Host;
        var port = request.Host.Port;

        if (!string.IsNullOrWhiteSpace(overrideIp))
        {
            host = overrideIp.Trim();
        }
        else if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) || host == "127.0.0.1" || host == "::1")
        {
            host = GetLocalLanIp();
        }

        var hostPort = port.HasValue ? $"{host}:{port.Value}" : host;
        return $"{scheme}://{hostPort}";
    }
}
