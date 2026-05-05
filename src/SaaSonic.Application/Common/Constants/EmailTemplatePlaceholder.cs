namespace SaaSonic.Application.Common.Constants;

/// <summary>
/// Standard placeholder keys used inside email template bodies and subjects.
/// In the stored template, placeholders appear as literal strings: {Name}, {CallbackUrl}, etc.
/// </summary>
public static class EmailTemplatePlaceholder
{
    /// <summary>Recipient's display name.</summary>
    public const string Name = "Name";

    /// <summary>Action URL (verification link, password-reset link, etc.).</summary>
    public const string CallbackUrl = "CallbackUrl";

    /// <summary>Raw token value, when it must appear directly in the body.</summary>
    public const string Token = "Token";
}
