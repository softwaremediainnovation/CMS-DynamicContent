using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CmsBillboard.DynamicContent.Services;

public class SecurityTokenService : ISecurityTokenService
{
    private readonly byte[] _aesKey;
    private readonly byte[] _hmacKey;
    private readonly int _defaultExpiryMinutes;
    private readonly ILogger<SecurityTokenService> _logger;

    public SecurityTokenService(IConfiguration configuration, ILogger<SecurityTokenService> logger)
    {
        _logger = logger;
        var secret = configuration["Security:TokenSecretKey"] 
                     ?? "WWM_DynamicDOOH_SuperSecretKey_2026_SecureSecurityToken#9988";
        
        _defaultExpiryMinutes = configuration.GetValue("Security:TokenExpiryMinutes", 60);

        // Derive 32 bytes AES Key and 32 bytes HMAC Key using SHA256 hashes
        using var sha256 = SHA256.Create();
        _aesKey = sha256.ComputeHash(Encoding.UTF8.GetBytes(secret + "_AES_KEY"));
        _hmacKey = sha256.ComputeHash(Encoding.UTF8.GetBytes(secret + "_HMAC_KEY"));
    }

    public string GenerateVoteToken(string contentId, string siteId, string mode, int? expiryMinutes = null)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var expiry = now + (expiryMinutes ?? _defaultExpiryMinutes) * 60;

        var payload = new VoteTokenPayload(
            contentId: contentId,
            siteId: siteId,
            mode: mode,
            createdAtUnix: now,
            expiresAtUnix: expiry
        );

        var json = JsonSerializer.Serialize(payload);
        var plainBytes = Encoding.UTF8.GetBytes(json);

        // Encrypt with AES-CBC + Random IV
        using var aes = Aes.Create();
        aes.Key = _aesKey;
        aes.GenerateIV();
        var iv = aes.IV;

        byte[] cipherBytes;
        using (var encryptor = aes.CreateEncryptor())
        {
            cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
        }

        // Combine IV + Ciphertext
        var payloadToSign = new byte[iv.Length + cipherBytes.Length];
        Buffer.BlockCopy(iv, 0, payloadToSign, 0, iv.Length);
        Buffer.BlockCopy(cipherBytes, 0, payloadToSign, iv.Length, cipherBytes.Length);

        // Calculate HMAC-SHA256 signature (Encrypt-then-MAC)
        using var hmac = new HMACSHA256(_hmacKey);
        var signature = hmac.ComputeHash(payloadToSign);

        // Final package: [IV (16 bytes)] + [Ciphertext] + [HMAC (32 bytes)]
        var finalPackage = new byte[payloadToSign.Length + signature.Length];
        Buffer.BlockCopy(payloadToSign, 0, finalPackage, 0, payloadToSign.Length);
        Buffer.BlockCopy(signature, 0, finalPackage, payloadToSign.Length, signature.Length);

        // Return Base64Url string
        return Convert.ToBase64String(finalPackage)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");
    }

    public (bool IsValid, VoteTokenPayload? Payload, string? ErrorMessage) ValidateVoteToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return (false, null, "Token tidak boleh kosong.");
        }

        try
        {
            // Base64Url decode
            string incoming = token.Replace("-", "+").Replace("_", "/");
            switch (incoming.Length % 4)
            {
                case 2: incoming += "=="; break;
                case 3: incoming += "="; break;
            }

            var package = Convert.FromBase64String(incoming);

            // Minimum length: 16 (IV) + 1 (min cipher) + 32 (HMAC) = 49 bytes
            if (package.Length < 49)
            {
                return (false, null, "Format token tidak valid.");
            }

            int hmacLength = 32;
            int ivLength = 16;
            int payloadLength = package.Length - hmacLength;
            int cipherLength = payloadLength - ivLength;

            var payloadToVerify = new byte[payloadLength];
            var receivedHmac = new byte[hmacLength];
            Buffer.BlockCopy(package, 0, payloadToVerify, 0, payloadLength);
            Buffer.BlockCopy(package, payloadLength, receivedHmac, 0, hmacLength);

            // Verify HMAC signature
            using var hmac = new HMACSHA256(_hmacKey);
            var computedHmac = hmac.ComputeHash(payloadToVerify);

            if (!CryptographicOperations.FixedTimeEquals(computedHmac, receivedHmac))
            {
                _logger.LogWarning("Invalid HMAC signature detected on vote token: {Token}", token);
                return (false, null, "Token tidak valid atau telah dimodifikasi secara ilegal!");
            }

            // Extract IV and Ciphertext
            var iv = new byte[ivLength];
            var cipherBytes = new byte[cipherLength];
            Buffer.BlockCopy(payloadToVerify, 0, iv, 0, ivLength);
            Buffer.BlockCopy(payloadToVerify, ivLength, cipherBytes, 0, cipherLength);

            // Decrypt
            using var aes = Aes.Create();
            aes.Key = _aesKey;
            aes.IV = iv;

            byte[] decryptedBytes;
            using (var decryptor = aes.CreateDecryptor())
            {
                decryptedBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
            }

            var json = Encoding.UTF8.GetString(decryptedBytes);
            var payload = JsonSerializer.Deserialize<VoteTokenPayload>(json);

            if (payload == null)
            {
                return (false, null, "Gagal memproses data payload token.");
            }

            // Check Expiration
            var currentUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (currentUnix > payload.ExpiresAtUnix)
            {
                _logger.LogInformation("Expired vote token presented: created {Created}, expired {Expired}", 
                    payload.CreatedAtUnix, payload.ExpiresAtUnix);
                return (false, null, "Token QR Code telah kedaluwarsa. Silakan pindai ulang dari layar videotron.");
            }

            return (true, payload, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while validating vote token: {Token}", token);
            return (false, null, "Token rusak atau tidak dapat diproses.");
        }
    }
}
