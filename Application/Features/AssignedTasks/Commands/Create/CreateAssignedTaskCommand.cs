using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.AssignedTasks.Commands.Create
{
    public record CreateAssignedTaskCommand(CreateAssignedTaskRequestDto Dto) : IRequest<AssignedTaskDetailDto>;
}
