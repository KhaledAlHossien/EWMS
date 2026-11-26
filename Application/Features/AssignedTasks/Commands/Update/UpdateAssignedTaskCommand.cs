using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.AssignedTasks.Commands.Update
{
    public record UpdateAssignedTaskCommand(int Id, UpdateAssignedTaskRequestDto Dto) : IRequest<AssignedTaskDetailDto>;
}
