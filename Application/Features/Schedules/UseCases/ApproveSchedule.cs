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
    public record ApproveScheduleCommand(int Id) : IRequest<ScheduleDetailResponseDto>;


    public class ApproveScheduleHandler(ScheduleContext ctx) : IRequestHandler<ApproveScheduleCommand, ScheduleDetailResponseDto>
    {
        public async Task<ScheduleDetailResponseDto> Handle(ApproveScheduleCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var schedule = await ctx.LoadScheduleAsync(viewer, request.Id);
            ProjectRules.EnsureOpen(schedule.Project);
            ScheduleRules.EnsureCanApprove(viewer, schedule);

            var dependencies = await ctx.Schedules.GetDependenciesAsync(schedule.Id);
            var edges = dependencies.Select(d => new ScheduleRules.Edge(d.PredecessorId, d.SuccessorId)).ToList();
            if (ScheduleRules.ValidateForApproval(schedule.Tasks.ToList(), edges) is string problem)
                throw new InvalidOperationException(problem);

            var previous = await ctx.Schedules.GetApprovedAsync(schedule.ProjectId);
            var details = $"البنود: {schedule.Tasks.Count}\nالتبعيات: {dependencies.Count}"
                          + (previous != null ? $"\nحلّ محل الإصدار {previous.VersionNumber}" : "\nأول إصدار معتمد: ثُبّتت التواريخ الأصلية");

            await ctx.Schedules.ApproveAsync(schedule, previous, viewer.Id, new AuditEntry(viewer.Id, AuditActions.Approved, AuditEntities.Schedule,
                schedule.Id, $"اعتماد الجدول {schedule.VersionNumber} للمشروع «{schedule.Project.Name}»", details), schedule.Project.BranchId);

            return await ctx.DetailAsync(viewer, await ctx.LoadScheduleAsync(viewer, schedule.Id));
        }
    }
}
