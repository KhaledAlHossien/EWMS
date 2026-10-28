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
    public record RemoveScheduleDependencyCommand(int Id) : IRequest<Unit>;


    public class RemoveScheduleDependencyHandler(ScheduleContext ctx) : IRequestHandler<RemoveScheduleDependencyCommand, Unit>
    {
        public async Task<Unit> Handle(RemoveScheduleDependencyCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var dependency = await ctx.Schedules.GetDependencyAsync(request.Id) ?? throw new KeyNotFoundException("التبعية غير موجودة");
            var schedule = dependency.Schedule;
            ProjectRules.EnsureView(viewer, schedule.Project);
            EnsureDraftEditable(schedule);

            await ctx.Schedules.RemoveDependencyAsync(dependency, new AuditEntry(viewer.Id, AuditActions.Deleted, AuditEntities.ScheduleDependency,
                dependency.Id, $"حذف تبعية من جدول «{schedule.Project.Name}»",
                $"«{dependency.Predecessor.Name}» ← «{dependency.Successor.Name}»"), schedule.Project.BranchId);
            return Unit.Value;
        }
    }
}
