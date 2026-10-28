using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using MediatR;
using static Application.Features.Projects.ProjectSupport;

namespace Application.Features.Projects
{
    public record GetProjectQuery(int Id) : IRequest<ProjectResponseDto>;


    public class GetProjectHandler(ProjectContext ctx) : IRequestHandler<GetProjectQuery, ProjectResponseDto>
    {
        public async Task<ProjectResponseDto> Handle(GetProjectQuery request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            return ToDto(await ctx.LoadVisibleAsync(viewer, request.Id), viewer, withMembers: true);
        }
    }
}
