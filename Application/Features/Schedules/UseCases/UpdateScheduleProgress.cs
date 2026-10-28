using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.Projects;
using Application.Interfaces;
using Domain.Entities;
using FluentValidation;
using MediatR;
using static Application.Features.Schedules.ScheduleSupport;

namespace Application.Features.Schedules
{
    public record UpdateScheduleProgressCommand(int TaskId, UpdateScheduleProgressRequestDto Dto) : IRequest<ScheduleTaskResponseDto>;

    public class UpdateScheduleProgressCommandValidator : AbstractValidator<UpdateScheduleProgressCommand>
    {
        public UpdateScheduleProgressCommandValidator()
        {
            RuleFor(x => x.Dto.Status).Must(s => Enum.IsDefined(typeof(ScheduleTaskStatus), s)).WithMessage("حالة البند غير صحيحة");
            RuleFor(x => x.Dto.Note).MaximumLength(1000).WithMessage("الملاحظة لا تتجاوز 1000 حرف");
        }
    }

    public class UpdateScheduleProgressHandler(ScheduleContext ctx) : IRequestHandler<UpdateScheduleProgressCommand, ScheduleTaskResponseDto>
    {
        public async Task<ScheduleTaskResponseDto> Handle(UpdateScheduleProgressCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var task = await ctx.LoadTaskAsync(viewer, request.TaskId);
            var schedule = task.Schedule;
            ProjectRules.EnsureOpen(schedule.Project);
            if (schedule.Status != ScheduleStatus.Approved)
                throw new InvalidOperationException(schedule.Status == ScheduleStatus.Draft
                    ? "الإنجاز يُحدَّث على الجدول المعتمد: اعتمد المسودة أولاً"
                    : "هذا إصدار سابق من الجدول؛ حدّث الإنجاز على الإصدار المعتمد");

            var dto = request.Dto;
            var today = Today;
            var status = (ScheduleTaskStatus)dto.Status;
            var (actualStart, actualEnd) = ScheduleRules.CompleteActualDates(status, dto.ActualStart ?? task.ActualStart, dto.ActualEnd ?? task.ActualEnd, today);
            if (ScheduleRules.ValidateProgress(status, dto.Progress, actualStart, actualEnd, today) is string problem)
                throw new InvalidOperationException(problem);

            var note = (dto.Note ?? string.Empty).Trim();
            if (dto.Progress < task.Progress && note.Length == 0)
                throw new InvalidOperationException("خفض نسبة الإنجاز يحتاج ملاحظة تشرح السبب");

            var unchanged = status == task.Status && dto.Progress == task.Progress
                            && actualStart?.Date == task.ActualStart?.Date && actualEnd?.Date == task.ActualEnd?.Date;
            if (unchanged && note.Length == 0) return ToTaskDto(task, approved: true, today);

            var oldProgress = task.Progress;
            var oldStatus = task.Status;
            task.Status = status;
            task.Progress = dto.Progress;
            task.ActualStart = actualStart?.Date;
            task.ActualEnd = actualEnd?.Date;

            var entry = new ScheduleProgressEntry
            {
                TaskId = task.Id, Progress = dto.Progress, Status = status, Note = note, UserId = viewer.Id, CreatedAt = DateTime.UtcNow
            };
            await ctx.Schedules.SaveProgressAsync(task, entry, dto.RowVersion, new AuditEntry(viewer.Id, AuditActions.Progress, AuditEntities.ScheduleTask,
                task.Id, $"تحديث إنجاز البند «{task.Name}» في «{schedule.Project.Name}»",
                $"النسبة: {oldProgress}% ← {dto.Progress}%\nالحالة: {ScheduleRules.TaskStatusAr(oldStatus)} ← {ScheduleRules.TaskStatusAr(status)}"
                + (note.Length > 0 ? $"\nملاحظة: {note}" : string.Empty)), schedule.Project.BranchId);

            return ToTaskDto(task, approved: true, today);
        }
    }
}
