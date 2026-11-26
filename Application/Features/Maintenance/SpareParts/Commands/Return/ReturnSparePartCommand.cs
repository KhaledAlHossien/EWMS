using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Maintenance.SpareParts.Commands.Return
{
    public record ReturnSparePartCommand(int RequestPartId) : IRequest<RequestPartsDto>;
}
