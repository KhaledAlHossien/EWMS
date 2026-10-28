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
    public record GetScheduleOverviewQuery(int ProjectId) : IRequest<ScheduleOverviewResponseDto>;


    public class GetScheduleOverviewHandler(ScheduleContext ctx) : IRequestHandler<GetScheduleOverviewQuery, ScheduleOverviewResponseDto>
    {
        public async Task<ScheduleOverviewResponseDto> Handle(GetScheduleOverviewQuery request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var project = await ctx.LoadProjectAsync(viewer, request.ProjectId);
            var startOrder = await ctx.Schedules.GetStartOrderAsync(project.Id);
            var versions = await ctx.Schedules.GetVersionsAsync(project.Id);

            var dto = new ScheduleOverviewResponseDto
            {
                ProjectId = project.Id,
                ProjectNumber = ProjectRules.Number(project.Id, project.CreatedAt),
                ProjectName = project.Name,
                ProjectFinished = ProjectRules.IsFinished(project.Stage),
                StartOrder = startOrder == null ? null : ToStartOrderDto(startOrder),
                Versions = versions.OrderByDescending(v => v.Schedule.VersionNumber).Select(ToVersionDto).ToList(),
                ApprovedScheduleId = versions.FirstOrDefault(v => v.Schedule.Status == ScheduleStatus.Approved)?.Schedule.Id,
                DraftScheduleId = versions.FirstOrDefault(v => v.Schedule.Status == ScheduleStatus.Draft)?.Schedule.Id,
                CanEditStartOrder = viewer.Has(AppPermissions.EditProjectStartOrder) && !ProjectRules.IsFinished(project.Stage),
                CanEditSchedule = viewer.Has(AppPermissions.EditProjectSchedule) && !ProjectRules.IsFinished(project.Stage)
            };

            if (dto.ApprovedScheduleId is int approvedId)
            {
                var approved = await ctx.Schedules.GetByIdAsync(approvedId);
                if (approved != null)
                {
                    var today = Today;
                    dto.ActualProgress = ScheduleRules.ActualProgress(approved.Tasks);
                    dto.PlannedProgress = ScheduleRules.PlannedProgress(approved.Tasks, today);
                    dto.BaselineProgress = ScheduleRules.PlannedProgress(approved.Tasks, today, useOriginal: true);
                    dto.OverdueCandidates = approved.Tasks.Count(t => ScheduleRules.IsOverdueCandidate(t, today));
                }
            }
            return dto;
        }

        private static ScheduleVersionResponseDto ToVersionDto(ScheduleVersionRow row) => new()
        {
            Id = row.Schedule.Id, VersionNumber = row.Schedule.VersionNumber, Title = row.Schedule.Title, Note = row.Schedule.Note,
            Status = (int)row.Schedule.Status, StatusName = ScheduleRules.StatusAr(row.Schedule.Status),
            CreatedByName = row.Schedule.CreatedByUser?.FullName ?? string.Empty, CreatedAt = row.Schedule.CreatedAt,
            ApprovedByName = row.Schedule.ApprovedByUser?.FullName, ApprovedAt = row.Schedule.ApprovedAt,
            TaskCount = row.TaskCount, ActualProgress = row.ActualProgress
        };
    }
}
