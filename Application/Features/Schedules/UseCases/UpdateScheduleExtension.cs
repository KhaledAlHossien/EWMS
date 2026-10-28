using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.Projects;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using MediatR;
using static Application.Features.Schedules.ScheduleChangeSupport;

namespace Application.Features.Schedules
{
    public record UpdateScheduleExtensionCommand(int Id, ScheduleExtensionRequestDto Dto) : IRequest<ScheduleExtensionResponseDto>;

    public class UpdateScheduleExtensionCommandValidator : AbstractValidator<UpdateScheduleExtensionCommand>
    {
        public UpdateScheduleExtensionCommandValidator()
        {
            RuleFor(x => x.Dto).SetValidator(new ScheduleExtensionRequestDtoValidator());
            RuleFor(x => x.Dto.Reason).NotEmpty().WithMessage("سبب التمديد مطلوب");
        }
    }

    public class UpdateScheduleExtensionHandler(ScheduleChangeContext ctx) : IRequestHandler<UpdateScheduleExtensionCommand, ScheduleExtensionResponseDto>
    {
        public async Task<ScheduleExtensionResponseDto> Handle(UpdateScheduleExtensionCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var extension = await ctx.LoadExtensionAsync(viewer, request.Id);
            var project = extension.Project;
            ProjectRules.EnsureOpen(project);
            EnsureRequesterDraft(viewer, extension);
            var dto = request.Dto;

            var (schedule, _, preview) = await ctx.BuildPreviewAsync(project, dto);
            var delay = await ctx.ResolveDelayAsync(project, schedule, dto.DelayId);
            var versions = await ctx.ResolveDocumentsAsync(project, dto.DocumentVersionIds);

            extension.DelayId = delay?.Id;
            extension.Mode = (ExtensionMode)dto.Mode;
            extension.DayCount = (ExtensionDayCount)dto.DayCount;
            extension.Reason = dto.Reason!.Trim();

            // البنود تُستبدل (لكل سطر معرّف خاص)، والمستندات بالفرق (مفتاحها مركّب)
            foreach (var old in extension.Items.ToList()) extension.Items.Remove(old);
            foreach (var item in preview.Select(ToItem)) extension.Items.Add(item);
            var keep = versions.Select(v => v.Id).ToHashSet();
            foreach (var old in extension.Documents.Where(d => !keep.Contains(d.DocumentVersionId)).ToList()) extension.Documents.Remove(old);
            var have = extension.Documents.Select(d => d.DocumentVersionId).ToHashSet();
            foreach (var id in keep.Where(id => !have.Contains(id)))
                extension.Documents.Add(new ScheduleExtensionDocument { ExtensionId = extension.Id, DocumentVersionId = id });

            await ctx.Changes.SaveExtensionAsync(extension, dto.RowVersion, new AuditEntry(viewer.Id, AuditActions.Updated, AuditEntities.Extension, extension.Id,
                $"تعديل طلب تمديد لجدول «{project.Name}»", ExtensionSummary(extension, preview)), project.BranchId);

            return await ctx.ExtensionDetailAsync(viewer, await ctx.LoadExtensionAsync(viewer, extension.Id));
        }
    }
}
