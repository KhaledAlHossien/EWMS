using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Users.Queries.GetById
{
    public record GetUserByIdQuery(int Id) : IRequest<UserResponseDto>;
}
