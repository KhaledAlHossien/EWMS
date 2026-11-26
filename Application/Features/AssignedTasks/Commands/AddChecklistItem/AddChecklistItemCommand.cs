using Application.DTOs.Response;
using MediatR;

namespace Application.Features.AssignedTasks.Commands.AddChecklistItem
{
    public record AddChecklistItemCommand(int TaskId, string Text) : IRequest<AssignedTaskDetailDto>;
}
