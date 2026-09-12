using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Projects.Queries.GetMine
{
    public record GetMyProjectsQuery : IRequest<List<ProjectResponseDto>>;
}
