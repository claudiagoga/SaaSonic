using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SaaSonic.Domain.Constants;
using SaaSonic.Domain.Entities;
using SaaSonic.Domain.Enums;

namespace SaaSonic.Infrastructure.Persistence.Configurations;

internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(r => r.Name).IsUnique();

        builder.HasData(
            new Role { Id = RoleIds.SystemAdmin, Name = RoleNames.SystemAdmin, Scope = RoleScope.System },
            new Role { Id = RoleIds.Owner,       Name = RoleNames.Owner,       Scope = RoleScope.Workspace },
            new Role { Id = RoleIds.Admin,       Name = RoleNames.Admin,       Scope = RoleScope.Workspace },
            new Role { Id = RoleIds.Editor,      Name = RoleNames.Editor,      Scope = RoleScope.Workspace },
            new Role { Id = RoleIds.Viewer,      Name = RoleNames.Viewer,      Scope = RoleScope.Workspace }
        );
    }
}
