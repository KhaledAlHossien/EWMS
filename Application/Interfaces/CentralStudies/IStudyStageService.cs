using Domain.Entities.CentralStudiesBranch;

namespace Application.Interfaces
{
    /// <summary>مراحل المشاريع ومهامها</summary>
    public interface IStudyStageService
    {
        /// <summary>مع المشروع ونوع المرحلة والقسم المسؤول ومن أغلقها والمهام</summary>
        Task<ProjectStage?> GetStageAsync(int id);
        Task<List<ProjectStage>> GetStagesAsync(int projectId);
        Task AddStageAsync(ProjectStage stage);
        Task UpdateStageAsync(ProjectStage stage, byte[]? expectedRowVersion = null);
        Task DeleteStageAsync(ProjectStage stage);

        /// <summary>مع المرحلة ومشروعها والجهة المسندة إليها ومن أسندها</summary>
        Task<StageTask?> GetTaskAsync(int id);
        /// <param name="filter">شرط المهام المرئية (ProjectScope.TaskFilter)؛ null = كلها</param>
        Task<List<StageTask>> GetTasksAsync(int stageId, System.Linq.Expressions.Expression<Func<StageTask, bool>>? filter = null);
        /// <summary>كل مهام المشروع في استعلام واحد (لمخطط Gantt): بترتيب المرحلة ثم البدء المخطط</summary>
        /// <summary>أول <paramref name="limit"/> مهمة مع عددها الكلي</summary>
        /// <param name="filter">شرط المهام المرئية؛ والعدد الكلي للمرئي منها فقط</param>
        Task<(List<StageTask> Items, int Total)> GetProjectTasksAsync(int projectId, int limit, System.Linq.Expressions.Expression<Func<StageTask, bool>>? filter = null);
        /// <summary>المراحل (من المشاريع المعطاة) التي فيها مهمة واحدة على الأقل تحقق الشرط</summary>
        Task<HashSet<int>> GetStageIdsWithTasksAsync(IEnumerable<int> projectIds, System.Linq.Expressions.Expression<Func<StageTask, bool>> filter);
        /// <summary>هل المهمة تحقق شرط الرؤية (استعلام واحد بالشرط نفسه فلا نسختان من القاعدة)</summary>
        Task<bool> IsTaskVisibleAsync(int taskId, System.Linq.Expressions.Expression<Func<StageTask, bool>> filter);
        /// <summary>المهام المسندة إلى المستخدم أو مكتبه أو قسمه (للقسم: موظفو القسم المباشرون بلا مكتب)</summary>
        Task<(List<StageTask> Items, int Total)> GetAssignedAsync(int userId, int? officeId, int? departmentId, bool openOnly, int page, int pageSize);
        Task AddTaskAsync(StageTask task);
        Task UpdateTaskAsync(StageTask task, byte[]? expectedRowVersion = null);
        Task DeleteTaskAsync(StageTask task);

        Task SaveAsync();
    }
}
