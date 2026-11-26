using Application.DTOs.Response;
using Domain.Entities;
using MediatR;

namespace Application.Features.DeviceInventory.Queries.GetHistory
{
    public record GetDeviceInventoryHistoryQuery(DeviceInventoryEntity Type, int Id, int Page, int PageSize) : IRequest<PagedResultDto<DeviceInventoryLogDto>>;
}
