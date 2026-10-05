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

        Task<List<Vacation>> GetPendingInBranchAsync(VacationStatus status, int branchId);
        Task<List<Vacation>> GetAllPendingAsync();

        /// <summary>هل للمستخدم إجازات، أو قرارات موافقة/رفض على إجازات؟ (حماية حذف المستخدم)</summary>
        Task<bool> ExistsForUserAsync(int userId);

        // ==================== CRUD ====================
        Task AddAsync(Vacation vacation);
        Task UpdateAsync(Vacation vacation);
        Task<bool> DeleteAsync(Vacation vacation);
        Task<bool> SaveChangesAsync();

        // ==================== فحوصات ====================
        Task<bool> VacationTypeExistsAsync(int vacationTypeId);
        Task<bool> HasOverlappingVacationAsync(
            int userId, DateTime start, DateTime end, int? excludeId = null);

        /// <summary>الأيام المدفوعة المعتمدة للموظف في شهر (من أجزاء الإجازات المعتمدة)</summary>
        Task<int> GetPaidVacationDaysInMonthAsync(int userId, int year, int month);

        /// <summary>الأيام المدفوعة المعتمدة لكل شهر تمر به المدة — لتوزيع الدفع عند الاعتماد النهائي</summary>
        Task<Dictionary<(int Year, int Month), int>> GetApprovedPaidDaysByMonthAsync(
            int userId, DateTime start, DateTime end, int? excludeVacationId = null);

        /// <summary>مرفق مع إجازته ومحتواه (للعرض/التنزيل بعد التحقق من حق عرض الإجازة)</summary>
        Task<VacationAttachment?> GetAttachmentAsync(int attachmentId);

        // ==================== «سجل الموظف» قبل القرار ====================
        /// <summary>آخر إجازة معتمدة بدأت حتى تاريخ معيّن</summary>
        Task<Vacation?> GetLastTakenAsync(int userId, DateTime upTo);

        /// <summary>الإجازات المعتمدة التي تبدأ بعد تاريخ معيّن (الأقرب أولاً)</summary>
        Task<List<Vacation>> GetUpcomingApprovedAsync(int userId, DateTime after);

        /// <summary>الإجازات المعتمدة وأيام العمل فيها خلال شهر (من أجزاء الإجازات المعتمدة)</summary>
        Task<(int Count, int Days)> GetApprovedInMonthAsync(int userId, int year, int month);

        /// <summary>إجازات قسم (معتمدة أو معلّقة) تتداخل مع فترة، لغير موظف معيّن</summary>
        Task<List<Vacation>> GetOverlappingInDepartmentAsync(int departmentId, int excludeUserId, DateTime start, DateTime end);

        /// <summary>طلبات الموظف المعلّقة الأخرى</summary>
        Task<List<Vacation>> GetOtherPendingAsync(int userId, int excludeVacationId);

        /// <summary>ينفّذ العملية داخل معاملة بقفل حصري على الموظف (لا يُحسب الحد الشهري مرتين بالتوازي)</summary>
        Task<T> RunExclusiveForUserAsync<T>(int userId, Func<Task<T>> action);
    }
}