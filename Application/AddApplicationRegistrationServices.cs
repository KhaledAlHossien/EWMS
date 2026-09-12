using Application.Common.Behaviors;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Application
{
    public static class AddApplicationRegistrationServices
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            var applicationAssembly = typeof(AddApplicationRegistrationServices).Assembly;

            // AutoMapper
            services.AddAutoMapper(cfg =>
            {
                cfg.AddMaps(applicationAssembly);
            });

            // MediatR + ValidationBehavior
            services.AddMediatR(cfg =>
            {
                cfg.RegisterServicesFromAssembly(applicationAssembly);
                cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
            });

            // FluentValidation
            services.AddValidatorsFromAssembly(applicationAssembly);

            return services;
        }
    }
}