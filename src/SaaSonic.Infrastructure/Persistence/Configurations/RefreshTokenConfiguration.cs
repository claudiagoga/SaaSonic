using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SaaSonic.Domain.Entities;

namespace SaaSonic.Infrastructure.Persistence.Configurations;

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.TokenHash)
            .HasMaxLength(512)
            .IsRequired();

        // Tokens are looked up by hash on every authenticated request
        builder.HasIndex(r => r.TokenHash);

        builder.Property(r => r.DeviceId).HasMaxLength(256);
        builder.Property(r => r.DeviceName).HasMaxLength(256);
        builder.Property(r => r.IpAddress).HasMaxLength(45);   // IPv6 max length
        builder.Property(r => r.UserAgent).HasMaxLength(512);

        builder.HasOne(r => r.User)
            .WithMany(u => u.RefreshTokens)
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Self-referential: must be Restrict to avoid cycles
        builder.HasOne(r => r.ReplacedByToken)
            .WithMany()
            .HasForeignKey(r => r.ReplacedByTokenId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
