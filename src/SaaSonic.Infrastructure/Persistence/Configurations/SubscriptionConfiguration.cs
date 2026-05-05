using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SaaSonic.Domain.Entities;

namespace SaaSonic.Infrastructure.Persistence.Configurations;

internal sealed class SubscriptionConfiguration : IEntityTypeConfiguration<Subscription>
{
    public void Configure(EntityTypeBuilder<Subscription> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.PaymentProviderCustomerId)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(s => s.PaymentProviderSubscriptionId)
            .HasMaxLength(256)
            .IsRequired();
        builder.HasIndex(s => s.PaymentProviderSubscriptionId).IsUnique();

        builder.Property(s => s.BillingEmail)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(s => s.BillingName)
            .HasMaxLength(200)
            .IsRequired();

        // Deleting a plan with active subscriptions must be blocked
        builder.HasOne(s => s.Plan)
            .WithMany(p => p.WorkspaceSubscriptions)
            .HasForeignKey(s => s.PlanId)
            .OnDelete(DeleteBehavior.Restrict);

        // Workspace ↔ Subscription one-to-one is configured in WorkspaceConfiguration
    }
}
