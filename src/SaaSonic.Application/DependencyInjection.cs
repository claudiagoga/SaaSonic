using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using SaaSonic.Application.Common.Behaviours;
using System.Reflection;

namespace SaaSonic.Application;

public static class ApplicationServiceCollectionExtensions 
{
    extension (IServiceCollection services)
    {
            public  IServiceCollection AddApplication()
            {
                services.AddMediatR(cfg =>
                {
                    cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
                });

                services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

                services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehaviour<,>));

                return services;
            }
    }
}
