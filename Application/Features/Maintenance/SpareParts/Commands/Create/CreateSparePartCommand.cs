using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Maintenance.SpareParts.Commands.Create
{
    public record CreateSparePartCommand(SparePartRequestDto Dto) : IRequest<SparePartResponseDto>;
}
