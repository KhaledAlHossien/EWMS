using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using MediatR;

namespace Application.Features.AssignedTasks.Commands.SaveRecurrence
{
    public class SaveTaskRecurrenceCommandHandler : IRequestHandler<SaveTaskRecurrenceCommand, TaskRecurrenceDto>
    {
        private readonly IAssignedTaskPlanningService _planning;
        private readonly AssignedTaskCreator _creator;
        private readonly TaskRecurrencesSupport _support;

        public SaveTaskRecurrenceCommandHandler(IAssignedTaskPlanningService planning, AssignedTaskCreator creator, TaskRecurrencesSupport support)
        {
            _planning = planning;
            _creator = creator;
            _support = support;
        }

        public async Task<TaskRecurrenceDto> Handle(SaveTaskRecurrenceCommand request, CancellationToken ct)
        {
            var viewer = await _support.AssignerAsync();
            var dto = request.Dto;
            var template = await _planning.GetTemplateAsync(dto.TemplateId) ?? throw new KeyNotFoundException("القالب غير موجود");
            if (template.OwnerUserId != viewer.Id) throw new UnauthorizedAccessException("استعمل قالباً من قوالبك");
            if (!Enum.TryParse<AssignedTaskTargetType>(dto.TargetType, true, out var targetType))
                throw new ArgumentException("نوع الجهة غير صحيح");
            await _creator.EnsureTargetAllowedAsync(viewer, targetType, dto.TargetId);

            var now = DateTime.UtcNow;
            AssignedTaskRecurrence r;
            if (request.Id is int id) r = await _support.OwnedAsync(id, viewer);
            else
            {
                if ((await _planning.GetRecurrencesAsync(viewer.Id)).Count >= TaskPlanningRules.MaxRecurrences)
                    throw new InvalidOperationException($"الحد الأقصى {TaskPlanningRules.MaxRecurrences} مهمة دورية");
                r = new AssignedTaskRecurrence { OwnerUserId = viewer.Id, CreatedAt = now };
            }

            r.TemplateId = template.Id;
            r.Template = template;
            r.TargetType = targetType;
            r.TargetId = dto.TargetId;
            r.Frequency = (TaskRecurrenceFrequency)dto.Frequency;
            r.DayOfWeek = r.Frequency == TaskRecurrenceFrequency.Weekly ? dto.DayOfWeek : null;
            r.DayOfMonth = r.Frequency == TaskRecurrenceFrequency.Monthly ? dto.DayOfMonth : null;
            r.DueAfterDays = dto.DueAfterDays;
            r.StartDate = dto.StartDate.Date;
            r.EndDate = dto.EndDate?.Date;
            r.LastError = null;

            var next = TaskPlanningRules.NextRun(r.Frequency, r.DayOfWeek, r.DayOfMonth, new[] { r.StartDate, DateTime.Today }.Max());
            if (r.EndDate != null && next > r.EndDate) throw new InvalidOperationException("المدة المحددة لا تتضمن أي موعد تنفيذ");
            r.NextRunDate = next;
            r.IsActive = true;

            if (request.Id == null) await _planning.AddRecurrenceAsync(r);
            else await _planning.SaveChangesAsync();
            return TaskPlanningRules.ToDto(r, await _support.TargetNameAsync(r.TargetType, r.TargetId));
        }
    }
}
