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
    public record GetProjectAuditQuery(int Id, string? EntityType, int Page, int PageSize) : IRequest<PagedResultDto<AuditLogResponseDto>>;


    public class GetProjectAuditHandler(ProjectContext ctx) : IRequestHandler<GetProjectAuditQuery, PagedResultDto<AuditLogResponseDto>>
    {
        public async Task<PagedResultDto<AuditLogResponseDto>> Handle(GetProjectAuditQuery request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            await ctx.LoadVisibleAsync(viewer, request.Id);   // سجل مشروع لا يراه المستخدم لا يُقرأ
            var page = Math.Max(1, request.Page);
            var pageSize = Math.Clamp(request.PageSize, 1, 100);

            var (items, total) = await ctx.Audit.GetForProjectAsync(request.Id, request.EntityType, null, page, pageSize);
            return new PagedResultDto<AuditLogResponseDto>
            {
                Items = items.Select(a => new AuditLogResponseDto
                {
                    Id = a.Id, CreatedAt = a.CreatedAt, UserId = a.UserId, UserName = a.User?.FullName ?? string.Empty,
                    Action = a.Action, EntityType = a.EntityType, EntityId = a.EntityId, ProjectId = a.ProjectId,
                    Title = a.Title, Details = a.Details
                }).ToList(),
                TotalCount = total, Page = page, PageSize = pageSize
            };
        }
    }
}
