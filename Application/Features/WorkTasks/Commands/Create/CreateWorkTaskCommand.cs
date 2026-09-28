using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.WorkTasks.Commands.Create
{
    public record CreateWorkTaskCommand(CreateWorkTaskRequestDto TaskDto) : IRequest<WorkTaskResponseDto>;
}
