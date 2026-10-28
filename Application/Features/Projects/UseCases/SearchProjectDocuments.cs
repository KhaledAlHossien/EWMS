using Application.Common;
using Application.DTOs.Response;
using Application.Features.MapLayers;
using Application.Interfaces;
using Domain.Entities;
using MediatR;
using static Application.Features.Projects.ProjectDocumentSupport;

namespace Application.Features.Projects
{
    public record SearchProjectDocumentsQuery(int? ProjectId, int? Kind, string? Search, DateTime? From, DateTime? To, int Page, int PageSize)
        : IRequest<PagedResultDto<ProjectDocumentResponseDto>>;


    public class SearchProjectDocumentsHandler(ProjectDocumentContext ctx) : IRequestHandler<SearchProjectDocumentsQuery, PagedResultDto<ProjectDocumentResponseDto>>
    {
        public async Task<PagedResultDto<ProjectDocumentResponseDto>> Handle(SearchProjectDocumentsQuery request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var page = Math.Max(1, request.Page);
            var pageSize = Math.Clamp(request.PageSize, 1, 100);
            ProjectDocumentKind? kind = request.Kind is int k && ProjectRules.IsValidKind(k) ? (ProjectDocumentKind)k : null;

            var (items, total) = await ctx.Documents.SearchAsync(ProjectRules.Visible(viewer),
                new DocumentFilter(request.ProjectId, kind, request.Search, request.From, request.To), page, pageSize);
            return new PagedResultDto<ProjectDocumentResponseDto>
            {
                Items = items.Select(d => ToDto(d, viewer, withVersions: false)).ToList(),
                TotalCount = total, Page = page, PageSize = pageSize
            };
        }
    }
}
