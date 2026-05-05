namespace SaaSonic.Domain.Enums;

public enum SubscriptionStatus : short
{
    Trialing = 1,
    Active = 2,
    PastDue = 3,
    Canceled = 4
}
