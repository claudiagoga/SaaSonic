using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SaaSonic.Domain.Entities;

namespace SaaSonic.Infrastructure.Persistence.Configurations;

internal sealed class WorkspaceMemberConfiguration : IEntityTypeConfiguration<WorkspaceMember>
{
    public void Configure(EntityTypeBuilder<WorkspaceMember> builder)
    {
        builder.HasKey(m => m.Id);

        // A user can only appear once per workspace
        builder.HasIndex(m => new { m.WorkspaceId, m.UserId }).IsUnique();

        builder.HasOne(m => m.Workspace)
            .WithMany(w => w.Members)
            .HasForeignKey(m => m.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.User)
            .WithMany(u => u.WorkspaceMemberships)
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.Role)
            .WithMany(r => r.WorkspaceMembers)
            .HasForeignKey(m => m.RoleId)
            .OnDelete(DeleteBehavior.Restrict);

        // Second FK to User: must not cascade (two cascade paths from User → WorkspaceMember)
        builder.HasOne(m => m.InvitedByUser)
            .WithMany()
            .HasForeignKey(m => m.InvitedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
