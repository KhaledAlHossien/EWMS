using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Maintenance.DamageTypes.Commands.Create
{
    public record CreateDamageTypeCommand(DamageTypeRequestDto Dto) : IRequest<DamageTypeResponseDto>;
}
