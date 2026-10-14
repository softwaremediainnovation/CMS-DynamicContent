namespace CmsBillboard.DynamicContent.Services;

public static class NetworkHelper
{
    private static string? _cachedNgrokUrl;
    private static DateTime _lastNgrokCheck = DateTime.MinValue;

    /// <summary>
    /// Checks if a local ngrok agent is active on port 4040 and returns the public https tunnel URL.
    /// </summary>
    public static string? GetActiveNgrokPublicUrl()
    {
        if (DateTime.UtcNow - _lastNgrokCheck < TimeSpan.FromSeconds(3) && !string.IsNullOrEmpty(_cachedNgrokUrl))
        {
            return _cachedNgrokUrl;
        }

        try
        {
            using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromMilliseconds(400) };
            var json = client.GetStringAsync("http://127.0.0.1:4040/api/tunnels").GetAwaiter().GetResult();
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("tunnels", out var tunnels) && tunnels.GetArrayLength() > 0)
            {
                foreach (var t in tunnels.EnumerateArray())
                {
                    if (t.TryGetProperty("public_url", out var pubUrl))
                    {
                        var url = pubUrl.GetString();
                        if (!string.IsNullOrEmpty(url))
                        {
                            _cachedNgrokUrl = url.StartsWith("http://") ? url.Replace("http://", "https://") : url;
                            _lastNgrokCheck = DateTime.UtcNow;
                            return _cachedNgrokUrl;
                        }
                    }
                }
            }
        }
        catch
        {
            _cachedNgrokUrl = null;
        }

        _lastNgrokCheck = DateTime.UtcNow;
        return null;
    }

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
    /// Builds an accessible base URL.
    /// Priority:
    /// 1. Reverse proxy / ngrok headers (X-Forwarded-Host)
    /// 2. Active ngrok tunnel (auto-detected via 127.0.0.1:4040)
    /// 3. Manual override ?ip= or ?host=
    /// 4. Local LAN Wi-Fi IP
    /// </summary>
    public static string ResolveAccessibleBaseUrl(HttpRequest request, string? overrideIp = null)
    {
        // 1. If accessed through ngrok directly
        if (request.Headers.TryGetValue("X-Forwarded-Host", out var fwdHost) && !string.IsNullOrWhiteSpace(fwdHost))
        {
            var fwdProto = request.Headers.TryGetValue("X-Forwarded-Proto", out var proto) ? proto.ToString() : "https";
            return $"{fwdProto}://{fwdHost}";
        }

        if (!string.IsNullOrWhiteSpace(overrideIp))
        {
            var scheme = request.Scheme;
            return $"{scheme}://{overrideIp.Trim()}";
        }

        // 2. If opened on localhost / local IP, check if ngrok is active
        var ngrokUrl = GetActiveNgrokPublicUrl();
        if (!string.IsNullOrEmpty(ngrokUrl))
        {
            return ngrokUrl;
        }

        // 3. Fallback to Wi-Fi LAN IP
        var host = request.Host.Host;
        var port = request.Host.Port;
        var reqScheme = request.Scheme;

        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) || host == "127.0.0.1" || host == "::1")
        {
            host = GetLocalLanIp();
        }

        var hostPort = port.HasValue ? $"{host}:{port.Value}" : host;
        return $"{reqScheme}://{hostPort}";
    }
}
