using SaaSonic.Application.Common.Interfaces;
using SaaSonic.Domain.Entities;
using SaaSonic.Infrastructure.Persistence;
using System.Text.Json;

namespace SaaSonic.Infrastructure.Email;

internal sealed class DbEmailQueue : IEmailQueue
{
    private readonly AppDbContext _db;

    public DbEmailQueue(AppDbContext db) => _db = db;

    public void Enqueue(string toEmail, string templateSlug, Dictionary<string, string> placeholders)
    {
        // Only stages in the change tracker — no SaveChangesAsync here.
        // The caller's SaveChangesAsync commits this row atomically with the business entity.
        _db.PendingEmails.Add(new PendingEmail
        {
            Id = Guid.NewGuid(),
            ToEmail = toEmail,
            TemplateSlug = templateSlug,
            Placeholders = JsonSerializer.Serialize(placeholders),
            CreatedAt = DateTimeOffset.UtcNow,
            NextRetryAt = DateTimeOffset.UtcNow,
        });
    }
}
