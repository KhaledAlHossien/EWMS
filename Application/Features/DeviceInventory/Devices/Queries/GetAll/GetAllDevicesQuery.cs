using Application.DTOs.Response;
using MediatR;

namespace Application.Features.DeviceInventory.Devices.Queries.GetAll
{
    // كتالوج الأجهزة: نوع/موديل قابل للتركيب عدة مرات — الاسم + الموديل فريدان معاً (مراجعة 2026-10-05)
    public record GetAllDevicesQuery : IRequest<List<DeviceResponseDto>>;
}
