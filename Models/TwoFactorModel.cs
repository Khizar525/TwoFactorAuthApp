using System.ComponentModel.DataAnnotations;

namespace TwoFactorAuthApp.Models;

public class TwoFactorModel
{
    [Required(ErrorMessage = "Verification code is required")]
    [Display(Name = "Authentication Code")]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "Code must be 6 digits")]
    public string Code { get; set; } = "";

    public string? QrCodeImage { get; set; }

    public string? ManualEntryKey { get; set; }
}
