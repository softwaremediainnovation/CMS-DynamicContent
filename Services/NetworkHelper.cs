namespace CmsBillboard.DynamicContent.Services;

public static class NetworkHelper
{
    private static string? _cachedNgrokUrl;
    private static DateTime _lastNgrokCheck = DateTime.MinValue;

    private static string? _cachedCloudflareUrl;
    private static DateTime _lastCloudflareCheck = DateTime.MinValue;

    public static string? ConfiguredPublicUrl { get; set; }

    /// <summary>
    /// Checks if cloudflared is running and reads the active public tunnel URL from cloudflared.log.
    /// </summary>
    public static string? GetActiveCloudflarePublicUrl()
    {
        if (DateTime.UtcNow - _lastCloudflareCheck < TimeSpan.FromSeconds(3) && !string.IsNullOrEmpty(_cachedCloudflareUrl))
        {
            return _cachedCloudflareUrl;
        }

        try
        {
            // 1. Check if cloudflared process is running
            var cfProcesses = System.Diagnostics.Process.GetProcessesByName("cloudflared");
            if (cfProcesses.Length == 0)
            {
                _cachedCloudflareUrl = null;
                _lastCloudflareCheck = DateTime.UtcNow;
                return null;
            }

            // 2. Read recent log file for quick tunnel trycloudflare.com URL
            var candidatePaths = new[]
            {
                Path.Combine(Directory.GetCurrentDirectory(), "cloudflared.log"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cloudflared.log")
            };

            foreach (var logPath in candidatePaths)
            {
                if (File.Exists(logPath))
                {
                    using var stream = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var reader = new StreamReader(stream);
                    var content = reader.ReadToEnd();

                    var matches = System.Text.RegularExpressions.Regex.Matches(
                        content,
                        @"https://[a-zA-Z0-9-]+\.trycloudflare\.com",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                    if (matches.Count > 0)
                    {
                        var latestMatch = matches[^1].Value;
                        _cachedCloudflareUrl = latestMatch.TrimEnd('/');
                        _lastCloudflareCheck = DateTime.UtcNow;
                        return _cachedCloudflareUrl;
                    }
                }
            }
        }
        catch
        {
            // Ignore file read or process permission issues
        }

        _cachedCloudflareUrl = null;
        _lastCloudflareCheck = DateTime.UtcNow;
        return null;
    }

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
    /// Builds an accessible base URL for smartphones and audience devices to scan QR codes.
    /// Priority:
    /// 1. Query parameter override ?host= or ?ip= or parameter overrideIp
    /// 2. If the request comes from an external host (Cloudflare domain, ngrok domain, custom domain, or non-localhost LAN IP):
    ///    ALWAYS respect and use the incoming host!
    /// 3. If the request comes from localhost/loopback (billboard player PC):
    ///    a. Configured PublicUrl in appsettings
    ///    b. Active Cloudflare Tunnel (trycloudflare.com)
    ///    c. Active Ngrok Tunnel (ngrok-free.dev)
    ///    d. Local Wi-Fi LAN IP
    /// </summary>
    public static string ResolveAccessibleBaseUrl(HttpRequest request, string? overrideIp = null)
    {
        // 1. Check parameter or query string override
        if (string.IsNullOrWhiteSpace(overrideIp))
        {
            if (request.Query.TryGetValue("host", out var qHost) && !string.IsNullOrWhiteSpace(qHost))
            {
                overrideIp = qHost.ToString();
            }
            else if (request.Query.TryGetValue("ip", out var qIp) && !string.IsNullOrWhiteSpace(qIp))
            {
                overrideIp = qIp.ToString();
            }
        }

        if (!string.IsNullOrWhiteSpace(overrideIp))
        {
            var clean = overrideIp.Trim();
            if (clean.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || 
                clean.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return clean.TrimEnd('/');
            }
            var scheme = request.IsHttps ? "https" : "http";
            return $"{scheme}://{clean.TrimEnd('/')}";
        }

        // 2. Identify the incoming host from headers
        string host = request.Host.Host;
        int? port = request.Host.Port;

        if (request.Headers.TryGetValue("X-Forwarded-Host", out var fwdHost) && !string.IsNullOrWhiteSpace(fwdHost))
        {
            var fwdHostStr = fwdHost.ToString().Split(',')[0].Trim();
            if (fwdHostStr.Contains(':'))
            {
                var parts = fwdHostStr.Split(':');
                host = parts[0];
                if (int.TryParse(parts[1], out var parsedPort)) port = parsedPort;
            }
            else
            {
                host = fwdHostStr;
                port = null;
            }
        }

        bool isLoopback = string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) ||
                          host == "127.0.0.1" || 
                          host == "::1";

        // 3. If accessed through a REAL EXTERNAL HOST (Cloudflare tunnel, ngrok domain, custom domain, or direct LAN IP):
        // DO NOT override it with local tunnels! The client is ALREADY successfully visiting on this host!
        if (!isLoopback)
        {
            string scheme = "http";
            if (request.Headers.TryGetValue("X-Forwarded-Proto", out var fwdProto) && !string.IsNullOrWhiteSpace(fwdProto))
            {
                scheme = fwdProto.ToString().Split(',')[0].Trim();
            }
            else if (request.Headers.ContainsKey("CF-Visitor") || request.Headers.ContainsKey("CF-Ray"))
            {
                scheme = "https";
            }
            else if (request.IsHttps || 
                     host.EndsWith(".trycloudflare.com", StringComparison.OrdinalIgnoreCase) ||
                     host.EndsWith(".ngrok-free.dev", StringComparison.OrdinalIgnoreCase) ||
                     host.EndsWith(".ngrok.io", StringComparison.OrdinalIgnoreCase))
            {
                scheme = "https";
            }
            else
            {
                scheme = request.Scheme;
            }

            // Public domains don't need internal ports appended
            bool isPublicDomain = host.EndsWith(".trycloudflare.com", StringComparison.OrdinalIgnoreCase) ||
                                 host.EndsWith(".ngrok-free.dev", StringComparison.OrdinalIgnoreCase) ||
                                 host.EndsWith(".ngrok.io", StringComparison.OrdinalIgnoreCase) ||
                                 port == 80 || port == 443;

            if (isPublicDomain || !port.HasValue)
            {
                return $"{scheme}://{host}";
            }

            return $"{scheme}://{host}:{port.Value}";
        }

        // 4. If accessed locally on localhost/127.0.0.1 (e.g. browser on LED PC), find public tunnel or LAN IP
        if (!string.IsNullOrEmpty(ConfiguredPublicUrl))
        {
            return ConfiguredPublicUrl.TrimEnd('/');
        }

        // 4a. Prioritize active Cloudflare Tunnel
        var cfUrl = GetActiveCloudflarePublicUrl();
        if (!string.IsNullOrEmpty(cfUrl))
        {
            return cfUrl;
        }

        // 4b. Check active Ngrok Tunnel
        var ngrokUrl = GetActiveNgrokPublicUrl();
        if (!string.IsNullOrEmpty(ngrokUrl))
        {
            return ngrokUrl;
        }

        // 4c. Fallback to Local Wi-Fi LAN IP
        var lanIp = GetLocalLanIp();
        var reqPort = request.Host.Port ?? 5215;
        return $"{request.Scheme}://{lanIp}:{reqPort}";
    }
}
