using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Projects.Commands.Create
{
    public record CreateProjectCommand(CreateProjectRequestDto ProjectDto) : IRequest<ProjectResponseDto>;
}
