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
    public record JustifyScheduleDelayCommand(int Id, JustifyScheduleDelayRequestDto Dto) : IRequest<ScheduleDelayResponseDto>;

    public class JustifyScheduleDelayCommandValidator : AbstractValidator<JustifyScheduleDelayCommand>
    {
        public JustifyScheduleDelayCommandValidator()
        {
            RuleFor(x => x.Dto.Reason).MaximumLength(2000).WithMessage("السبب لا يتجاوز 2000 حرف");
            RuleFor(x => x.Dto.Actions).MaximumLength(2000).WithMessage("الإجراءات لا تتجاوز 2000 حرف");
            RuleFor(x => x.Dto.ApprovalAuthority).MaximumLength(200).WithMessage("اسم الجهة لا يتجاوز 200 حرف");
        }
    }

    public class JustifyScheduleDelayHandler(ScheduleChangeContext ctx) : IRequestHandler<JustifyScheduleDelayCommand, ScheduleDelayResponseDto>
    {
        public async Task<ScheduleDelayResponseDto> Handle(JustifyScheduleDelayCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var delay = await ctx.LoadDelayAsync(viewer, request.Id);
            var project = delay.Project;
            ProjectRules.EnsureOpen(project);
            EnsureApproved(delay.Task.Schedule, "التأخير مسجَّل");
            var dto = request.Dto;

            // من سجّل التأخير يرفع تبريره وحده، كي لا يعتمد من رفعه تبريره بنفسه (الاعتماد لغير المسجِّل)
            if (delay.RecordedByUserId != viewer.Id)
                throw new InvalidOperationException("يرفع تبرير التأخير من سجّله");
            if (delay.Status is not (DelayStatus.Recorded or DelayStatus.Rejected))
                throw new InvalidOperationException("لا يُرفع تبرير لهذا التأخير في حالته الحالية");

            if (ScheduleRules.ValidateJustification(dto.Reason, dto.ApprovalAuthority, dto.ApprovalDate, dto.ApprovalDocumentVersionId, Today) is string problem)
                throw new InvalidOperationException(problem);
            var version = await ctx.Documents.GetVersionAsync(dto.ApprovalDocumentVersionId!.Value) ?? throw new KeyNotFoundException("مستند الموافقة غير موجود");
            if (version.Document.ProjectId != project.Id)
                throw new InvalidOperationException("مستند الموافقة يجب أن يكون من وثائق هذا المشروع");

            var affected = await ctx.ResolveAffectedAsync(delay.Task.ScheduleId, delay.TaskId, dto.AffectedTaskIds);
            SyncAffected(delay, affected);

            delay.Kind = DelayKind.Justified;
            delay.Status = DelayStatus.Submitted;
            delay.Reason = dto.Reason!.Trim();
            delay.Actions = (dto.Actions ?? string.Empty).Trim();
            delay.ApprovalAuthority = dto.ApprovalAuthority!.Trim();
            delay.ApprovalDate = dto.ApprovalDate!.Value.Date;
            delay.ApprovalDocumentVersionId = version.Id;     // المفتاح فقط: إسناد الكيان المقروء بلا تتبّع يُدخل نسخة ثانية من المشروع في التتبّع
            delay.AffectsCriticalWork = dto.AffectsCriticalWork;
            delay.DecidedByUserId = null;
            delay.DecidedAt = null;
            delay.DecisionNote = string.Empty;

            await ctx.Changes.SaveDelayAsync(delay, dto.RowVersion, new AuditEntry(viewer.Id, AuditActions.Justified, AuditEntities.Delay, delay.Id,
                $"رفع تبرير تأخير «{delay.Task.Name}» في «{project.Name}»",
                $"السبب: {delay.Reason}\nالجهة الموافقة: {delay.ApprovalAuthority} ({Format(delay.ApprovalDate.Value)})\nالمستند: {version.Document.Title} — {version.FileName}"
                + (delay.AffectsCriticalWork ? "\nيؤثر على أعمال حرجة" : string.Empty)), project.BranchId);

            await ScheduleNotifier.SendAsync(ctx.Notifications, await ScheduleNotifier.HoldersAsync(ctx.Permissions, project, AppPermissions.ApproveDelayJustification),
                viewer.Id, project, NotificationType.ScheduleJustificationSubmitted, "تبرير تأخير بانتظار الاعتماد",
                $"رفع {viewer.User.FullName} تبرير تأخير البند «{delay.Task.Name}» في المشروع {ScheduleNotifier.Label(project)}");

            return await ctx.DelayDetailAsync(viewer, await ctx.LoadDelayAsync(viewer, delay.Id));
        }

        /// <summary>يحدّث البنود المتأثرة بالفرق فقط (مفتاح الجدول مركّب، فإعادة إضافة بند محذوف في الحفظ نفسه تُربك التتبع)</summary>
        private static void SyncAffected(ScheduleDelay delay, IReadOnlyList<ScheduleTask> wanted)
        {
            var keep = wanted.Select(t => t.Id).ToHashSet();
            foreach (var old in delay.AffectedTasks.Where(a => !keep.Contains(a.TaskId)).ToList()) delay.AffectedTasks.Remove(old);
            var have = delay.AffectedTasks.Select(a => a.TaskId).ToHashSet();
            foreach (var id in keep.Where(id => !have.Contains(id))) delay.AffectedTasks.Add(new ScheduleDelayAffectedTask { DelayId = delay.Id, TaskId = id });
        }
    }
}
