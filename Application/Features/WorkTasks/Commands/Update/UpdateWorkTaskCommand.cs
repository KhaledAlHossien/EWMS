using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.WorkTasks.Commands.Update
{
    public record UpdateWorkTaskCommand(int Id, UpdateWorkTaskRequestDto TaskDto) : IRequest<WorkTaskResponseDto>;
}
