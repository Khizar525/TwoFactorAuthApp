using System.ComponentModel.DataAnnotations;

namespace TwoFactorAuthApp.Models;

public class UserModel
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string UserName { get; set; } = "";

    [Required]
    public string Email { get; set; } = "";

    [Required]
    public string HashedPassword { get; set; } = "";

    // Google Authenticator 2FA
    public string TwoFactorSecret { get; set; } = "";

    // Email-Based 2FA fields
    public string? TwoFactorCode { get; set; }
    public DateTime? TwoFactorCodeExpiry { get; set; }
}
