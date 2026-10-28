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
    public record GetScheduleProgressHistoryQuery(int TaskId) : IRequest<List<ScheduleProgressEntryResponseDto>>;


    public class GetScheduleProgressHistoryHandler(ScheduleContext ctx) : IRequestHandler<GetScheduleProgressHistoryQuery, List<ScheduleProgressEntryResponseDto>>
    {
        public async Task<List<ScheduleProgressEntryResponseDto>> Handle(GetScheduleProgressHistoryQuery request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            await ctx.LoadTaskAsync(viewer, request.TaskId);
            var entries = await ctx.Schedules.GetProgressHistoryAsync(request.TaskId, take: 100);
            return entries.Select(e => new ScheduleProgressEntryResponseDto
            {
                Id = e.Id, Progress = e.Progress, Status = (int)e.Status, StatusName = ScheduleRules.TaskStatusAr(e.Status),
                Note = e.Note, UserName = e.User?.FullName ?? string.Empty, CreatedAt = e.CreatedAt
            }).ToList();
        }
    }
}
