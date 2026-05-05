using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SaaSonic.Domain.Entities;

namespace SaaSonic.Infrastructure.Persistence.Configurations;

internal sealed class WorkspaceConfiguration : IEntityTypeConfiguration<Workspace>
{
    public void Configure(EntityTypeBuilder<Workspace> builder)
    {
        builder.HasKey(w => w.Id);

        builder.HasQueryFilter(w => w.DeletedAt == null);

        builder.Property(w => w.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(w => w.Slug)
            .HasMaxLength(100)
            .IsRequired();
        builder.HasIndex(w => w.Slug).IsUnique();

        // Deleting a user should not cascade-delete their workspaces —
        // ownership transfer or explicit deletion should be required first
        builder.HasOne(w => w.OwnerUser)
            .WithMany(u => u.OwnedWorkspaces)
            .HasForeignKey(w => w.OwnerUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // One-to-one: FK lives on Subscription.WorkspaceId
        // EF also adds a unique index on Subscription.WorkspaceId automatically
        builder.HasOne(w => w.Subscription)
            .WithOne(s => s.Workspace)
            .HasForeignKey<Subscription>(s => s.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
