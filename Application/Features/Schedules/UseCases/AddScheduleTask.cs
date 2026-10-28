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
    public record AddScheduleTaskCommand(int ScheduleId, ScheduleTaskRequestDto Dto) : IRequest<ScheduleTaskResponseDto>;

    public class AddScheduleTaskCommandValidator : AbstractValidator<AddScheduleTaskCommand>
    {
        public AddScheduleTaskCommandValidator() => RuleFor(x => x.Dto).SetValidator(new ScheduleTaskRequestDtoValidator());
    }

    public class AddScheduleTaskHandler(ScheduleContext ctx) : IRequestHandler<AddScheduleTaskCommand, ScheduleTaskResponseDto>
    {
        public async Task<ScheduleTaskResponseDto> Handle(AddScheduleTaskCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var schedule = await ctx.LoadScheduleAsync(viewer, request.ScheduleId);
            EnsureDraftEditable(schedule);
            var dto = request.Dto;

            if (schedule.Tasks.Count >= ScheduleRules.MaxTasks)
                throw new InvalidOperationException($"بلغ الجدول الحد الأقصى للبنود ({ScheduleRules.MaxTasks})");
            if (ScheduleRules.ValidateTask(dto.Name, dto.PlannedStart, dto.PlannedEnd, dto.Weight) is string problem)
                throw new InvalidOperationException(problem);
            var assignee = AssigneeOrNull(schedule.Project, dto.AssigneeUserId);

            var task = new ScheduleTask
            {
                ScheduleId = schedule.Id,
                SortOrder = schedule.Tasks.Count == 0 ? 1 : schedule.Tasks.Max(t => t.SortOrder) + 1,
                Code = (dto.Code ?? string.Empty).Trim(),
                Name = dto.Name.Trim(),
                PlannedStart = dto.PlannedStart.Date,
                PlannedEnd = dto.PlannedEnd.Date,
                Weight = dto.Weight,
                Note = (dto.Note ?? string.Empty).Trim(),
                AssigneeUserId = assignee?.Id,
                Status = ScheduleTaskStatus.NotStarted,
                Progress = 0
            };

            await ctx.Schedules.AddTaskAsync(task, new AuditEntry(viewer.Id, AuditActions.Created, AuditEntities.ScheduleTask, null,
                $"إضافة بند «{task.Name}» إلى جدول «{schedule.Project.Name}»",
                $"{Format(task.PlannedStart)} → {Format(task.PlannedEnd)}\nالوزن: {task.Weight:0.##}"), schedule.Project.BranchId);

            task.Assignee = assignee;
            return ToTaskDto(task, approved: false, Today);
        }
    }
}
