using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SaaSonic.Application.Common.Interfaces;
using SaaSonic.Domain.Enums;
using SaaSonic.Infrastructure.Persistence;
using System.Text.Json;

namespace SaaSonic.Infrastructure.Email;

internal sealed class EmailSenderWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EmailSenderWorker> _logger;

    private const int MaxRetries = 5;
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(30);

    public EmailSenderWorker(IServiceScopeFactory scopeFactory, ILogger<EmailSenderWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Unhandled error in EmailSenderWorker.");
            }

            await Task.Delay(PollingInterval, stoppingToken);
        }
    }

    private async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailTemplateService>();

        var pending = await db.PendingEmails
            .AsTracking()
            .Where(e => e.Status == PendingEmailStatus.Pending
                     && e.NextRetryAt <= DateTimeOffset.UtcNow)
            .OrderBy(e => e.CreatedAt)
            .Take(20)
            .ToListAsync(cancellationToken);

        if (pending.Count == 0) return;

        foreach (var email in pending)
        {
            try
            {
                var placeholders = JsonSerializer.Deserialize<Dictionary<string, string>>(email.Placeholders) ?? [];

                await emailService.SendFromTemplateAsync(
                    email.ToEmail,
                    email.TemplateSlug,
                    placeholders,
                    cancellationToken);

                email.Status = PendingEmailStatus.Sent;
                email.SentAt = DateTimeOffset.UtcNow;
                email.ErrorMessage = null;

                _logger.LogInformation(
                    "Email sent to {Email} (template: {Slug}).", email.ToEmail, email.TemplateSlug);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                email.RetryCount++;
                email.ErrorMessage = ex.Message;

                if (email.RetryCount >= MaxRetries)
                {
                    email.Status = PendingEmailStatus.Failed;
                    _logger.LogError(ex,
                        "Email to {Email} permanently failed after {Retries} retries.",
                        email.ToEmail, email.RetryCount);
                }
                else
                {
                    // Exponential backoff: 2 → 4 → 8 → 16 → 32 minutes
                    email.NextRetryAt = DateTimeOffset.UtcNow.AddMinutes(Math.Pow(2, email.RetryCount));
                    _logger.LogWarning(ex,
                        "Email to {Email} failed (attempt {Attempt}/{Max}). Next retry at {NextRetry}.",
                        email.ToEmail, email.RetryCount, MaxRetries, email.NextRetryAt);
                }
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
