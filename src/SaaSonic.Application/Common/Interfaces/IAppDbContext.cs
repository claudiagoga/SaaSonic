using Microsoft.EntityFrameworkCore;
using SaaSonic.Domain.Entities;

namespace SaaSonic.Application.Common.Interfaces;

public interface IAppDbContext
{
    DbSet<User> Users { get; }
    DbSet<AuthIdentity> AuthIdentities { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<Workspace> Workspaces { get; }
    DbSet<WorkspaceMember> WorkspaceMembers { get; }
    DbSet<WorkspaceInvitation> WorkspaceInvitations { get; }
    DbSet<Role> Roles { get; }
    DbSet<Permission> Permissions { get; }
    DbSet<RolePermission> RolePermissions { get; }
    DbSet<Plan> Plans { get; }
    DbSet<Subscription> Subscriptions { get; }
    DbSet<Invoice> Invoices { get; }
    DbSet<PaymentMethod> PaymentMethods { get; }
    DbSet<PaymentEvent> PaymentEvents { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<EmailTemplate> EmailTemplates { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
