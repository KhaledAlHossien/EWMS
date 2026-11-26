using Application.DTOs.Response;
using MediatR;

namespace Application.Features.AssignedTasks.Commands.DeleteChecklistItem
{
    public record DeleteChecklistItemCommand(int ItemId) : IRequest<AssignedTaskDetailDto>;
}
