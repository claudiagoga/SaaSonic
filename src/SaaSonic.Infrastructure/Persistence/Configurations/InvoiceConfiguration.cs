using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SaaSonic.Domain.Entities;

namespace SaaSonic.Infrastructure.Persistence.Configurations;

internal sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Currency)
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(i => i.PaymentProviderInvoiceId)
            .HasMaxLength(256)
            .IsRequired();
        builder.HasIndex(i => i.PaymentProviderInvoiceId).IsUnique();

        builder.Property(i => i.InvoiceUrl)
            .HasMaxLength(1000);

        builder.HasOne(i => i.Workspace)
            .WithMany()
            .HasForeignKey(i => i.WorkspaceId)
            .OnDelete(DeleteBehavior.Restrict);

        // Financial records must survive subscription cancellation
        builder.HasOne(i => i.Subscription)
            .WithMany()
            .HasForeignKey(i => i.SubscriptionId)
            .OnDelete(DeleteBehavior.Restrict);

        // Plan is a snapshot FK — records which plan was active at invoice time.
        // Must be Restrict: a plan cannot be deleted while invoices reference it.
        builder.HasOne(i => i.Plan)
            .WithMany()
            .HasForeignKey(i => i.PlanId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
