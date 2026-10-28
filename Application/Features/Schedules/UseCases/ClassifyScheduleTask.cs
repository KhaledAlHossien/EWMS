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
    public record ClassifyScheduleTaskCommand(int TaskId, ClassifyScheduleTaskRequestDto Dto) : IRequest<ScheduleTaskResponseDto>;


    public class ClassifyScheduleTaskHandler(ScheduleContext ctx) : IRequestHandler<ClassifyScheduleTaskCommand, ScheduleTaskResponseDto>
    {
        public async Task<ScheduleTaskResponseDto> Handle(ClassifyScheduleTaskCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var task = await ctx.LoadTaskAsync(viewer, request.TaskId);
            var schedule = task.Schedule;
            ProjectRules.EnsureOpen(schedule.Project);
            if (schedule.Status == ScheduleStatus.Superseded)
                throw new InvalidOperationException("هذا إصدار سابق ومحفوظ للمراجعة فقط");

            var approved = schedule.Status == ScheduleStatus.Approved;
            if (task.IsCritical == request.Dto.IsCritical) return ToTaskDto(task, approved, Today);

            task.IsCritical = request.Dto.IsCritical;
            await ctx.Schedules.UpdateTaskAsync(task, request.Dto.RowVersion, new AuditEntry(viewer.Id, AuditActions.Classified, AuditEntities.ScheduleTask,
                task.Id, $"تصنيف البند «{task.Name}» في «{schedule.Project.Name}»", request.Dto.IsCritical ? "حرج" : "غير حرج"), schedule.Project.BranchId);
            return ToTaskDto(task, approved, Today);
        }
    }
}
