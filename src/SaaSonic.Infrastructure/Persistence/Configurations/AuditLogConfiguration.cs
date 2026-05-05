using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SaaSonic.Domain.Entities;

namespace SaaSonic.Infrastructure.Persistence.Configurations;

internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Action)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(a => a.EntityType)
            .HasMaxLength(200)
            .IsRequired();

        // jsonb allows future queries like "find all audit logs where metadata->>'ip' = '1.2.3.4'"
        builder.Property(a => a.Metadata)
            .HasColumnType("jsonb");

        // Audit logs are almost always queried by workspace or user — indexes are critical here
        builder.HasIndex(a => a.WorkspaceId);
        builder.HasIndex(a => a.UserId);

        // SetNull: audit records must be preserved even if the user or workspace is deleted
        builder.HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(a => a.Workspace)
            .WithMany(w => w.AuditLogs)
            .HasForeignKey(a => a.WorkspaceId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
