using Application.DTOs.Response;
using MediatR;

namespace Application.Features.AssignedTasks.Commands.RemoveLink
{
    public record RemoveTaskLinkCommand(int LinkId) : IRequest<List<TaskLinkDto>>;
}
