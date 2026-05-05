using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MimeKit;
using SaaSonic.Application.Common.Interfaces;

namespace SaaSonic.Infrastructure.Email;

public sealed class SmtpEmailService : IEmailService
{
    private readonly string _host;
    private readonly int _port;
    private readonly string _username;
    private readonly string _password;
    private readonly string _fromEmail;
    private readonly string _fromName;
    private readonly IAppDbContext _db;

    public SmtpEmailService(IConfiguration configuration, IAppDbContext db)
    {
        _host = configuration["Smtp:Host"] ?? "localhost";
        _port = int.TryParse(configuration["Smtp:Port"], out var p) ? p : 587;
        _username = configuration["Smtp:Username"] ?? string.Empty;
        _password = configuration["Smtp:Password"] ?? string.Empty;
        _fromEmail = configuration["Smtp:FromEmail"] ?? "noreply@saasonic.com";
        _fromName = configuration["Smtp:FromName"] ?? "SaaSonic";
        _db = db;
    }

    public async Task SendAsync(
        string to,
        string subject,
        string body,
        CancellationToken cancellationToken = default)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_fromName, _fromEmail));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new TextPart("html") { Text = body };

        using var client = new SmtpClient();
        await client.ConnectAsync(_host, _port, SecureSocketOptions.StartTls, cancellationToken);

        if (!string.IsNullOrEmpty(_username))
            await client.AuthenticateAsync(_username, _password, cancellationToken);

        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);
    }


    public async Task<string> GetTemplateAsync(
        string slug,
        CancellationToken cancellationToken = default)
    {
        var template = await _db.EmailTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Slug == slug, cancellationToken);

        return template?.Body ?? string.Empty;
    }
}
