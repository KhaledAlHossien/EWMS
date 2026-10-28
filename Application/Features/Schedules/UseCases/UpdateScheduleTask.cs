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
    public record UpdateScheduleTaskCommand(int TaskId, ScheduleTaskRequestDto Dto) : IRequest<ScheduleTaskResponseDto>;

    public class UpdateScheduleTaskCommandValidator : AbstractValidator<UpdateScheduleTaskCommand>
    {
        public UpdateScheduleTaskCommandValidator() => RuleFor(x => x.Dto).SetValidator(new ScheduleTaskRequestDtoValidator());
    }

    public class UpdateScheduleTaskHandler(ScheduleContext ctx) : IRequestHandler<UpdateScheduleTaskCommand, ScheduleTaskResponseDto>
    {
        public async Task<ScheduleTaskResponseDto> Handle(UpdateScheduleTaskCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var task = await ctx.LoadTaskAsync(viewer, request.TaskId);
            var schedule = task.Schedule;
            EnsureDraftEditable(schedule);
            var dto = request.Dto;

            if (ScheduleRules.ValidateTask(dto.Name, dto.PlannedStart, dto.PlannedEnd, dto.Weight) is string problem)
                throw new InvalidOperationException(problem);

            // بند له تواريخ أصلية (منقول من جدول معتمد): تواريخه الحالية لا تتغير إلا بتمديد معتمد
            var start = dto.PlannedStart.Date;
            var end = dto.PlannedEnd.Date;
            if (task.OriginalStart != null && (start != task.PlannedStart.Date || end != task.PlannedEnd.Date))
                throw new InvalidOperationException("تواريخ هذا البند مثبّتة من الجدول المعتمد وتتغير بتمديد معتمد فقط");

            var assignee = AssigneeOrNull(schedule.Project, dto.AssigneeUserId);

            var changes = new List<string>();
            void Track(string label, string? from, string? to)
            {
                if (!string.Equals(from ?? string.Empty, to ?? string.Empty, StringComparison.Ordinal))
                    changes.Add($"{label}: {(string.IsNullOrEmpty(from) ? "—" : from)} ← {(string.IsNullOrEmpty(to) ? "—" : to)}");
            }
            var name = dto.Name.Trim();
            var code = (dto.Code ?? string.Empty).Trim();
            var note = (dto.Note ?? string.Empty).Trim();
            Track("الاسم", task.Name, name);
            Track("الرمز", task.Code, code);
            Track("البداية", Format(task.PlannedStart), Format(start));
            Track("النهاية", Format(task.PlannedEnd), Format(end));
            Track("الوزن", task.Weight.ToString("0.##"), dto.Weight.ToString("0.##"));
            Track("المسؤول", task.Assignee?.FullName, assignee?.FullName);
            if (task.Note != note) changes.Add("الملاحظة: تغيّرت");
            if (changes.Count == 0) return ToTaskDto(task, approved: false, Today);

            task.Name = name;
            task.Code = code;
            task.PlannedStart = start;
            task.PlannedEnd = end;
            task.Weight = dto.Weight;
            task.Note = note;
            task.AssigneeUserId = assignee?.Id;
            task.Assignee = assignee;

            await ctx.Schedules.UpdateTaskAsync(task, dto.RowVersion, new AuditEntry(viewer.Id, AuditActions.Updated, AuditEntities.ScheduleTask,
                task.Id, $"تعديل البند «{task.Name}» في جدول «{schedule.Project.Name}»", string.Join("\n", changes)), schedule.Project.BranchId);
            return ToTaskDto(task, approved: false, Today);
        }
    }
}
