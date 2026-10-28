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
    public record PreviewScheduleExtensionCommand(int ProjectId, ScheduleExtensionRequestDto Dto) : IRequest<ScheduleExtensionPreviewResponseDto>;

    public class PreviewScheduleExtensionCommandValidator : AbstractValidator<PreviewScheduleExtensionCommand>
    {
        public PreviewScheduleExtensionCommandValidator() => RuleFor(x => x.Dto).SetValidator(new ScheduleExtensionRequestDtoValidator());
    }

    public class PreviewScheduleExtensionHandler(ScheduleChangeContext ctx) : IRequestHandler<PreviewScheduleExtensionCommand, ScheduleExtensionPreviewResponseDto>
    {
        public async Task<ScheduleExtensionPreviewResponseDto> Handle(PreviewScheduleExtensionCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var project = await ctx.LoadProjectAsync(viewer, request.ProjectId);
            ProjectRules.EnsureOpen(project);
            var (schedule, byId, preview) = await ctx.BuildPreviewAsync(project, request.Dto);

            var edges = (await ctx.Schedules.GetDependenciesAsync(schedule.Id)).Select(d => new ScheduleRules.Edge(d.PredecessorId, d.SuccessorId)).ToList();
            return new ScheduleExtensionPreviewResponseDto
            {
                Items = preview.Select(p => ToItemDto(p, byId)).ToList(),
                Conflicts = ToConflicts(ScheduleRules.PreviewConflicts(preview, byId, edges), byId),
                SuggestedTasks = ToSuggested(ScheduleRules.SuggestedSuccessors(preview.Select(p => p.TaskId), edges, byId), byId)
            };
        }

        private static ScheduleExtensionItemResponseDto ToItemDto(ScheduleRules.ExtensionPreviewItem p, IReadOnlyDictionary<int, ScheduleTask> byId) => new()
        {
            TaskId = p.TaskId, TaskCode = byId[p.TaskId].Code, TaskName = p.TaskName, Days = p.Days, MovesStart = p.MovesStart,
            OldStart = p.OldStart, OldEnd = p.OldEnd, NewStart = p.NewStart, NewEnd = p.NewEnd
        };
    }
}
