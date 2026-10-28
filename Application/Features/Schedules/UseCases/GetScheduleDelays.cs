using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.Projects;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using MediatR;
using static Application.Features.Schedules.ScheduleChangeSupport;

namespace Application.Features.Schedules
{
    public record GetScheduleDelaysQuery(int ProjectId) : IRequest<List<ScheduleDelayResponseDto>>;


    public class GetScheduleDelaysHandler(ScheduleChangeContext ctx) : IRequestHandler<GetScheduleDelaysQuery, List<ScheduleDelayResponseDto>>
    {
        public async Task<List<ScheduleDelayResponseDto>> Handle(GetScheduleDelaysQuery request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var project = await ctx.LoadProjectAsync(viewer, request.ProjectId);
            var delays = await ctx.Changes.GetDelaysAsync(project.Id, take: 300);
            return delays.Select(d => ToDelayDto(d, project, viewer, suggested: null)).ToList();
        }
    }
}
