using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Users.Commands.Update
{
    public record UpdateUserCommand(int Id, UpdateUserRequestDto UserDto) : IRequest<UserResponseDto>;
}
