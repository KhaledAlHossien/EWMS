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
    public record DeleteScheduleTaskCommand(int TaskId) : IRequest<Unit>;


    public class DeleteScheduleTaskHandler(ScheduleContext ctx) : IRequestHandler<DeleteScheduleTaskCommand, Unit>
    {
        public async Task<Unit> Handle(DeleteScheduleTaskCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var task = await ctx.LoadTaskAsync(viewer, request.TaskId);
            EnsureDraftEditable(task.Schedule);

            await ctx.Schedules.DeleteTaskAsync(task, new AuditEntry(viewer.Id, AuditActions.Deleted, AuditEntities.ScheduleTask, task.Id,
                $"حذف البند «{task.Name}» من جدول «{task.Schedule.Project.Name}»",
                $"{Format(task.PlannedStart)} → {Format(task.PlannedEnd)}"), task.Schedule.Project.BranchId);
            return Unit.Value;
        }
    }
}
