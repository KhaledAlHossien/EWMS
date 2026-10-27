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
        // حراس الحذف: كل مفتاح أجنبي هنا Restrict، فلا يُترك لقاعدة البيانات أن تفشل بخطأ 500 غامض
        Task<bool> ExistsForBranchAsync(int branchId);
        Task<bool> ExistsForDepartmentAsync(int departmentId);
        Task<bool> ExistsForSiteAsync(int siteId);
        /// <summary>أنشأ أو شارك في مشروع، أو رفع/اعتمد إصدار وثيقة، أو له أثر في سجل التدقيق</summary>
        Task<bool> ExistsForUserAsync(int userId);
    }

    /// <summary>أثر عملية في سجل التدقيق (يُبنى في الـ handler ويحفظه الـ service مع العملية)</summary>
    public sealed record AuditEntry(int UserId, string Action, string EntityType, int? EntityId, string Title, string Details = "");

    /// <summary>قراءة سجل التدقيق. الكتابة لا تمرّ من هنا: تُحفظ مع العملية نفسها في خدمتها.</summary>
    public interface IAuditLogService
    {
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
