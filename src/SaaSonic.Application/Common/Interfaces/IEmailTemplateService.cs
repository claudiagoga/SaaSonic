namespace SaaSonic.Application.Common.Interfaces;


/// Sends emails rendered from a stored template.
/// The template is identified by its slug, and named placeholders
/// (e.g. {Name}, {CallbackUrl}) are replaced at send-time.

public interface IEmailTemplateService
{
    
    /// Fetch the template identified by <paramref name="templateSlug"/>,
    /// replace every <c>{Key}</c> occurrence in both subject and body with
    /// the corresponding value in <paramref name="placeholders"/>, then send.
  
    Task SendFromTemplateAsync(
        string toEmail,
        string templateSlug,
        Dictionary<string, string> placeholders,
        CancellationToken cancellationToken = default);
}
