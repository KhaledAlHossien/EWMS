using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Vacations.Queries.GetAll
{
    public class GetAllVacationsQuery : IRequest<List<VacationResponseDto>> { }
}
