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
    public record AddProjectMemberCommand(int Id, AddProjectMemberRequestDto Dto) : IRequest<ProjectResponseDto>;

    public class AddProjectMemberCommandValidator : AbstractValidator<AddProjectMemberCommand>
    {
        public AddProjectMemberCommandValidator()
        {
            RuleFor(x => x.Dto.UserId).GreaterThan(0).WithMessage("اختر الموظف");
            RuleFor(x => x.Dto.Title).MaximumLength(100).WithMessage("الصفة لا تتجاوز 100 حرف");
        }
    }

    public class AddProjectMemberHandler(ProjectContext ctx) : IRequestHandler<AddProjectMemberCommand, ProjectResponseDto>
    {
        public async Task<ProjectResponseDto> Handle(AddProjectMemberCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var project = await ctx.LoadVisibleAsync(viewer, request.Id);
            ProjectRules.EnsureOpen(project);

            var user = await ctx.Users.GetByIdAsync(request.Dto.UserId) ?? throw new KeyNotFoundException("الموظف غير موجود");
            ProjectRules.EnsureCanBeMember(project, user);

            var member = new ProjectMember
            {
                ProjectId = project.Id, UserId = user.Id, Title = (request.Dto.Title ?? string.Empty).Trim(), AddedAt = DateTime.UtcNow
            };
            await ctx.Projects.AddMemberAsync(member, new AuditEntry(viewer.Id, AuditActions.MemberAdded, AuditEntities.Project, project.Id,
                $"إضافة مشارك إلى «{project.Name}»", $"{user.FullName}{(member.Title.Length > 0 ? $" — {member.Title}" : string.Empty)}"));

            if (user.Id != viewer.Id)
                await ctx.Notifications.AddAsync(new Notification
                {
                    UserId = user.Id,
                    Title = "أُضفت إلى مشروع",
                    Message = $"أضافك {viewer.User.FullName} مشاركاً في المشروع {ProjectRules.Number(project.Id, project.CreatedAt)} — {project.Name}",
                    Type = NotificationType.ProjectMemberAdded,
                    RelatedEntityType = AuditEntities.Project,
                    RelatedEntityId = project.Id,
                    CreatedAt = DateTime.UtcNow
                });

            return ToDto(await ctx.LoadVisibleAsync(viewer, project.Id), viewer, withMembers: true);
        }
    }
}
