namespace SaaSonic.Domain.Entities;

public sealed class AuditLog
{
    public Guid Id { get; set; }

    public Guid? UserId { get; set; } // null for system actions
    public Guid? WorkspaceId { get; set; }

    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public Guid? EntityId { get; set; }

    public string? Metadata { get; set; } // JSON

    public DateTimeOffset CreatedAt { get; set; }

    public User? User { get; set; }
    public Workspace? Workspace { get; set; }
}
