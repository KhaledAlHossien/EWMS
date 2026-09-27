using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Users.Queries.GetByOffice
{
    public record GetUsersByOfficeQuery(int OfficeId) : IRequest<List<UserResponseDto>>;
}
