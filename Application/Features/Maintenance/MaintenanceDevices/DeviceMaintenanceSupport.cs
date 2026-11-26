using Application.DTOs.Request;
using Application.Interfaces;
using Domain.Entities.Maintenance;

namespace Application.Features.Maintenance.MaintenanceDevices
{
    /// <summary>الدوال المشتركة بين عمليات هذه الميزة</summary>
    public class DeviceMaintenanceSupport
    {
        private readonly IDeviceTypeService _deviceTypeService;
        private readonly IDeviceCompanyService _companyService;

        public DeviceMaintenanceSupport(IDeviceTypeService deviceTypeService, IDeviceCompanyService companyService)
        {
            _deviceTypeService = deviceTypeService;
            _companyService = companyService;
        }

        public async Task EnsureReferencesAsync(DeviceMaintenanceRequestDto dto)
        {
            if (!await _deviceTypeService.ExistsAsync(dto.DeviceTypeId))
                throw new KeyNotFoundException("نوع الجهاز المحدد غير موجود");
            if (!await _companyService.ExistsAsync(dto.DeviceCompanyId))
                throw new KeyNotFoundException("الشركة المصنعة المحددة غير موجودة");
        }

        // الرقم التسلسلي والموديل يُبحث عنهما بـ"يبدأ بـ" فيُحفظان بلا فراغات زائدة
        public static void Trim(DeviceMaintenance d)
        {
            d.Name = d.Name.Trim();
            d.SerialNumber = d.SerialNumber.Trim();
            d.Model = d.Model.Trim();
            d.Description = d.Description.Trim();
        }
    }
}
