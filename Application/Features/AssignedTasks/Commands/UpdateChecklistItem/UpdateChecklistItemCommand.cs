using Application.DTOs.Response;
using MediatR;

namespace Application.Features.AssignedTasks.Commands.UpdateChecklistItem
{
    public record UpdateChecklistItemCommand(int ItemId, bool? IsDone, string? Text) : IRequest<AssignedTaskDetailDto>;
}
