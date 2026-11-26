using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Vacations.Queries.GetById
{
    public class GetVacationByIdQuery : IRequest<VacationResponseDto>
    {
        public int Id { get; set; }
        public GetVacationByIdQuery(int id) => Id = id;
    }
}
