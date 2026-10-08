using System.Linq.Expressions;
using Domain.Entities;
using Domain.Enums;

namespace Application.Interfaces
{
    /// <summary>سطر مختصر لإحصائيات المهام (بلا تحميل الأنشطة والمرفقات)</summary>
    public sealed record TaskStatsRow(
        int Id, AssignedTaskStatus Status, DateTime? DueDate, DateTime CreatedAt, DateTime? CompletedAt,
        int ReturnCount, AssignedTaskTargetType TargetType, int TargetKey, string TargetName);

    /// <summary>قوالب المهام، المهام الدورية، روابط السجلات، وأسطر الإحصائيات</summary>
    public interface IAssignedTaskPlanningService
    {
        // ===== القوالب =====
        Task<List<AssignedTaskTemplate>> GetTemplatesAsync(int ownerUserId);
        /// <summary>مع البنود</summary>
        Task<AssignedTaskTemplate?> GetTemplateAsync(int id);
        Task AddTemplateAsync(AssignedTaskTemplate template);
        Task DeleteTemplateAsync(AssignedTaskTemplate template);
        Task<bool> TemplateNameExistsAsync(int ownerUserId, string name, int? excludeId = null);
        Task<bool> TemplateInUseAsync(int templateId);
        /// <summary>يستبدل بنود القالب (يحذف القديمة ويضيف الجديدة)</summary>
        Task ReplaceTemplateItemsAsync(AssignedTaskTemplate template, IEnumerable<string> items);

        // ===== المهام الدورية =====
        /// <summary>مع القالب وبنوده</summary>
        Task<List<AssignedTaskRecurrence>> GetRecurrencesAsync(int ownerUserId);
        Task<AssignedTaskRecurrence?> GetRecurrenceAsync(int id);
        Task AddRecurrenceAsync(AssignedTaskRecurrence recurrence);
        Task DeleteRecurrenceAsync(AssignedTaskRecurrence recurrence);
        /// <summary>النشطة التي حان موعدها (NextRunDate ≤ today) مع القالب وبنوده وصاحبها</summary>
        Task<List<AssignedTaskRecurrence>> GetDueRecurrencesAsync(DateTime today);

        // ===== الروابط =====
        Task<List<AssignedTaskLink>> GetLinksAsync(int taskId);
        Task<AssignedTaskLink?> GetLinkAsync(int id);
        Task AddLinkAsync(AssignedTaskLink link);
        Task DeleteLinkAsync(AssignedTaskLink link);
        Task<bool> LinkExistsAsync(int taskId, TaskLinkType type, int entityId);
        Task<int> CountLinksAsync(int taskId);
        /// <summary>أرقام المهام المرتبطة بسجل</summary>
        Task<List<int>> GetTaskIdsByLinkAsync(TaskLinkType type, int entityId);

        // ===== الإحصائيات =====
        Task<List<TaskStatsRow>> GetStatsRowsAsync(Expression<Func<AssignedTask, bool>> filter, DateTime from, DateTime to);

        Task SaveChangesAsync();
    }
}
