using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SaaSonic.Domain.Entities;

namespace SaaSonic.Infrastructure.Persistence.Configurations;

internal sealed class AuthIdentityConfiguration : IEntityTypeConfiguration<AuthIdentity>
{
    public void Configure(EntityTypeBuilder<AuthIdentity> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.ProviderUserId)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(a => a.EmailAtProvider)
            .HasMaxLength(256);

        // OAuth tokens can be long — 2000 is a safe ceiling
        builder.Property(a => a.ProviderAccessToken)
            .HasMaxLength(2000);

        builder.Property(a => a.ProviderRefreshToken)
            .HasMaxLength(2000);

        // A user cannot have two identities for the same provider
        builder.HasIndex(a => new { a.Provider, a.ProviderUserId }).IsUnique();

        builder.HasOne(a => a.User)
            .WithMany(u => u.AuthIdentities)
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
