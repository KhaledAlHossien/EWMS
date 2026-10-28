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
    public record UpdateProjectCommand(int Id, ProjectRequestDto Dto) : IRequest<ProjectResponseDto>;

    public class UpdateProjectCommandValidator : AbstractValidator<UpdateProjectCommand>
    {
        public UpdateProjectCommandValidator() => RuleFor(x => x.Dto).SetValidator(new ProjectRequestDtoValidator());
    }

    public class UpdateProjectHandler(ProjectContext ctx) : IRequestHandler<UpdateProjectCommand, ProjectResponseDto>
    {
        public async Task<ProjectResponseDto> Handle(UpdateProjectCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var project = await ctx.LoadVisibleAsync(viewer, request.Id);
            var dto = request.Dto;

            var oldStage = project.Stage;
            var newStage = dto.Stage is int s ? (ProjectStage)s : oldStage;
            // مغلق أو ملغى يبقى كما هو: إعادة الفتح بتغيير المرحلة إلى غير نهائية أولاً، ثم يُعدَّل
            if (ProjectRules.IsFinished(oldStage) && ProjectRules.IsFinished(newStage))
                ProjectRules.EnsureOpen(project);

            var departmentName = await ctx.ResolveDepartmentAsync(dto.DepartmentId, project.BranchId);
            var siteName = await ctx.ResolveSiteAsync(dto.SiteId);
            var reference = ProjectRules.CleanReference(dto.ReferenceNumber);
            if (reference != null && await ctx.Projects.ReferenceExistsAsync(project.BranchId, reference, project.Id))
                throw new InvalidOperationException("يوجد مشروع بالرقم المرجعي نفسه في هذا الفرع");

            var changes = new List<string>();
            void Track(string label, string? from, string? to)
            {
                if (!string.Equals(from ?? string.Empty, to ?? string.Empty, StringComparison.Ordinal))
                    changes.Add($"{label}: {(string.IsNullOrEmpty(from) ? "—" : from)} ← {(string.IsNullOrEmpty(to) ? "—" : to)}");
            }

            var name = dto.Name.Trim();
            var beneficiary = (dto.Beneficiary ?? string.Empty).Trim();
            var description = (dto.Description ?? string.Empty).Trim();
            Track("الاسم", project.Name, name);
            Track("الرقم المرجعي", project.ReferenceNumber, reference);
            Track("الجهة المستفيدة", project.Beneficiary, beneficiary);
            if (project.Description != description) changes.Add("الوصف: تغيّر");
            Track("قسم المعالجة", project.Department?.Name, departmentName);
            Track("الموقع", project.Site?.Name, siteName);
            Track("المرحلة", ProjectRules.StageAr(oldStage), ProjectRules.StageAr(newStage));

            if (changes.Count == 0) return ToDto(project, viewer, withMembers: true);

            project.Name = name;
            project.ReferenceNumber = reference;
            project.Beneficiary = beneficiary;
            project.Description = description;
            project.DepartmentId = dto.DepartmentId;
            project.Department = null;
            project.SiteId = dto.SiteId;
            project.Site = null;
            project.Stage = newStage;

            var action = oldStage != newStage ? AuditActions.Stage : AuditActions.Updated;
            await ctx.Projects.UpdateAsync(project, dto.RowVersion, new AuditEntry(viewer.Id, action, AuditEntities.Project, project.Id,
                $"{(action == AuditActions.Stage ? "تغيير مرحلة" : "تعديل")} المشروع «{project.Name}»", string.Join("\n", changes)));

            return ToDto(await ctx.LoadVisibleAsync(viewer, project.Id), viewer, withMembers: true);
        }
    }
}
