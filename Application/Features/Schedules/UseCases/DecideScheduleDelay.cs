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
    public record DecideScheduleDelayCommand(int Id, ScheduleDecisionRequestDto Dto) : IRequest<ScheduleDelayResponseDto>;

    public class DecideScheduleDelayCommandValidator : AbstractValidator<DecideScheduleDelayCommand>
    {
        public DecideScheduleDelayCommandValidator() =>
            RuleFor(x => x.Dto.Note).MaximumLength(1000).WithMessage("الملاحظة لا تتجاوز 1000 حرف");
    }

    public class DecideScheduleDelayHandler(ScheduleChangeContext ctx) : IRequestHandler<DecideScheduleDelayCommand, ScheduleDelayResponseDto>
    {
        public async Task<ScheduleDelayResponseDto> Handle(DecideScheduleDelayCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var delay = await ctx.LoadDelayAsync(viewer, request.Id);
            var project = delay.Project;
            ProjectRules.EnsureOpen(project);
            var dto = request.Dto;
            ScheduleRules.EnsureCanDecideJustification(viewer, delay, dto.Approve, dto.Note);

            delay.Status = dto.Approve ? DelayStatus.Approved : DelayStatus.Rejected;
            delay.DecidedByUserId = viewer.Id;
            delay.DecidedAt = DateTime.UtcNow;
            delay.DecisionNote = (dto.Note ?? string.Empty).Trim();

            await ctx.Changes.SaveDelayAsync(delay, dto.RowVersion, new AuditEntry(viewer.Id, dto.Approve ? AuditActions.Approved : AuditActions.Rejected,
                AuditEntities.Delay, delay.Id, $"{(dto.Approve ? "اعتماد" : "رفض")} تبرير تأخير «{delay.Task.Name}» في «{project.Name}»",
                delay.DecisionNote), project.BranchId);

            await ScheduleNotifier.SendAsync(ctx.Notifications, [delay.RecordedByUserId], viewer.Id, project, NotificationType.ScheduleJustificationDecided,
                dto.Approve ? "اعتُمد تبرير التأخير" : "رُفض تبرير التأخير",
                $"{(dto.Approve ? "اعتمد" : "رفض")} {viewer.User.FullName} تبرير تأخير البند «{delay.Task.Name}» في المشروع {ScheduleNotifier.Label(project)}"
                + (delay.DecisionNote.Length > 0 ? $" — {delay.DecisionNote}" : string.Empty));

            return await ctx.DelayDetailAsync(viewer, await ctx.LoadDelayAsync(viewer, delay.Id));
        }
    }
}
