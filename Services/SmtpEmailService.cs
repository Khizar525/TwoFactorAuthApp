using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace TwoFactorAuthApp.Services;

/// <summary>
/// Real SMTP email service using MailKit.
/// Configure SMTP settings in appsettings.json under "SmtpSettings".
/// </summary>
public class SmtpEmailService : IEmailService
{
    private readonly SmtpSettings _settings;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IOptions<SmtpSettings> settings, ILogger<SmtpEmailService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task SendTwoFactorCodeAsync(string email, string code)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromEmail));
        message.To.Add(new MailboxAddress("", email));
        message.Subject = "Your Two-Factor Authentication Code";

        message.Body = new TextPart("html")
        {
            Text = $"""
            <html>
            <body style="font-family: Arial, sans-serif; padding: 20px;">
                <h2 style="color: #333;">Two-Factor Authentication</h2>
                <p>Your verification code is:</p>
                <div style="font-size: 32px; font-weight: bold; letter-spacing: 8px;
                            background: #f5f5f5; padding: 16px 24px; text-align: center;
                            border-radius: 8px; margin: 20px 0;">
                    {code}
                </div>
                <p>This code expires in <strong>5 minutes</strong>.</p>
                <p>If you did not request this code, please ignore this email.</p>
                <hr style="margin-top: 30px; border: none; border-top: 1px solid #eee;" />
                <p style="color: #888; font-size: 12px;">TwoFactorAuthApp - Cloud Computing Lab 11</p>
            </body>
            </html>
            """
        };

        using var client = new SmtpClient();

        // Trust all certs for dev (disable in production)
        client.ServerCertificateValidationCallback = (s, c, h, e) =>
        {
            if (_settings.IgnoreCertificateErrors)
                return true;
            return e == System.Net.Security.SslPolicyErrors.None;
        };

        await client.ConnectAsync(_settings.Host, _settings.Port,
            _settings.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable);

        if (!string.IsNullOrEmpty(_settings.Username))
            await client.AuthenticateAsync(_settings.Username, _settings.Password);

        await client.SendAsync(message);
        await client.DisconnectAsync(true);

        _logger.LogInformation("2FA code email sent to {Email}", email);
    }
}

public class SmtpSettings
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public bool UseSsl { get; set; }
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string FromEmail { get; set; } = "";
    public string FromName { get; set; } = "TwoFactorAuthApp";
    public bool IgnoreCertificateErrors { get; set; } = false;
}
