using Application.DTOs.Request;
using Domain.Entities;
using static Application.Features.DeviceInventory.DeviceInventoryRules;

namespace Application.Features.DeviceInventory.Devices
{
    /// <summary>الدوال المشتركة بين عمليات هذه الميزة</summary>
    public static class DeviceSupport
    {
        public static void Apply(DeviceRequestDto dto, Device device)
        {
            device.Name = Clean(dto.Name);
            device.Model = Clean(dto.Model);
            device.Description = Clean(dto.Description);
            device.Category = Clean(dto.Category);
            device.Manufacturer = Clean(dto.Manufacturer);
        }
    }
}
