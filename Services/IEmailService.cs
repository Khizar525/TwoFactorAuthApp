namespace TwoFactorAuthApp.Services;

public interface IEmailService
{
    Task SendTwoFactorCodeAsync(string email, string code);
}
