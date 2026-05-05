using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SaaSonic.Application.Common.Interfaces;
using SaaSonic.Infrastructure.Auth;
using SaaSonic.Infrastructure.Email;
using SaaSonic.Infrastructure.Persistence;
using SaaSonic.Infrastructure.Security;

namespace SaaSonic.Infrastructure;

public static class DependencyInjection 
{
    extension (IServiceCollection services)
    {
        public IServiceCollection AddInfrastructure(
            IConfiguration configuration)
        {
            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseNpgsql(configuration.GetConnectionString("Default"));
                options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
            });
            services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
            services.AddScoped<IJwtService, JwtService>();
            services.AddScoped<IPasswordHasher, PasswordHasher>();
            services.AddScoped<IEmailService, SmtpEmailService>();
            services.AddScoped<IEmailTemplateService, EmailTemplateService>();
            services.AddScoped<IEmailQueue, DbEmailQueue>();
            services.AddHostedService<EmailSenderWorker>();

            return services;
        }
    }
}
