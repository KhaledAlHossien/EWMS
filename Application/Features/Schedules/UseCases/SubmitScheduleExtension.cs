using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.Projects;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using MediatR;
using static Application.Features.Schedules.ScheduleChangeSupport;

namespace Application.Features.Schedules
{
    public record SubmitScheduleExtensionCommand(int Id) : IRequest<ScheduleExtensionResponseDto>;


    public class SubmitScheduleExtensionHandler(ScheduleChangeContext ctx) : IRequestHandler<SubmitScheduleExtensionCommand, ScheduleExtensionResponseDto>
    {
        public async Task<ScheduleExtensionResponseDto> Handle(SubmitScheduleExtensionCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var extension = await ctx.LoadExtensionAsync(viewer, request.Id);
            var project = extension.Project;
            ProjectRules.EnsureOpen(project);
            EnsureRequesterDraft(viewer, extension);
            EnsureApproved(extension.Schedule, "الطلب مُعدّ");

            // لا يُرفع طلب تغيّرت تواريخ بنوده بعد إعداده: يُعاد إعداده ليُعاين من جديد
            var schedule = await ctx.Schedules.GetByIdAsync(extension.ScheduleId) ?? throw new KeyNotFoundException("الجدول غير موجود");
            if (ScheduleRules.StalePreviewProblem(extension.Items, schedule.Tasks.ToDictionary(t => t.Id)) is string stale)
                throw new InvalidOperationException(stale);

            extension.Status = ExtensionStatus.Submitted;
            extension.RequestedAt = DateTime.UtcNow;
            await ctx.Changes.SaveExtensionAsync(extension, null, new AuditEntry(viewer.Id, AuditActions.Submitted, AuditEntities.Extension, extension.Id,
                $"رفع طلب تمديد لجدول «{project.Name}» للاعتماد", $"البنود: {extension.Items.Count}"), project.BranchId);

            await ScheduleNotifier.SendAsync(ctx.Notifications, await ScheduleNotifier.HoldersAsync(ctx.Permissions, project, AppPermissions.ApproveScheduleExtension),
                viewer.Id, project, NotificationType.ScheduleExtensionSubmitted, "طلب تمديد بانتظار الاعتماد",
                $"رفع {viewer.User.FullName} طلب تمديد على جدول المشروع {ScheduleNotifier.Label(project)} ({extension.Items.Count} بنداً)");

            return await ctx.ExtensionDetailAsync(viewer, extension);
        }
    }
}
