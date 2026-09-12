using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Projects.Queries.GetById
{
    public record GetProjectByIdQuery(int Id) : IRequest<ProjectResponseDto>;
}
