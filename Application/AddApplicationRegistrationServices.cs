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

            // لوحة المهام: إنشاء المهام (يدوياً ومن التكرار) وربطها بالسجلات
            services.AddScoped<Application.Features.AssignedTasks.AssignedTaskCreator>();
            services.AddScoped<Application.Features.AssignedTasks.TaskLinkResolver>();
            services.AddScoped<Application.Features.Projects.ProjectUploadFinalizer>();
            // سياقات المعالجات: التبعيات والتحميل المشترك لحالات الاستخدام (معالج لكل حالة)
            services.AddScoped<Application.Features.Projects.ProjectContext>();
            services.AddScoped<Application.Features.Projects.ProjectDocumentContext>();
            services.AddScoped<Application.Features.Projects.ProjectUploadContext>();
            services.AddScoped<Application.Features.Schedules.ScheduleContext>();
            services.AddScoped<Application.Features.Schedules.ScheduleChangeContext>();
            services.AddScoped<Application.Features.Diwan.DiwanContext>();
            services.AddScoped<Application.Features.Studies.StudyContext>();
            services.AddScoped<Application.Features.Contracts.ContractContext>();
            services.AddScoped<Application.Features.Handover.HandoverContext>();

            return services;
        }
    }
}