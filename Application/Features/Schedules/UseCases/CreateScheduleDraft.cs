using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.Projects;
using Application.Interfaces;
using Domain.Entities;
using FluentValidation;
using MediatR;
using static Application.Features.Schedules.ScheduleSupport;

namespace Application.Features.Schedules
{
    public record CreateScheduleDraftCommand(int ProjectId, ScheduleDraftRequestDto Dto) : IRequest<ScheduleDetailResponseDto>;

    public class CreateScheduleDraftCommandValidator : AbstractValidator<CreateScheduleDraftCommand>
    {
        public CreateScheduleDraftCommandValidator()
        {
            RuleFor(x => x.Dto.Title).MaximumLength(200).WithMessage("عنوان الجدول لا يتجاوز 200 حرف");
            RuleFor(x => x.Dto.Note).MaximumLength(1000).WithMessage("الملاحظة لا تتجاوز 1000 حرف");
        }
    }

    public class CreateScheduleDraftHandler(ScheduleContext ctx) : IRequestHandler<CreateScheduleDraftCommand, ScheduleDetailResponseDto>
    {
        public async Task<ScheduleDetailResponseDto> Handle(CreateScheduleDraftCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var project = await ctx.LoadProjectAsync(viewer, request.ProjectId);
            ProjectRules.EnsureOpen(project);

            var versions = await ctx.Schedules.GetVersionsAsync(project.Id);
            if (versions.Any(v => v.Schedule.Status == ScheduleStatus.Draft))
                throw new InvalidOperationException("توجد مسودة جدول بالفعل: أكملها واعتمدها أو احذفها قبل إنشاء غيرها");
            if (await ctx.Schedules.HasPendingExtensionAsync(project.Id))
                throw new InvalidOperationException("يوجد طلب تمديد لم يُبتّ فيه: اعتمده أو ارفضه قبل إنشاء إصدار جديد من الجدول");

            // إعادة الإصدار تنسخ بنود المعتمد بتواريخه الأصلية فلا يضيع خط الأساس
            var approved = await ctx.Schedules.GetApprovedAsync(project.Id);
            List<(int OldId, ScheduleTask Copy)> copies = approved == null ? new() : ScheduleRules.CloneForRevision(approved.Tasks);
            List<ScheduleRules.Edge> edges = approved == null ? new() : (await ctx.Schedules.GetDependenciesAsync(approved.Id))
                .Select(d => new ScheduleRules.Edge(d.PredecessorId, d.SuccessorId)).ToList();

            var draft = new ProjectSchedule
            {
                ProjectId = project.Id,
                Title = (request.Dto.Title ?? string.Empty).Trim(),
                Note = (request.Dto.Note ?? string.Empty).Trim(),
                Status = ScheduleStatus.Draft,
                CreatedByUserId = viewer.Id,
                CreatedAt = DateTime.UtcNow
            };

            var source = approved == null ? "جدول فارغ" : $"نسخة من الإصدار {approved.VersionNumber} ({copies.Count} بنداً)";
            var created = await ctx.Schedules.CreateDraftAsync(draft, copies, edges, new AuditEntry(viewer.Id, AuditActions.Created,
                AuditEntities.Schedule, null, $"إنشاء مسودة جدول للمشروع «{project.Name}»", source), project.BranchId);

            return await ctx.DetailAsync(viewer, await ctx.LoadScheduleAsync(viewer, created.Id));
        }
    }
}
