using SaaSonic.Domain.Enums;

namespace SaaSonic.Domain.Entities;

public sealed class PaymentMethod
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }

    public PaymentProvider Provider { get; set; } = PaymentProvider.Stripe;
    public string ProviderPaymentMethodId { get; set; } = string.Empty; // Stripe: pm_xxx

    public CardBrand CardBrand { get; set; }
    public string Last4 { get; set; } = string.Empty;
    public int ExpiryMonth { get; set; }
    public int ExpiryYear { get; set; }

    public bool IsDefault { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public Workspace Workspace { get; set; } = null!;
}
