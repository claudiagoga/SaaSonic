using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SaaSonic.Domain.Entities;

namespace SaaSonic.Infrastructure.Persistence.Configurations;

internal sealed class EmailTemplateConfiguration : IEntityTypeConfiguration<EmailTemplate>
{
    public void Configure(EntityTypeBuilder<EmailTemplate> builder)
    {
        builder.HasKey(t => t.ID);

        builder.Property(t => t.Slug)
            .HasMaxLength(100)
            .IsRequired();
        builder.HasIndex(t => t.Slug).IsUnique();

        builder.Property(t => t.Subject)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(t => t.Body)
            .IsRequired();
    }
}
