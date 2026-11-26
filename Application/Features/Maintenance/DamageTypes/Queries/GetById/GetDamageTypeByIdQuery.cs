using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Maintenance.DamageTypes.Queries.GetById
{
    public record GetDamageTypeByIdQuery(int Id) : IRequest<DamageTypeResponseDto>;
}
