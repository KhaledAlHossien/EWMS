using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Roles.Queries.GetById
{
    public record GetRoleByIdQuery(int Id) : IRequest<RoleResponseDto>;
}
