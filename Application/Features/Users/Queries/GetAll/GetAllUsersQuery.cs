using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Users.Queries.GetAll
{
    public record GetAllUsersQuery : IRequest<List<UserResponseDto>>;
}
