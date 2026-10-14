using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CmsBillboard.DynamicContent.Models;

public class AdminUser
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string PasswordHash { get; set; } = string.Empty;

    [MaxLength(100)]
    public string FullName { get; set; } = "Administrator";

    [MaxLength(50)]
    public string Role { get; set; } = "Admin"; // "Admin", "SuperAdmin"

    public bool IsActive { get; set; } = true;

    public DateTime? LastLoginAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class AdminLoginRequest
{
    [Required(ErrorMessage = "Username wajib diisi.")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password wajib diisi.")]
    public string Password { get; set; } = string.Empty;

    public bool RememberMe { get; set; } = false;
}
