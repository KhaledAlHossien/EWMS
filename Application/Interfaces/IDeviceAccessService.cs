namespace Application.Interfaces
{
    /// <summary>
    /// صلاحية الوصول لتوثيق الأجهزة (مناطق / مواقع / أجهزة / تركيبات) — قرار المستخدم 2026-09-28:
    /// الميزة وظيفة قسم العمليات في الفرع التقني (القسم المالك من الإعداد DeviceInventory:OwnerDepartmentId).
    /// - المشاهدة: كل موظفي القسم المالك، أو من يملك صلاحية ViewDevices.
    /// - الإضافة / التعديل / الحذف: رئيس القسم المالك (دور Manager) يملكها كلها،
    ///   أو من يملك الصلاحية المقابلة CreateDevice / EditDevice / DeleteDevice (SuperAdmin افتراضياً).
    /// </summary>
    public interface IDeviceAccessService
    {
        Task<DeviceAccess> GetCurrentAsync();
    }

    public enum DeviceOperation
    {
        View,
        Create,
        Edit,
        Delete
    }

    /// <param name="InOwnerDepartment">المستخدم من قسم العمليات نفسه — الواجهة تُظهر قسم توثيق الأجهزة في لوحته فقط عندها</param>
    public record DeviceAccess(bool CanView, bool CanCreate, bool CanEdit, bool CanDelete, bool InOwnerDepartment)
    {
        /// <summary>يملك أي عملية تعديل — تعتمد عليه الواجهة الحالية لإظهار أدوات الإدارة</summary>
        public bool CanManage => CanCreate || CanEdit || CanDelete;

        public bool Can(DeviceOperation operation) => operation switch
        {
            DeviceOperation.View => CanView,
            DeviceOperation.Create => CanCreate,
            DeviceOperation.Edit => CanEdit,
            DeviceOperation.Delete => CanDelete,
            _ => false
        };
    }
}
