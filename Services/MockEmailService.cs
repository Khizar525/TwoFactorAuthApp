namespace TwoFactorAuthApp.Services;

/// <summary>
/// Mock email service for demonstration purposes.
/// In production, replace with SMTP, SendGrid, MailKit, etc.
/// </summary>
public class MockEmailService : IEmailService
{
    private readonly ILogger<MockEmailService> _logger;

    public MockEmailService(ILogger<MockEmailService> logger)
    {
        _logger = logger;
    }

    public Task SendTwoFactorCodeAsync(string email, string code)
    {
        // Log to console for demo
        _logger.LogInformation("========== EMAIL 2FA CODE ==========");
        _logger.LogInformation("To: {Email}", email);
        _logger.LogInformation("Your verification code is: {Code}", code);
        _logger.LogInformation("This code expires in 5 minutes.");
        _logger.LogInformation("====================================");

        Console.WriteLine("========================================");
        Console.WriteLine($"EMAIL 2FA - To: {email}");
        Console.WriteLine($"Your verification code is: {code}");
        Console.WriteLine("========================================");

        return Task.CompletedTask;
    }
}
