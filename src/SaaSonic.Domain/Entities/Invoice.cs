using SaaSonic.Domain.Enums;

namespace SaaSonic.Domain.Entities;

public sealed class Invoice
{
    public Guid Id { get; set; }

    public Guid WorkspaceId { get; set; }
    public Guid SubscriptionId { get; set; }

    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;

    public PaymentStatus? LastPaymentStatus { get; set; }

    // Snapshots — record the plan and interval and amount at the time this invoice was issued.
    // plan, amount, billing interval may change over time on the subscription.
    // but these fields must never be updated after the invoice is created.
    public Guid PlanId { get; set; }
    public BillingInterval BillingInterval { get; set; }   
    public int SubtotalAmountCents { get; set; }
    public int TaxAmountCents { get; set; }
    public int TotalAmountCents { get; set; }
    public string Currency { get; set; } = "USD";

    public string PaymentProviderInvoiceId { get; set; } = string.Empty;
    public string? InvoiceUrl { get; set; }

    public DateTimeOffset IssuedAt { get; set; }
    public DateTimeOffset? DueAt { get; set; }
    public DateTimeOffset? PaidAt { get; set; }

    public Workspace Workspace { get; set; } = null!;
    public Subscription Subscription { get; set; } = null!;
    public Plan Plan { get; set; } = null!;

}
