namespace SaaSonic.Application.Common.Interfaces;

/// <summary>
/// Enqueues an email for delivery by writing a PendingEmail row to the database.
/// The row is committed atomically with whatever SaveChangesAsync the caller issues next,
/// so SMTP failure can never roll back the originating business operation.
/// </summary>
public interface IEmailQueue
{
    /// <summary>
    /// Stages the email in the EF change tracker. Does NOT save — the caller must call
    /// SaveChangesAsync to persist both the email and any accompanying entity changes.
    /// </summary>
    void Enqueue(
        string toEmail,
        string templateSlug,
        Dictionary<string, string> placeholders);
}
