using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Offices.Queries.GetById
{
    public record GetOfficeByIdQuery(int Id) : IRequest<OfficeResponseDto>;
}
