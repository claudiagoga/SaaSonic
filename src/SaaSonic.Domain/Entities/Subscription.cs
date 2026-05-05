using SaaSonic.Domain.Enums;

namespace SaaSonic.Domain.Entities;

public sealed class Subscription
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid PlanId { get; set; }

    public BillingInterval BillingInterval { get; set; } = BillingInterval.Monthly;
    public PaymentProvider PaymentProvider { get; set; } = PaymentProvider.Stripe;
    public string PaymentProviderCustomerId { get; set; } = string.Empty; 
    public string PaymentProviderSubscriptionId { get; set; } = string.Empty;

    public string BillingEmail { get; set; } = string.Empty;
    public string BillingName { get; set; } = string.Empty;
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Trialing;

    public DateTimeOffset TrialEndsAt { get; set; }
    public DateTimeOffset CurrentPeriodStart { get; set; }
    public DateTimeOffset CurrentPeriodEnd { get; set; }
    public bool CancelAtPeriodEnd { get; set; }

    public Workspace Workspace { get; set; } = null!;
    public Plan Plan { get; set; } = null!;

}
