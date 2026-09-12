using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Permissions.Queries.GetAll
{
    public record GetAllPermissionsQuery : IRequest<List<PermissionResponseDto>>;
}
