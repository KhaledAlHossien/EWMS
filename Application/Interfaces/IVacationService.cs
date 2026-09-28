using Domain.Entities;
using Domain.Enums;

namespace Application.Interfaces
{
    public interface IVacationService
    {
        // ==================== الاستعلامات الأساسية ====================
        Task<Vacation?> GetByIdAsync(int id);
        Task<Vacation?> GetWithDetailsAsync(int id);
        Task<List<Vacation>> GetAllAsync();

        // ==================== استعلامات المستخدم ====================
        Task<List<Vacation>> GetByUserIdAsync(int userId);

        // ==================== استعلامات النطاق (كل الحالات) ====================
        Task<List<Vacation>> GetByDepartmentIdAsync(int departmentId);
        Task<List<Vacation>> GetByBranchIdAsync(int branchId);
        Task<List<Vacation>> GetByOfficeIdAsync(int officeId); // إجازات موظفي المكتب (الإجازة لا تخزّن المكتب — عبر User.OfficeId)

        // ==================== استعلامات سير العمل ====================
        Task<List<Vacation>> GetByStatusAsync(
            VacationStatus status, int? departmentId, int? branchId);

        Task<List<Vacation>> GetPendingForManagerAsync(int departmentId);
        Task<List<Vacation>> GetPendingForBranchManagerAsync(int branchId);  // ⬅️
        Task<List<Vacation>> GetAllPendingAsync();

        // ==================== CRUD ====================
        Task AddAsync(Vacation vacation);
        Task UpdateAsync(Vacation vacation);
        Task<bool> DeleteAsync(Vacation vacation);
        Task<bool> SaveChangesAsync();

        // ==================== فحوصات ====================
        Task<bool> VacationTypeExistsAsync(int vacationTypeId);
        Task<bool> HasOverlappingVacationAsync(
            int userId, DateTime start, DateTime end, int? excludeId = null);

        Task<int> GetVacationDaysInMonthAsync(int userId, int year, int month);
        Task<int> GetPaidVacationDaysInMonthAsync(int userId, int year, int month);
        Task<int> GetTotalPaidDaysInYearAsync(int userId, int year);
        Task<int> GetTotalUnpaidDaysInYearAsync(int userId, int year);
    }
}