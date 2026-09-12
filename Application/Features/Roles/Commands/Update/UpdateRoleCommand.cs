using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Roles.Commands.Update
{
    public record UpdateRoleCommand(int Id, UpdateRoleRequestDto RoleDto) : IRequest<RoleResponseDto>;
}
