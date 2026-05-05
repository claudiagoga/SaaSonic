namespace SaaSonic.Domain.Enums;

public enum WebhookStatus : short
{
    Pending = 0,
    Processed = 1,
    Failed = 2,
    Ignored = 3
}
