using Application.DTOs.Response;
using MediatR;

namespace Application.Features.DeviceInventory.Installations.Queries.GetById
{
    public record GetInstallationByIdQuery(int Id) : IRequest<DeviceSiteResponseDto>;
}
