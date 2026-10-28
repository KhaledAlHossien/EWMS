using Application.Common;
using Application.DTOs.Response;
using Application.Features.MapLayers;
using Application.Interfaces;
using Domain.Entities;
using MediatR;
using static Application.Features.Projects.ProjectDocumentSupport;

namespace Application.Features.Projects
{
    public record GetProjectDocumentQuery(int Id) : IRequest<ProjectDocumentResponseDto>;


    public class GetProjectDocumentHandler(ProjectDocumentContext ctx) : IRequestHandler<GetProjectDocumentQuery, ProjectDocumentResponseDto>
    {
        public async Task<ProjectDocumentResponseDto> Handle(GetProjectDocumentQuery request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            return ToDto(await ctx.LoadVisibleAsync(viewer, request.Id), viewer, withVersions: true);
        }
    }
}
