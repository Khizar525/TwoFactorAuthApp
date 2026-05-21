using System.ComponentModel.DataAnnotations;

namespace TwoFactorAuthApp.Models;

public class Email2FAModel
{
    [Required(ErrorMessage = "Verification code is required")]
    [Display(Name = "Email Verification Code")]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "Code must be 6 digits")]
    public string Code { get; set; } = "";
}
