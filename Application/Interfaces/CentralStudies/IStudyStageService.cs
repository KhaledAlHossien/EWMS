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
        Task<List<StageTask>> GetTasksAsync(int stageId);
        /// <summary>كل مهام المشروع في استعلام واحد (لمخطط Gantt): بترتيب المرحلة ثم البدء المخطط</summary>
        Task<List<StageTask>> GetProjectTasksAsync(int projectId);
        /// <summary>المهام المسندة إلى المستخدم أو مكتبه أو قسمه (للقسم: موظفو القسم المباشرون بلا مكتب)</summary>
        Task<(List<StageTask> Items, int Total)> GetAssignedAsync(int userId, int? officeId, int? departmentId, bool openOnly, int page, int pageSize);
        Task AddTaskAsync(StageTask task);
        Task UpdateTaskAsync(StageTask task, byte[]? expectedRowVersion = null);
        Task DeleteTaskAsync(StageTask task);

        Task SaveAsync();
    }
}
