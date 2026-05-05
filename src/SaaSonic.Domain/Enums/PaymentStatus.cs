namespace SaaSonic.Domain.Enums;

public enum PaymentStatus : short
{
    RequiresAction = 1,
    Succeeded = 2,
    Failed = 3,
    Refunded = 4
}
