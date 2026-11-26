using Application.Common.Behaviors;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

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
            services.AddScoped<Application.Features.CentralStudies.Responses.StudyDiwanSupport>();

            // بقية الميزات: الدوال المشتركة بين عملياتها — صنف «Support» لكل ميزة بالنمط نفسه
            services.AddScoped<Application.Features.AssignedTasks.AssignedTaskExtrasSupport>();
            services.AddScoped<Application.Features.AssignedTasks.TaskTemplatesSupport>();
            services.AddScoped<Application.Features.AssignedTasks.TaskRecurrencesSupport>();
            services.AddScoped<Application.Features.AssignedTasks.TaskLinksSupport>();
            services.AddScoped<Application.Features.DeviceInventory.Installations.InstallationSupport>();
            services.AddScoped<Application.Features.DeviceInventory.Sites.SiteSupport>();
            services.AddScoped<Application.Features.Maintenance.MaintenanceDevices.DeviceMaintenanceSupport>();
            services.AddScoped<Application.Features.Maintenance.Requests.MaintenanceRequestSupport>();
            services.AddScoped<Application.Features.Maintenance.Requests.MaintenanceRequestQuerySupport>();
            services.AddScoped<Application.Features.Maintenance.SpareParts.SparePartSupport>();
            services.AddScoped<Application.Features.Maintenance.Tasks.MaintenanceTaskSupport>();
            services.AddScoped<Application.Features.Maintenance.Transfers.MaintenanceTransferSupport>();
            services.AddScoped<Application.Features.MapLayers.Entries.MapEntrySupport>();
            services.AddScoped<Application.Features.MapLayers.Layers.MapLayerSupport>();
            services.AddScoped<Application.Features.Reports.ReportSupport>();
            services.AddScoped<Application.Features.ToDoLists.ToDoListSupport>();
            services.AddScoped<Application.Features.ToDoLists.Items.ToDoItemSupport>();
            services.AddScoped<Application.Features.Vacations.Holidays.PublicHolidaySupport>();

            return services;
        }
    }
}