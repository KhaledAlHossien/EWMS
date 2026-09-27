using Application.DTOs.Response;
using MediatR;

namespace Application.Features.VacationTypes.Queries.GetById
{
    public class GetVacationTypeByIdQuery : IRequest<VacationTypeResponseDto>
    {
        public int Id { get; set; }
        public GetVacationTypeByIdQuery(int id) => Id = id;
    }
}