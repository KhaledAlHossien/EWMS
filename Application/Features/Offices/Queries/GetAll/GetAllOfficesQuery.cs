using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Offices.Queries.GetAll
{
    public record GetAllOfficesQuery : IRequest<List<OfficeResponseDto>>;
}
