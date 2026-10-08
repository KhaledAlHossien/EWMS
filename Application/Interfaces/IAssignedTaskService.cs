using System.Linq.Expressions;
using Domain.Entities;

namespace Application.Interfaces
{
    public interface IAssignedTaskService
    {
        /// <summary>مع كل التفاصيل: الجهة، المُسنِد، الأصل، المهام الفرعية، السجل</summary>
        Task<AssignedTask?> GetByIdAsync(int id);

        /// <summary>بطاقات اللوحة (مع المهام الفرعية وتعليقات فقط من السجل لحساب العدد)</summary>
        Task<List<AssignedTask>> GetBoardAsync(Expression<Func<AssignedTask, bool>> filter);

        Task AddAsync(AssignedTask task);
        Task AddActivityAsync(AssignedTaskActivity activity);
        Task SaveChangesAsync();

        // ===== المرفقات =====
        /// <summary>مع المحتوى والمهمة (ومهمتها الأصل)</summary>
        Task<AssignedTaskAttachment?> GetAttachmentAsync(int id);
        Task AddAttachmentsAsync(IEnumerable<AssignedTaskAttachment> attachments);
        Task DeleteAttachmentAsync(AssignedTaskAttachment attachment);
        Task<int> CountAttachmentsAsync(int taskId);
        /// <summary>المهام الفرعية المباشرة (بلا تفاصيل) — لقاعدة رؤية مرفقات الأصل من المهمة الفرعية</summary>
        Task<List<AssignedTask>> GetChildrenAsync(int parentTaskId);

        // ===== قائمة التحقق =====
        Task<AssignedTaskChecklistItem?> GetChecklistItemAsync(int id);
        Task AddChecklistItemAsync(AssignedTaskChecklistItem item);
        Task DeleteChecklistItemAsync(AssignedTaskChecklistItem item);
        Task<int> CountChecklistAsync(int taskId);

        // ===== تذكيرات الموعد =====
        /// <summary>المهام غير المنجزة التي موعدها اليوم أو قبله ولم يكتمل إرسال تذكيرها (للتعديل: نسخ متتبَّعة)</summary>
        Task<List<AssignedTask>> GetForRemindersAsync(DateTime tomorrow);
        Task DeleteAsync(AssignedTask task);

        // حراسة الحذف (العلاقات Restrict)
        Task<bool> ExistsForDepartmentAsync(int departmentId);
        Task<bool> ExistsForOfficeAsync(int officeId);
        Task<bool> ExistsForUserAsync(int userId);
    }
}
