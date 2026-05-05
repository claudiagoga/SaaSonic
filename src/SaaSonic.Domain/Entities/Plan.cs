namespace SaaSonic.Domain.Entities;

public sealed class Plan
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int PriceMonthlyCents { get; set; }
    public int PriceYearlyCents { get; set; }
    public string Currency { get; set; } = "USD";
    public bool IsActive { get; set; } = true;

    // null = unlimited
    public int? MaxMembersPerWorkspace { get; set; }
    public int? MaxWorkspacesPerUser { get; set; }
    public int? StorageLimitMb { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<Subscription> WorkspaceSubscriptions { get; set; } = new List<Subscription>();
}
