using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Maintenance.SpareParts.Commands.Adjust
{
    public record AdjustSparePartCommand(int Id, AdjustSparePartDto Dto) : IRequest<SparePartResponseDto>;
}
