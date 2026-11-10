using Application.DTOs.Response;

namespace Application.Features.CentralStudies.Tasks.Queries.GetByProject
{
    /// <summary>Total = العدد الكلي؛ Items مقطوعة عند StudyRules.MaxProjectTasks</summary>
    public record StudyProjectTasksResult(List<StageTaskDto> Items, int Total)
    {
        public bool Truncated => Items.Count < Total;
    }
}
