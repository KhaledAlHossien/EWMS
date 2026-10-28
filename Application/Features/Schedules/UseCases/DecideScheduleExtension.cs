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
    public record DecideScheduleExtensionCommand(int Id, ScheduleDecisionRequestDto Dto) : IRequest<ScheduleExtensionResponseDto>;

    public class DecideScheduleExtensionCommandValidator : AbstractValidator<DecideScheduleExtensionCommand>
    {
        public DecideScheduleExtensionCommandValidator() =>
            RuleFor(x => x.Dto.Note).MaximumLength(1000).WithMessage("الملاحظة لا تتجاوز 1000 حرف");
    }

    public class DecideScheduleExtensionHandler(ScheduleChangeContext ctx) : IRequestHandler<DecideScheduleExtensionCommand, ScheduleExtensionResponseDto>
    {
        public async Task<ScheduleExtensionResponseDto> Handle(DecideScheduleExtensionCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var extension = await ctx.LoadExtensionAsync(viewer, request.Id);
            var project = extension.Project;
            ProjectRules.EnsureOpen(project);
            var dto = request.Dto;
            ScheduleRules.EnsureCanDecideExtension(viewer, extension);
            if (!dto.Approve && string.IsNullOrWhiteSpace(dto.Note))
                throw new InvalidOperationException("اذكر سبب رفض التمديد");

            var details = new List<string>();
            if (dto.Approve)
            {
                EnsureApproved(extension.Schedule, "الطلب مُعدّ");
                var schedule = await ctx.Schedules.GetByIdAsync(extension.ScheduleId) ?? throw new KeyNotFoundException("الجدول غير موجود");
                var byId = schedule.Tasks.ToDictionary(t => t.Id);
                if (ScheduleRules.StalePreviewProblem(extension.Items, byId) is string stale) throw new InvalidOperationException(stale);

                details.AddRange(extension.Items.Select(i => $"«{byId[i.TaskId].Name}»: {Format(i.OldEnd)} ← {Format(i.NewEnd)} (+{i.Days})"));
                ScheduleRules.ApplyExtension(extension.Items, byId);      // الأصلية لا تُمسّ: الحالية فقط

                // التأخيرات القائمة على البنود الممدودة ينتهي أثرها
                var resolvable = await ctx.Changes.GetResolvableDelaysAsync(extension.Items.Select(i => i.TaskId).ToList());
                foreach (var delay in resolvable) delay.Status = DelayStatus.Resolved;
            }
            else details.Add(dto.Note!.Trim());

            extension.Status = dto.Approve ? ExtensionStatus.Approved : ExtensionStatus.Rejected;
            extension.DecidedByUserId = viewer.Id;
            extension.DecidedAt = DateTime.UtcNow;
            extension.DecisionNote = (dto.Note ?? string.Empty).Trim();

            await ctx.Changes.SaveExtensionAsync(extension, dto.RowVersion, new AuditEntry(viewer.Id, dto.Approve ? AuditActions.Approved : AuditActions.Rejected,
                AuditEntities.Extension, extension.Id, $"{(dto.Approve ? "اعتماد" : "رفض")} طلب تمديد لجدول «{project.Name}»",
                string.Join("\n", details)), project.BranchId);

            await ScheduleNotifier.SendAsync(ctx.Notifications, [extension.RequestedByUserId], viewer.Id, project, NotificationType.ScheduleExtensionDecided,
                dto.Approve ? "اعتُمد التمديد" : "رُفض التمديد",
                $"{(dto.Approve ? "اعتمد" : "رفض")} {viewer.User.FullName} طلب التمديد على جدول المشروع {ScheduleNotifier.Label(project)}"
                + (extension.DecisionNote.Length > 0 ? $" — {extension.DecisionNote}" : string.Empty));

            return await ctx.ExtensionDetailAsync(viewer, await ctx.LoadExtensionAsync(viewer, extension.Id));
        }
    }
}
