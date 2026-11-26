using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Maintenance.DamageTypes.Queries.GetAll
{
    public record GetAllDamageTypesQuery : IRequest<List<DamageTypeResponseDto>>;
}
