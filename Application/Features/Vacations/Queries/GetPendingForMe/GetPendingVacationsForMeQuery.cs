using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Vacations.Queries.GetPendingForMe
{
    public class GetPendingVacationsForMeQuery
        : IRequest<List<VacationResponseDto>>
    { }
}
