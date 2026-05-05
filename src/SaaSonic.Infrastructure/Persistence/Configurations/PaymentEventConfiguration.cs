using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SaaSonic.Domain.Entities;

namespace SaaSonic.Infrastructure.Persistence.Configurations;

internal sealed class PaymentEventConfiguration : IEntityTypeConfiguration<PaymentEvent>
{
    public void Configure(EntityTypeBuilder<PaymentEvent> builder)
    {
        builder.HasKey(pe => pe.Id);

        builder.Property(pe => pe.ProviderEventId)
            .HasMaxLength(256)
            .IsRequired();

        // Unique index is what makes webhook processing idempotent —
        // inserting a duplicate ProviderEventId will fail fast at the DB level
        builder.HasIndex(pe => pe.ProviderEventId).IsUnique();

        builder.Property(pe => pe.EventType)
            .HasMaxLength(256)
            .IsRequired();

        // jsonb allows querying into the payload (e.g. payload->>'customer_id')
        // without deserializing the entire object in application code
        builder.Property(pe => pe.Payload)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(pe => pe.ErrorMessage)
            .HasMaxLength(2000);
    }
}
