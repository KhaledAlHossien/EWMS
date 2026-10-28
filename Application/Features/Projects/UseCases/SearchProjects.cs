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
    public record SearchProjectsQuery(string? Search, int? BranchId, int? DepartmentId, int? SiteId, int? Stage, int Page, int PageSize)
        : IRequest<PagedResultDto<ProjectResponseDto>>;


    public class SearchProjectsHandler(ProjectContext ctx) : IRequestHandler<SearchProjectsQuery, PagedResultDto<ProjectResponseDto>>
    {
        public async Task<PagedResultDto<ProjectResponseDto>> Handle(SearchProjectsQuery request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var page = Math.Max(1, request.Page);
            var pageSize = Math.Clamp(request.PageSize, 1, 100);
            ProjectStage? stage = request.Stage is int s && ProjectRules.IsValidStage(s) ? (ProjectStage)s : null;

            var (items, total) = await ctx.Projects.SearchAsync(ProjectRules.Visible(viewer),
                new ProjectFilter(request.Search, request.BranchId, request.DepartmentId, request.SiteId, stage), page, pageSize);

            return new PagedResultDto<ProjectResponseDto>
            {
                Items = items.Select(p => ToDto(p, viewer, withMembers: false)).ToList(),
                TotalCount = total, Page = page, PageSize = pageSize
            };
        }
    }
}
