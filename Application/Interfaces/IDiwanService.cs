using System.Linq.Expressions;
using Domain.Entities;

namespace Application.Interfaces
{
    /// <summary>فلاتر قائمة المعاملات (كلها اختيارية، تُجمع بـ AND). Inbox: ما تحمله وحدتي الآن ولم يُغلق.</summary>
    public sealed record DiwanFilter(
        CorrespondenceDirection? Direction, int? BranchId, int? TypeId, int? StatusId, CorrespondenceStage? Stage, int? ProjectId,
        string? Search, DateTime? From, DateTime? To, bool OverdueOnly, Expression<Func<Correspondence, bool>>? Inbox, DateTime Today);

    /// <summary>
    /// حفظ معاملات الديوان وأنواعها وحالاتها ومرفقاتها. كل كتابة تأخذ <see cref="AuditEntry"/> وتحفظه في معاملتها نفسها (القاعدة 6)،
    /// والترقيم (تسلسل الوارد عند التسجيل وتسلسل الصادر عند الصدور) داخل معاملة بقفل تطبيقي لكل فرع واتجاه وسنة.
    /// </summary>
    public interface IDiwanService
    {
        // ───────── الأنواع والحالات (إعدادات الفرع) ─────────
        Task<List<CorrespondenceType>> GetTypesAsync(int? branchId, bool includeInactive);
        Task<CorrespondenceType?> GetTypeAsync(int id);
        Task<CorrespondenceType> AddTypeAsync(CorrespondenceType type, AuditEntry audit);
        Task UpdateTypeAsync(CorrespondenceType type, AuditEntry audit);
        Task DeleteTypeAsync(CorrespondenceType type, AuditEntry audit);
        Task<bool> TypeNameExistsAsync(int branchId, string name, int? excludeId = null);
        Task<bool> TypeInUseAsync(int id);

        Task<List<CorrespondenceStatus>> GetStatusesAsync(int? branchId, bool includeInactive);
        Task<CorrespondenceStatus?> GetStatusAsync(int id);
        Task<CorrespondenceStatus> AddStatusAsync(CorrespondenceStatus status, AuditEntry audit);
        Task UpdateStatusAsync(CorrespondenceStatus status, AuditEntry audit);
        Task DeleteStatusAsync(CorrespondenceStatus status, AuditEntry audit);
        Task<bool> StatusNameExistsAsync(int branchId, string name, int? excludeId = null);
        Task<bool> StatusInUseAsync(int id);

        // ───────── المعاملات ─────────
        /// <summary>المعاملة بنوعها وحالتها ومشروعها ووحدتها الحالية وإحالاتها (بمستخدميها وأقسامها) ومرفقاتها: ما يحتاجه DiwanRules.Visible في الذاكرة</summary>
        Task<Correspondence?> GetByIdAsync(int id);

        Task<(List<Correspondence> Items, int TotalCount)> SearchAsync(Expression<Func<Correspondence, bool>> visible, DiwanFilter filter, int page, int pageSize);

        /// <summary>
        /// يحفظ المعاملة وأثرها في معاملة واحدة. للوارد يُعطى التسلسل هنا (قفل تطبيقي)، وللمسودة لا تسلسل حتى الصدور.
        /// رقمها لا يُعرف قبل الحفظ فيُكمَّل الأثر به.
        /// </summary>
        Task<Correspondence> AddAsync(Correspondence correspondence, AuditEntry audit);

        /// <summary>يحفظ التعديل وأثره معاً. rowVersion = ما رآه المستخدم؛ تغيّر المعاملة بعده يرفض الحفظ برسالة واضحة</summary>
        Task UpdateAsync(Correspondence correspondence, byte[]? rowVersion, AuditEntry audit);

        /// <summary>إحالة: يضيف السجل ويحدّث الجهة الحاملة (يضبطها المستدعي على الكيان) ويحفظ الأثر معاً</summary>
        Task AddReferralAsync(Correspondence correspondence, CorrespondenceReferral referral, AuditEntry audit);

        /// <summary>تسجيل استلام الإحالة (مرة واحدة)</summary>
        Task AcknowledgeAsync(Correspondence correspondence, CorrespondenceReferral referral, int userId, AuditEntry audit);

        /// <summary>إصدار الصادر المعتمَد: يعطيه تسلسله في سنته ويقفله، في معاملة واحدة مع الأثر</summary>
        Task IssueAsync(Correspondence correspondence, int userId, AuditEntry audit);

        Task<CorrespondenceReferral?> GetReferralAsync(int id);

        // ───────── المرفقات ─────────
        Task AddAttachmentsAsync(Correspondence correspondence, IReadOnlyCollection<CorrespondenceAttachment> attachments, AuditEntry audit);
        Task<CorrespondenceAttachment?> GetAttachmentAsync(int id);
        Task<byte[]?> GetAttachmentDataAsync(int id);
        Task DeleteAttachmentAsync(Correspondence correspondence, CorrespondenceAttachment attachment, AuditEntry audit);

        // ───────── السجل ─────────
        /// <summary>أثر عمليات معاملة واحدة (الأحدث أولاً). الحدّ مفروض من المستدعي على المعاملة نفسها.</summary>
        Task<List<AuditLog>> GetHistoryAsync(int correspondenceId);

        // ───────── حراس الحذف (كل مفتاح أجنبي Restrict) ─────────
        Task<bool> ExistsForBranchAsync(int branchId);
        Task<bool> ExistsForDepartmentAsync(int departmentId);
        Task<bool> ExistsForOfficeAsync(int officeId);
        /// <summary>سجّل أو أحال أو استلم أو اعتمد أو أصدر أو أرفق، أو حملت المعاملة باسمه، أو له أثر في سجل التدقيق عليها</summary>
        Task<bool> ExistsForUserAsync(int userId);
        Task<bool> ExistsForProjectAsync(int projectId);
    }
}
