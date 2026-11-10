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

            // فرع الدراسات المركزية: أدوات المعالجات المشتركة (المستخدم الحالي، السجل، التواقيع، الإشعارات)
            services.AddScoped<Application.Features.CentralStudies.StudyKit>();

            // فرع الدراسات: الدوال المشتركة بين عمليات كل ميزة (تحميل السجل، التحويل إلى DTO…) — صنف «Support» لكل ميزة
            services.AddScoped<Application.Features.CentralStudies.ActivityLogs.StudyActivityLogSupport>();
            services.AddScoped<Application.Features.CentralStudies.Assignments.StudyAssignmentSupport>();
            services.AddScoped<Application.Features.CentralStudies.Attachments.StudyAttachmentSupport>();
            services.AddScoped<Application.Features.CentralStudies.Drawings.StudyDrawingSupport>();
            services.AddScoped<Application.Features.CentralStudies.Projects.StudyProjectSupport>();
            services.AddScoped<Application.Features.CentralStudies.Requests.StudyRequestSupport>();
            services.AddScoped<Application.Features.CentralStudies.Responses.StudyResponseSupport>();
            services.AddScoped<Application.Features.CentralStudies.Stages.StudyStageSupport>();
            services.AddScoped<Application.Features.CentralStudies.StartOrders.StudyStartOrderSupport>();

            return services;
        }
    }
}