namespace SaaSonic.Domain.Constants;

public static class RoleIds
{
    public static readonly Guid SystemAdmin = new("00000000-0000-0000-0000-000000000001");
    public static readonly Guid Owner       = new("00000000-0000-0000-0000-000000000002");
    public static readonly Guid Admin       = new("00000000-0000-0000-0000-000000000003");
    public static readonly Guid Editor      = new("00000000-0000-0000-0000-000000000004");
    public static readonly Guid Viewer      = new("00000000-0000-0000-0000-000000000005");
}

public static class RoleNames
{
    public const string SystemAdmin = "SystemAdmin";
    public const string Owner       = "Owner";
    public const string Admin       = "Admin";
    public const string Editor      = "Editor";
    public const string Viewer      = "Viewer";
}
