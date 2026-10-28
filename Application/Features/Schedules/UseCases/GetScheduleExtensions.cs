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
    public record GetScheduleExtensionsQuery(int ProjectId) : IRequest<List<ScheduleExtensionResponseDto>>;


    public class GetScheduleExtensionsHandler(ScheduleChangeContext ctx) : IRequestHandler<GetScheduleExtensionsQuery, List<ScheduleExtensionResponseDto>>
    {
        public async Task<List<ScheduleExtensionResponseDto>> Handle(GetScheduleExtensionsQuery request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var project = await ctx.LoadProjectAsync(viewer, request.ProjectId);
            var extensions = await ctx.Changes.GetExtensionsAsync(project.Id, take: 100);
            return extensions.Select(x => ToExtensionDto(x, project, viewer, null, [], [])).ToList();
        }
    }
}
