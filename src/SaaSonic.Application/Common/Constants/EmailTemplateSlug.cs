namespace SaaSonic.Application.Common.Constants;

/// <summary>
/// Well-known slugs that map to rows in the EmailTemplates table.
/// Each slug corresponds to a stored template whose body and subject
/// may contain named placeholders such as {Name}, {CallbackUrl}, {Token}.
/// </summary>
public static class EmailTemplateSlug
{
    public const string EmailVerification = "email-verification";
    public const string PasswordReset = "password-reset";
    public const string Welcome = "welcome";
}
