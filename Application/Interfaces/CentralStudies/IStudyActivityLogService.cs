using Domain.Entities.CentralStudiesBranch;

namespace Application.Interfaces
{
    /// <summary>سجل الدراسة (إلحاقي)</summary>
    public interface IStudyActivityLogService
    {
        Task AddAsync(StudyActivityLog log);
        /// <summary>كل سجل الدراسة (الأحدث أولاً) مع المنفّذ</summary>
        Task<List<StudyActivityLog>> GetForRequestAsync(int requestId);
    }

    /// <summary>حراسة الحذف: هل يرتبط مستخدم/قسم/مكتب/فرع/موقع بسجلات فرع الدراسات (العلاقات Restrict)</summary>
    public interface IStudyReferenceService
    {
        Task<bool> ExistsForUserAsync(int userId);
        Task<bool> ExistsForDepartmentAsync(int departmentId);
        Task<bool> ExistsForOfficeAsync(int officeId);
        Task<bool> ExistsForBranchAsync(int branchId);
        Task<bool> ExistsForSiteAsync(int siteId);
    }
}
