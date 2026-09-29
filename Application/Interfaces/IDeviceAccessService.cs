namespace Application.Interfaces
{
    /// <summary>
    /// صلاحية الوصول لتوثيق الأجهزة (مناطق / مواقع / أجهزة / تركيبات) — قرار المستخدم 2026-09-28:
    /// الميزة وظيفة قسم العمليات في الفرع التقني (القسم المالك من الإعداد DeviceInventory:OwnerDepartmentId).
    /// - المشاهدة: كل موظفي القسم المالك، أو من يملك صلاحية ViewDevices.
    /// - الإدارة: رئيس القسم المالك (دور Manager)، أو من يملك صلاحية ManageDevices (SuperAdmin افتراضياً).
    /// </summary>
    public interface IDeviceAccessService
    {
        Task<DeviceAccess> GetCurrentAsync();
    }

    /// <param name="InOwnerDepartment">المستخدم من قسم العمليات نفسه — الواجهة تُظهر قسم توثيق الأجهزة في لوحته فقط عندها</param>
    public record DeviceAccess(bool CanView, bool CanManage, bool InOwnerDepartment);
}
