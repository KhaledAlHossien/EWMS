using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Maintenance.SpareParts.Commands.Update
{
    public record UpdateSparePartCommand(int Id, SparePartRequestDto Dto) : IRequest<SparePartResponseDto>;
}
