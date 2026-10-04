using Application.DTOs.Request;
using Domain.Entities.Maintenance;

namespace Application.Interfaces
{
    public interface IDeviceMaintenanceService
    {
        /// <summary>مع نوع الجهاز والشركة المصنعة</summary>
        Task<DeviceMaintenance?> GetByIdAsync(int id);

        /// <summary>مطابقة تامة للرقم التسلسلي (بلا فراغات زائدة)</summary>
        Task<DeviceMaintenance?> GetBySerialAsync(string serialNumber);

        /// <summary>البحث بالفلاتر، مرتّبة بالرقم التسلسلي</summary>
        Task<(List<DeviceMaintenance> Items, int TotalCount)> SearchAsync(DeviceMaintenanceFilterDto filter, int page, int pageSize);

        Task<DeviceMaintenance> AddAsync(DeviceMaintenance device);
        Task<bool> UpdateAsync(DeviceMaintenance device);
        Task<bool> DeleteAsync(DeviceMaintenance device);

        Task<bool> ExistsAsync(int id);
        Task<bool> ExistsBySerialAsync(string serialNumber, int? excludeId = null);

        /// <summary>حراسة الحذف: للجهاز طلبات صيانة (العلاقة Restrict)</summary>
        Task<bool> HasRequestsAsync(int id);
    }
}
