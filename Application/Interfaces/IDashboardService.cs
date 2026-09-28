using Application.DTOs.Response;

namespace Application.Interfaces
{
    public enum DashboardScope { All, Branch, Department, Office }

    /// <summary>
    /// إحصائيات لوحات المتابعة (استعلامات تجميعية مباشرة). لا يتحقق من الصلاحيات —
    /// التحقق من النطاق يتم في Features/Dashboard/DashboardQueries. يُرجع null إن لم يوجد الكيان.
    /// </summary>
    public interface IDashboardService
    {
        // لوحات عامة (الهيكل، الكادر، مهام العمل، النشاط)
        Task<OverviewDashboardDto> GetOverviewAsync();
        Task<BranchDashboardDto?> GetBranchAsync(int branchId);
        Task<DepartmentDashboardDto?> GetDepartmentAsync(int departmentId);
        Task<OfficeDashboardDto?> GetOfficeAsync(int officeId);
        Task<EmployeeDashboardDto?> GetEmployeeAsync(int userId);

        // صفحة إحصائيات الإجازات المستقلة
        Task<VacationStatsDto?> GetVacationStatsAsync(DashboardScope scope, int? id);
    }
}
