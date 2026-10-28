using System.Linq.Expressions;
using Domain.Entities;

namespace Application.Interfaces
{
    /// <summary>
    /// حفظ المشاريع ووثائقها. كل عملية كتابة تأخذ <see cref="AuditEntry"/> وتحفظه في معاملتها نفسها،
    /// فلا يوجد تغيير أو اعتماد بلا أثر في السجل (القاعدة 6).
    /// </summary>
    public interface IProjectService
    {
        /// <summary>المشروع مع فرعه وقسمه وموقعه ومنشئه ومشاركيه (ومستخدميهم وأقسامهم): ما تحتاجه ProjectRules.Visible في الذاكرة</summary>
        Task<Project?> GetByIdAsync(int id);

        /// <summary>بحث مرقّم ضمن شرط الرؤية (الأحدث أولاً)</summary>
        Task<(List<Project> Items, int TotalCount)> SearchAsync(Expression<Func<Project, bool>> visible, ProjectFilter filter, int page, int pageSize);

        /// <summary>يحفظ المشروع وأثره في المعاملة نفسها (رقم المشروع يُعرف بعد الحفظ فيُكمَّل الأثر به)</summary>
        Task<Project> AddAsync(Project project, AuditEntry audit);

        /// <summary>يحفظ التعديل وأثره معاً. rowVersion = ما رآه المستخدم؛ تغيّر المشروع بعده يرفض الحفظ برسالة واضحة</summary>
        Task UpdateAsync(Project project, byte[]? rowVersion, AuditEntry audit);

        Task AddMemberAsync(ProjectMember member, AuditEntry audit);
        Task RemoveMemberAsync(ProjectMember member, AuditEntry audit);

        Task<bool> ReferenceExistsAsync(int branchId, string reference, int? excludeProjectId = null);

        /// <summary>موظفون نشطون من فرع المشروع لم يُضافوا بعد (بحث بالاسم)، لنافذة إضافة مشارك</summary>
        Task<List<User>> GetMemberCandidatesAsync(int branchId, string? search, IReadOnlyCollection<int> excludeUserIds, int take);

        // حراس الحذف: كل مفتاح أجنبي هنا Restrict، فلا يُترك لقاعدة البيانات أن تفشل بخطأ 500 غامض
        Task<bool> ExistsForBranchAsync(int branchId);
        Task<bool> ExistsForDepartmentAsync(int departmentId);
        Task<bool> ExistsForSiteAsync(int siteId);
        /// <summary>مكتب أُسند إليه مخرج فني في أمر دراسة</summary>
        Task<bool> ExistsForOfficeAsync(int officeId);
        /// <summary>أنشأ أو شارك في مشروع، أو رفع/اعتمد إصدار وثيقة، أو له أثر في سجل التدقيق</summary>
        Task<bool> ExistsForUserAsync(int userId);
    }

    /// <summary>فلاتر قائمة المشاريع (كلها اختيارية، تُجمع بـ AND)</summary>
    public sealed record ProjectFilter(string? Search, int? BranchId, int? DepartmentId, int? SiteId, ProjectStage? Stage);

    /// <summary>أثر عملية في سجل التدقيق (يُبنى في الـ handler ويحفظه الـ service مع العملية)</summary>
    public sealed record AuditEntry(int UserId, string Action, string EntityType, int? EntityId, string Title, string Details = "");

    /// <summary>قراءة سجل التدقيق. الكتابة لا تمرّ من هنا: تُحفظ مع العملية نفسها في خدمتها.</summary>
    public interface IAuditLogService
    {
        /// <summary>يسجّل أثراً مستقلاً (مثل تنزيل إصدار معتمد) لا يرافق تغييراً في بيانات</summary>
        Task WriteAsync(AuditEntry entry, int projectId, int branchId);

        /// <summary>
        /// سجل مشروع واحد (الأحدث أولاً). الحدّ مفروض على المشروع نفسه من المستدعي،
        /// فهذه الدالة لا تفحص الصلاحيات.
        /// </summary>
        Task<(List<AuditLog> Items, int TotalCount)> GetForProjectAsync(int projectId, string? entityType, int? userId, int page, int pageSize);

        /// <summary>
        /// سجل كل المشاريع المرئية بالشرط المعطى (الأحدث أولاً) — لصفحة التدقيق العامة.
        /// الأثر بلا مشروع لا يظهر هنا إلا لنظرة المؤسسة (<paramref name="includeWithoutProject"/>).
        /// </summary>
        Task<(List<AuditLog> Items, int TotalCount)> SearchAsync(
            Expression<Func<Project, bool>> visibleProjects, bool includeWithoutProject,
            string? entityType, int? userId, DateTime? from, DateTime? to, int page, int pageSize);
    }
}
