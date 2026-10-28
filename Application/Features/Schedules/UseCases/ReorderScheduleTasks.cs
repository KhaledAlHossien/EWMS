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
    public record ReorderScheduleTasksCommand(int ScheduleId, ReorderScheduleTasksRequestDto Dto) : IRequest<Unit>;


    public class ReorderScheduleTasksHandler(ScheduleContext ctx) : IRequestHandler<ReorderScheduleTasksCommand, Unit>
    {
        public async Task<Unit> Handle(ReorderScheduleTasksCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var schedule = await ctx.LoadScheduleAsync(viewer, request.ScheduleId);
            EnsureDraftEditable(schedule);

            var ids = request.Dto.TaskIds;
            var current = schedule.Tasks.Select(t => t.Id).ToHashSet();
            if (ids.Count != current.Count || ids.Distinct().Count() != ids.Count || !ids.All(current.Contains))
                throw new InvalidOperationException("قائمة الترتيب يجب أن تضم كل بنود المسودة مرة واحدة");

            await ctx.Schedules.ReorderAsync(schedule.Id, ids, new AuditEntry(viewer.Id, AuditActions.Reordered, AuditEntities.Schedule, schedule.Id,
                $"إعادة ترتيب بنود الجدول {schedule.VersionNumber} من «{schedule.Project.Name}»"), schedule.Project.BranchId);
            return Unit.Value;
        }
    }
}
