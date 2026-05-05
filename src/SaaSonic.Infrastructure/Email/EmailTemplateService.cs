using Microsoft.EntityFrameworkCore;
using SaaSonic.Application.Common.Interfaces;

namespace SaaSonic.Infrastructure.Email;

public sealed class EmailTemplateService : IEmailTemplateService
{
    private readonly IAppDbContext _db;
    private readonly IEmailService _emailService;

    public EmailTemplateService(IAppDbContext db, IEmailService emailService)
    {
        _db = db;
        _emailService = emailService;
    }

    public async Task SendFromTemplateAsync(
        string toEmail,
        string templateSlug,
        Dictionary<string, string> placeholders,
        CancellationToken cancellationToken = default)
    {
        var template = await _db.EmailTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Slug == templateSlug, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Email template '{templateSlug}' was not found. " +
                "Seed the EmailTemplates table before sending this email.");

        var subject = ReplacePlaceholders(template.Subject, placeholders);
        var body = ReplacePlaceholders(template.Body, placeholders);

        await _emailService.SendAsync(toEmail, subject, body, cancellationToken);
    }

    /// <summary>
    /// Replaces every occurrence of <c>{Key}</c> in <paramref name="text"/>
    /// with the corresponding value from <paramref name="placeholders"/>.
    /// Keys are matched case-insensitively.
    /// </summary>
    private static string ReplacePlaceholders(string text, Dictionary<string, string> placeholders)
    {
        foreach (var (key, value) in placeholders)
            text = text.Replace($"{{{key}}}", value, StringComparison.OrdinalIgnoreCase);

        return text;
    }
}
