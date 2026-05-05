using SaaSonic.Domain.Enums;

namespace SaaSonic.Domain.Entities;

public sealed class Role
{
    public Guid Id { get; set; }
     public string Name { get; set; } = string.Empty;
    public RoleScope Scope { get; set; } 
 
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
    public ICollection<WorkspaceMember> WorkspaceMembers { get; set; } = new List<WorkspaceMember>();
    public ICollection<WorkspaceInvitation> WorkspaceInvitations { get; set; } = new List<WorkspaceInvitation>();
}
