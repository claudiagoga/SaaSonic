using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SaaSonic.Domain.Entities;

namespace SaaSonic.Infrastructure.Persistence.Configurations;

internal sealed class PendingEmailConfiguration : IEntityTypeConfiguration<PendingEmail>
{
    public void Configure(EntityTypeBuilder<PendingEmail> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.ToEmail)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(e => e.TemplateSlug)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.Placeholders)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(e => e.ErrorMessage)
            .HasMaxLength(2000);

        // Worker queries by status + next retry time on every poll
        builder.HasIndex(e => new { e.Status, e.NextRetryAt });
    }
}
