using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Roles.Queries.GetAll
{
    public record GetAllRolesQuery : IRequest<List<RoleResponseDto>>;
}
