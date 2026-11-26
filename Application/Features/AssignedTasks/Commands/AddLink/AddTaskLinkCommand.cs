using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.AssignedTasks.Commands.AddLink
{
    public record AddTaskLinkCommand(int TaskId, AddTaskLinkRequestDto Dto) : IRequest<List<TaskLinkDto>>;
}
