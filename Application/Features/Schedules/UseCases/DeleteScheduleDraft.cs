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
    public record DeleteScheduleDraftCommand(int Id) : IRequest<Unit>;


    public class DeleteScheduleDraftHandler(ScheduleContext ctx) : IRequestHandler<DeleteScheduleDraftCommand, Unit>
    {
        public async Task<Unit> Handle(DeleteScheduleDraftCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var schedule = await ctx.LoadScheduleAsync(viewer, request.Id);
            EnsureDraftEditable(schedule);

            await ctx.Schedules.DeleteDraftAsync(schedule, new AuditEntry(viewer.Id, AuditActions.Deleted, AuditEntities.Schedule, schedule.Id,
                $"حذف مسودة الجدول {schedule.VersionNumber} من «{schedule.Project.Name}»", $"البنود: {schedule.Tasks.Count}"), schedule.Project.BranchId);
            return Unit.Value;
        }
    }
}
