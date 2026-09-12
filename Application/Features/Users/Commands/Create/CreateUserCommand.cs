using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Users.Commands.Create
{
    public record CreateUserCommand(CreateUserRequestDto UserDto) : IRequest<UserResponseDto>;
}
