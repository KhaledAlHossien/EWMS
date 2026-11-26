using Application.DTOs.Request;
using MediatR;

namespace Application.Features.DeviceInventory.Installations.Queries.Export
{
    public record ExportInstallationsQuery(InstallationFilterDto Filter) : IRequest<byte[]>;
}
