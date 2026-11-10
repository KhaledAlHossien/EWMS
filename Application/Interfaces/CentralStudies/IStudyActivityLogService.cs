using Domain.Entities.CentralStudiesBranch;

namespace Application.Interfaces
{
    /// <summary>سجل الدراسة (إلحاقي)</summary>
    public interface IStudyActivityLogService
    {
        Task AddAsync(StudyActivityLog log);
        /// <summary>صفحة من سجل الدراسة (الأحدث أولاً) مع اسم المنفّذ فقط — إسقاط مباشر بلا كيانات</summary>
        /// <param name="projectId">مع <paramref name="visibleTasks"/>: سجل بحدّ الرؤية — تُخفى أحداث المشروع والمراحل والفريق ومرفقات المشروع،
        /// وتبقى مهام يحققها الشرط فقط (مهمة محذوفة لا يُعرف صاحب رؤيتها فتُخفى)؛ أحداث الوثائق والمخططات كما هي</param>
        Task<(List<Application.DTOs.Response.StudyActivityLogDto> Items, int Total)> GetForRequestAsync(
            int requestId, int page, int pageSize, int? projectId = null,
            System.Linq.Expressions.Expression<Func<StageTask, bool>>? visibleTasks = null);
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
