using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using CmsBillboard.DynamicContent.Data;
using CmsBillboard.DynamicContent.Models;

namespace CmsBillboard.DynamicContent.Services;

public interface IAuthService
{
    string HashPassword(string password);
    bool VerifyPassword(string password, string storedHash);
    Task<AdminUser?> ValidateLoginAsync(string username, string password);
    Task RecordLoginAsync(int userId);
}

public class AuthService : IAuthService
{
    private readonly AppDbContext _context;
    private readonly ILogger<AuthService> _logger;
    private const int SaltSize = 16; // 128 bits
    private const int KeySize = 32;  // 256 bits
    private const int Iterations = 100000;

    public AuthService(AppDbContext context, ILogger<AuthService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public string HashPassword(string password)
    {
        using var algorithm = new Rfc2898DeriveBytes(
            password,
            SaltSize,
            Iterations,
            HashAlgorithmName.SHA256);

        var key = Convert.ToBase64String(algorithm.GetBytes(KeySize));
        var salt = Convert.ToBase64String(algorithm.Salt);

        return $"{Iterations}.{salt}.{key}";
    }

    public bool VerifyPassword(string password, string storedHash)
    {
        if (string.IsNullOrWhiteSpace(storedHash))
            return false;

        var parts = storedHash.Split('.', 3);
        if (parts.Length != 3)
            return false;

        var iterations = int.Parse(parts[0]);
        var salt = Convert.FromBase64String(parts[1]);
        var key = Convert.FromBase64String(parts[2]);

        using var algorithm = new Rfc2898DeriveBytes(
            password,
            salt,
            iterations,
            HashAlgorithmName.SHA256);

        var keyToCheck = algorithm.GetBytes(KeySize);
        return CryptographicOperations.FixedTimeEquals(keyToCheck, key);
    }

    public async Task<AdminUser?> ValidateLoginAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return null;

        var user = await _context.AdminUsers
            .FirstOrDefaultAsync(u => u.Username.ToLower() == username.Trim().ToLower() && u.IsActive);

        if (user == null)
        {
            _logger.LogWarning("Admin login failed: user '{Username}' not found or inactive", username);
            return null;
        }

        if (!VerifyPassword(password, user.PasswordHash))
        {
            _logger.LogWarning("Admin login failed: invalid password for user '{Username}'", username);
            return null;
        }

        _logger.LogInformation("Admin login successful for user '{Username}'", username);
        return user;
    }

    public async Task RecordLoginAsync(int userId)
    {
        var user = await _context.AdminUsers.FindAsync(userId);
        if (user != null)
        {
            user.LastLoginAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }
}
