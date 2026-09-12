using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Roles.Commands.Create
{
    public record CreateRoleCommand(CreateRoleRequestDto RoleDto) : IRequest<RoleResponseDto>;
}
