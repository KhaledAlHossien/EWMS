using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using MediatR;

namespace Application.Features.Projects
{
    /// <summary>دوال ساكنة مشتركة بين معالجات المشاريع: تحويل إلى DTO وتنسيق وحراسات صرفة.</summary>
    public static class ProjectSupport
    {
        public static ProjectResponseDto ToDto(Project p, Viewer viewer, bool withMembers) => new()
        {
            Id = p.Id,
            Number = ProjectRules.Number(p.Id, p.CreatedAt),
            ReferenceNumber = p.ReferenceNumber,
            Name = p.Name,
            Beneficiary = p.Beneficiary,
            Description = p.Description,
            BranchId = p.BranchId,
            BranchName = p.Branch?.Name ?? string.Empty,
            DepartmentId = p.DepartmentId,
            DepartmentName = p.Department?.Name,
            SiteId = p.SiteId,
            SiteName = p.Site?.Name,
            Stage = (int)p.Stage,
            StageName = ProjectRules.StageAr(p.Stage),
            IsFinished = ProjectRules.IsFinished(p.Stage),
            CreatedByUserId = p.CreatedByUserId,
            CreatedByName = p.CreatedByUser?.FullName ?? string.Empty,
            CreatedAt = p.CreatedAt,
            UpdatedAt = p.UpdatedAt,
            RowVersion = p.RowVersion,
            MembersCount = p.Members.Count,
            CanEdit = viewer.Has(AppPermissions.EditProject),
            Members = withMembers
                ? p.Members.OrderBy(m => m.AddedAt).Select(m => new ProjectMemberResponseDto
                {
                    UserId = m.UserId, FullName = m.User?.FullName ?? string.Empty, DepartmentName = m.User?.Department?.Name,
                    Title = m.Title, AddedAt = m.AddedAt
                }).ToList()
                : null
        };
    }
}
