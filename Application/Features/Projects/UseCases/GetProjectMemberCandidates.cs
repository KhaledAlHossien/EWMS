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
    public record GetProjectMemberCandidatesQuery(int Id, string? Search) : IRequest<List<ProjectMemberCandidateDto>>;


    public class GetProjectMemberCandidatesHandler(ProjectContext ctx) : IRequestHandler<GetProjectMemberCandidatesQuery, List<ProjectMemberCandidateDto>>
    {
        public async Task<List<ProjectMemberCandidateDto>> Handle(GetProjectMemberCandidatesQuery request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var project = await ctx.LoadVisibleAsync(viewer, request.Id);
            var candidates = await ctx.Projects.GetMemberCandidatesAsync(project.BranchId, request.Search,
                project.Members.Select(m => m.UserId).ToList(), take: 30);
            return candidates.Select(u => new ProjectMemberCandidateDto { Id = u.Id, FullName = u.FullName, DepartmentName = u.Department?.Name }).ToList();
        }
    }
}
