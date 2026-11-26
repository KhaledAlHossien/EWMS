using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Maintenance.DamageTypes.Commands.Update
{
    public record UpdateDamageTypeCommand(int Id, DamageTypeRequestDto Dto) : IRequest<DamageTypeResponseDto>;
}
