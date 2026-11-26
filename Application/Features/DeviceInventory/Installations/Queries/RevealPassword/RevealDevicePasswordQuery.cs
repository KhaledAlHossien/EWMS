using Application.DTOs.Response;
using MediatR;

namespace Application.Features.DeviceInventory.Installations.Queries.RevealPassword
{
    /// <summary>Copy = نسخ (وإلا إظهار) — يُسجَّل كل منهما</summary>
    public record RevealDevicePasswordQuery(int Id, bool Copy) : IRequest<DevicePasswordDto>;
}
