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
    public record RemoveProjectMemberCommand(int Id, int UserId) : IRequest<ProjectResponseDto>;


    public class RemoveProjectMemberHandler(ProjectContext ctx) : IRequestHandler<RemoveProjectMemberCommand, ProjectResponseDto>
    {
        public async Task<ProjectResponseDto> Handle(RemoveProjectMemberCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var project = await ctx.LoadVisibleAsync(viewer, request.Id);
            ProjectRules.EnsureOpen(project);

            var member = project.Members.FirstOrDefault(m => m.UserId == request.UserId)
                ?? throw new KeyNotFoundException("هذا الموظف ليس مشاركاً في المشروع");

            await ctx.Projects.RemoveMemberAsync(member, new AuditEntry(viewer.Id, AuditActions.MemberRemoved, AuditEntities.Project, project.Id,
                $"إزالة مشارك من «{project.Name}»", member.User?.FullName ?? string.Empty));

            // قد يفقد المستخدم رؤية المشروع بإزالة نفسه: يُعاد المشروع بما يراه، ولا خطأ إن خرج من حدّه
            var reloaded = await ctx.Projects.GetByIdAsync(project.Id) ?? project;
            if (!ProjectRules.CanView(viewer, reloaded))
                throw new InvalidOperationException("أُزيلت مشاركتك، ولم يعد المشروع ضمن صلاحياتك");
            return ToDto(reloaded, viewer, withMembers: true);
        }
    }
}
