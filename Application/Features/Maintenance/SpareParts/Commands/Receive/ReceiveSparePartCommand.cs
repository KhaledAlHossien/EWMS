using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Maintenance.SpareParts.Commands.Receive
{
    public record ReceiveSparePartCommand(int Id, ReceiveSparePartDto Dto) : IRequest<SparePartResponseDto>;
}
