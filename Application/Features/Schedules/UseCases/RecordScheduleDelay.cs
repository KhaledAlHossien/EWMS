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
    public record RecordScheduleDelayCommand(int TaskId, ScheduleDelayRequestDto Dto) : IRequest<ScheduleDelayResponseDto>;

    public class RecordScheduleDelayCommandValidator : AbstractValidator<RecordScheduleDelayCommand>
    {
        public RecordScheduleDelayCommandValidator()
        {
            RuleFor(x => x.Dto.Kind).Must(k => Enum.IsDefined(typeof(DelayKind), k)).WithMessage("نوع التأخير غير صحيح");
            RuleFor(x => x.Dto.Reason).MaximumLength(2000).WithMessage("السبب لا يتجاوز 2000 حرف");
            RuleFor(x => x.Dto.Actions).MaximumLength(2000).WithMessage("الإجراءات لا تتجاوز 2000 حرف");
        }
    }

    public class RecordScheduleDelayHandler(ScheduleChangeContext ctx) : IRequestHandler<RecordScheduleDelayCommand, ScheduleDelayResponseDto>
    {
        public async Task<ScheduleDelayResponseDto> Handle(RecordScheduleDelayCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var task = await ctx.Schedules.GetTaskAsync(request.TaskId) ?? throw new KeyNotFoundException("البند غير موجود");
            var project = task.Schedule.Project;
            ProjectRules.EnsureView(viewer, project);
            ProjectRules.EnsureOpen(project);
            EnsureApproved(task.Schedule, "التأخير يُسجَّل");
            var dto = request.Dto;
            var kind = (DelayKind)dto.Kind;

            if (ScheduleRules.ValidateDelayRecord(task, kind, dto.DiscoveredAt, dto.Reason, dto.Actions, Today) is string problem)
                throw new InvalidOperationException(problem);
            if (await ctx.Changes.HasLiveDelayAsync(task.Id))
                throw new InvalidOperationException("يوجد تأخير قائم على هذا البند: عالجه (برّره أو مدّد البند) قبل تسجيل غيره");

            var affected = await ctx.ResolveAffectedAsync(task.ScheduleId, task.Id, dto.AffectedTaskIds);
            var delay = new ScheduleDelay
            {
                ProjectId = project.Id,
                TaskId = task.Id,
                Kind = kind,
                Status = DelayStatus.Recorded,
                DiscoveredAt = dto.DiscoveredAt.Date,
                PlannedEndAtDiscovery = task.PlannedEnd.Date,
                Reason = (dto.Reason ?? string.Empty).Trim(),
                Actions = (dto.Actions ?? string.Empty).Trim(),
                RecordedByUserId = viewer.Id,
                RecordedAt = DateTime.UtcNow,
                AffectedTasks = affected.Select(t => new ScheduleDelayAffectedTask { TaskId = t.Id }).ToList()
            };

            var late = Math.Max(0, (delay.DiscoveredAt - delay.PlannedEndAtDiscovery).Days);
            await ctx.Changes.AddDelayAsync(delay, new AuditEntry(viewer.Id, AuditActions.Created, AuditEntities.Delay, null,
                $"تسجيل تأخير على «{task.Name}» في «{project.Name}»",
                $"النوع: {ScheduleRules.DelayKindAr(kind)}\nنهاية البند المخططة: {Format(delay.PlannedEndAtDiscovery)}\nتاريخ الاكتشاف: {Format(delay.DiscoveredAt)} ({late} يوماً)"
                + (affected.Count > 0 ? $"\nمتأثرة: {string.Join("، ", affected.Select(t => t.Name))}" : string.Empty)), project.BranchId);

            return await ctx.DelayDetailAsync(viewer, await ctx.LoadDelayAsync(viewer, delay.Id));
        }
    }
}
