using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Projects.Queries.GetAll
{
    public record GetAllProjectsQuery : IRequest<List<ProjectResponseDto>>;
}
