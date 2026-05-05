using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SaaSonic.Domain.Entities;

namespace SaaSonic.Infrastructure.Persistence.Configurations;

internal sealed class WorkspaceInvitationConfiguration : IEntityTypeConfiguration<WorkspaceInvitation>
{
    public void Configure(EntityTypeBuilder<WorkspaceInvitation> builder)
    {
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Email)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(i => i.TokenHash)
            .HasMaxLength(512)
            .IsRequired();

        // Invitation acceptance is a token lookup
        builder.HasIndex(i => i.TokenHash);

        // Useful for checking if an email already has a pending invite
        builder.HasIndex(i => new { i.WorkspaceId, i.Email });

        builder.HasOne(i => i.Workspace)
            .WithMany(w => w.Invitations)
            .HasForeignKey(i => i.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);

        // Sender being deleted should not cascade-delete historical invitation records
        builder.HasOne(i => i.InvitedByUser)
            .WithMany(u => u.SentWorkspaceInvitations)
            .HasForeignKey(i => i.InvitedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.Role)
            .WithMany(r => r.WorkspaceInvitations)
            .HasForeignKey(i => i.RoleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
