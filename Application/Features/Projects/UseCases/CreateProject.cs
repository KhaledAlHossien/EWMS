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
    public record CreateProjectCommand(ProjectRequestDto Dto) : IRequest<ProjectResponseDto>;

    public class CreateProjectCommandValidator : AbstractValidator<CreateProjectCommand>
    {
        public CreateProjectCommandValidator() => RuleFor(x => x.Dto).SetValidator(new ProjectRequestDtoValidator());
    }

    public class CreateProjectHandler(ProjectContext ctx) : IRequestHandler<CreateProjectCommand, ProjectResponseDto>
    {
        public async Task<ProjectResponseDto> Handle(CreateProjectCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var dto = request.Dto;

            // الفرع: فرع المنشئ، ولنظرة المؤسسة أي فرع يختاره
            var branchId = viewer.IsOrganizationWide ? dto.BranchId ?? viewer.User.BranchId : viewer.User.BranchId;
            if (branchId is not int branch)
                throw new InvalidOperationException(viewer.IsOrganizationWide ? "اختر فرع المشروع" : "حسابك غير مرتبط بفرع، فلا تستطيع إنشاء مشروع");
            ProjectRules.EnsureCanCreateIn(viewer, branch);

            var departmentId = dto.DepartmentId ?? (OrganizationRole.InBranch(viewer.User, branch) ? viewer.User.DepartmentId : null);
            var departmentName = await ctx.ResolveDepartmentAsync(departmentId, branch);
            var siteName = await ctx.ResolveSiteAsync(dto.SiteId);
            var reference = ProjectRules.CleanReference(dto.ReferenceNumber);
            if (reference != null && await ctx.Projects.ReferenceExistsAsync(branch, reference))
                throw new InvalidOperationException("يوجد مشروع بالرقم المرجعي نفسه في هذا الفرع");

            var now = DateTime.UtcNow;
            var project = new Project
            {
                Name = dto.Name.Trim(),
                ReferenceNumber = reference,
                Beneficiary = (dto.Beneficiary ?? string.Empty).Trim(),
                Description = (dto.Description ?? string.Empty).Trim(),
                BranchId = branch,
                DepartmentId = departmentId,
                SiteId = dto.SiteId,
                Stage = ProjectStage.Study,
                CreatedByUserId = viewer.Id,
                CreatedAt = now,
                UpdatedAt = now,
                // المنشئ مشارك فيه من البداية فلا يفقد رؤيته إن غيّر قسمه لاحقاً
                Members = [new ProjectMember { UserId = viewer.Id, Title = "منشئ المشروع", AddedAt = now }]
            };

            var details = new List<string> { $"المرحلة: {ProjectRules.StageAr(project.Stage)}" };
            if (departmentName != null) details.Add($"القسم: {departmentName}");
            if (siteName != null) details.Add($"الموقع: {siteName}");
            if (reference != null) details.Add($"الرقم المرجعي: {reference}");

            await ctx.Projects.AddAsync(project, new AuditEntry(viewer.Id, AuditActions.Created, AuditEntities.Project, null,
                $"إنشاء المشروع «{project.Name}»", string.Join("\n", details)));

            return ToDto(await ctx.LoadVisibleAsync(viewer, project.Id), viewer, withMembers: true);
        }
    }
}
