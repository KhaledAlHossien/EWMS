using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Vacations.Queries.GetByUser
{
    public class GetVacationsByUserQuery : IRequest<List<VacationResponseDto>>
    {
        public int UserId { get; set; }
        public GetVacationsByUserQuery(int userId) => UserId = userId;
    }
}