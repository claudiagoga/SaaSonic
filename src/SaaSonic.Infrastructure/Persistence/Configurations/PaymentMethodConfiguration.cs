using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SaaSonic.Domain.Entities;

namespace SaaSonic.Infrastructure.Persistence.Configurations;

internal sealed class PaymentMethodConfiguration : IEntityTypeConfiguration<PaymentMethod>
{
    public void Configure(EntityTypeBuilder<PaymentMethod> builder)
    {
        builder.HasKey(pm => pm.Id);

        builder.Property(pm => pm.ProviderPaymentMethodId)
            .HasMaxLength(256)
            .IsRequired();
        builder.HasIndex(pm => pm.ProviderPaymentMethodId).IsUnique();

        builder.Property(pm => pm.Last4)
            .HasMaxLength(4)
            .IsRequired();

        builder.HasOne(pm => pm.Workspace)
            .WithMany()
            .HasForeignKey(pm => pm.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
