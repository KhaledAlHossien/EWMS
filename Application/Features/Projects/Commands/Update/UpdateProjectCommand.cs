using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Projects.Commands.Update
{
    public record UpdateProjectCommand(int Id, UpdateProjectRequestDto ProjectDto) : IRequest<ProjectResponseDto>;
}
