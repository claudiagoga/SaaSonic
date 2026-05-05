using SaaSonic.Domain.Enums;

namespace SaaSonic.Domain.Entities;

public sealed class PaymentEvent
{
    public Guid Id { get; set; }
    public PaymentProvider Provider { get; set; } = PaymentProvider.Stripe;
    public string ProviderEventId { get; set; } = string.Empty;  // unique index on this
    public string EventType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;          // raw JSON from Stripe
    public WebhookStatus Status { get; set; } = WebhookStatus.Pending;
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
}
