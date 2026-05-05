using SaaSonic.Domain.Enums;

namespace SaaSonic.Domain.Entities;

public sealed class PendingEmail
{
    public Guid Id { get; set; }
    public string ToEmail { get; set; } = string.Empty;
    public string TemplateSlug { get; set; } = string.Empty;

    // JSON object: {"Name": "...", "CallbackUrl": "..."}
    public string Placeholders { get; set; } = "{}";

    public PendingEmailStatus Status { get; set; } = PendingEmailStatus.Pending;
    public int RetryCount { get; set; }
    public string? ErrorMessage { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? SentAt { get; set; }

    // Next time the worker should attempt delivery; set to UtcNow for immediate pickup
    public DateTimeOffset NextRetryAt { get; set; }
}
