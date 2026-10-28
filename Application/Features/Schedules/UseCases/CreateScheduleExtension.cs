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
    public record CreateScheduleExtensionCommand(int ProjectId, ScheduleExtensionRequestDto Dto) : IRequest<ScheduleExtensionResponseDto>;

    public class CreateScheduleExtensionCommandValidator : AbstractValidator<CreateScheduleExtensionCommand>
    {
        public CreateScheduleExtensionCommandValidator()
        {
            RuleFor(x => x.Dto).SetValidator(new ScheduleExtensionRequestDtoValidator());
            RuleFor(x => x.Dto.Reason).NotEmpty().WithMessage("سبب التمديد مطلوب");
        }
    }

    public class CreateScheduleExtensionHandler(ScheduleChangeContext ctx) : IRequestHandler<CreateScheduleExtensionCommand, ScheduleExtensionResponseDto>
    {
        public async Task<ScheduleExtensionResponseDto> Handle(CreateScheduleExtensionCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var project = await ctx.LoadProjectAsync(viewer, request.ProjectId);
            ProjectRules.EnsureOpen(project);
            if (await ctx.Schedules.HasPendingExtensionAsync(project.Id))
                throw new InvalidOperationException("يوجد طلب تمديد لم يُبتّ فيه لهذا المشروع: اعتمده أو ارفضه أو احذفه قبل إعداد طلب جديد");

            var dto = request.Dto;
            var (schedule, _, preview) = await ctx.BuildPreviewAsync(project, dto);
            var delay = await ctx.ResolveDelayAsync(project, schedule, dto.DelayId);
            var versions = await ctx.ResolveDocumentsAsync(project, dto.DocumentVersionIds);

            var extension = new ScheduleExtension
            {
                ProjectId = project.Id,
                ScheduleId = schedule.Id,
                DelayId = delay?.Id,
                Mode = (ExtensionMode)dto.Mode,
                DayCount = (ExtensionDayCount)dto.DayCount,
                Status = ExtensionStatus.Draft,
                Reason = dto.Reason!.Trim(),
                RequestedByUserId = viewer.Id,
                RequestedAt = DateTime.UtcNow,
                Items = preview.Select(ToItem).ToList(),
                Documents = versions.Select(v => new ScheduleExtensionDocument { DocumentVersionId = v.Id }).ToList()
            };

            await ctx.Changes.AddExtensionAsync(extension, new AuditEntry(viewer.Id, AuditActions.Created, AuditEntities.Extension, null,
                $"إعداد طلب تمديد لجدول «{project.Name}»", ExtensionSummary(extension, preview)), project.BranchId);

            return await ctx.ExtensionDetailAsync(viewer, await ctx.LoadExtensionAsync(viewer, extension.Id));
        }
    }
}
