using Microsoft.EntityFrameworkCore;
using SaaSonic.Application.Common.Interfaces;
using SaaSonic.Domain.Entities;

namespace SaaSonic.Application.Tests.Common;

public class TestDbContext : DbContext, IAppDbContext
{
    public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }

    public DbSet<User> Users { get; set; } = null!;
    public DbSet<AuthIdentity> AuthIdentities { get; set; } = null!;
    public DbSet<RefreshToken> RefreshTokens { get; set; } = null!;
    public DbSet<Workspace> Workspaces { get; set; } = null!;
    public DbSet<WorkspaceMember> WorkspaceMembers { get; set; } = null!;
    public DbSet<WorkspaceInvitation> WorkspaceInvitations { get; set; } = null!;
    public DbSet<Role> Roles { get; set; } = null!;
    public DbSet<Permission> Permissions { get; set; } = null!;
    public DbSet<RolePermission> RolePermissions { get; set; } = null!;
    public DbSet<Plan> Plans { get; set; } = null!;
    public DbSet<Subscription> Subscriptions { get; set; } = null!;
    public DbSet<Invoice> Invoices { get; set; } = null!;
    public DbSet<PaymentMethod> PaymentMethods { get; set; } = null!;
    public DbSet<PaymentEvent> PaymentEvents { get; set; } = null!;
    public DbSet<AuditLog> AuditLogs { get; set; } = null!;
    public DbSet<EmailTemplate> EmailTemplates { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // WorkspaceMember has two FKs to User (UserId and InvitedByUserId).
        // EF Core can't determine which one backs User.WorkspaceMemberships by
        // convention alone, so we configure both sides explicitly.
        modelBuilder.Entity<WorkspaceMember>()
            .HasOne(wm => wm.User)
            .WithMany(u => u.WorkspaceMemberships)
            .HasForeignKey(wm => wm.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<WorkspaceMember>()
            .HasOne(wm => wm.InvitedByUser)
            .WithMany()
            .HasForeignKey(wm => wm.InvitedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // WorkspaceInvitation.InvitedByUserId backs User.SentWorkspaceInvitations —
        // the property names don't match EF Core's convention so we configure it.
        modelBuilder.Entity<WorkspaceInvitation>()
            .HasOne(wi => wi.InvitedByUser)
            .WithMany(u => u.SentWorkspaceInvitations)
            .HasForeignKey(wi => wi.InvitedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Workspace.OwnerUserId backs User.OwnedWorkspaces.
        modelBuilder.Entity<Workspace>()
            .HasOne(w => w.OwnerUser)
            .WithMany(u => u.OwnedWorkspaces)
            .HasForeignKey(w => w.OwnerUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // RolePermission is a join table — composite PK must be declared explicitly
        // because there is no single 'Id' column for EF Core to discover by convention.
        modelBuilder.Entity<RolePermission>()
            .HasKey(rp => new { rp.RoleId, rp.PermissionId });
    }
}
