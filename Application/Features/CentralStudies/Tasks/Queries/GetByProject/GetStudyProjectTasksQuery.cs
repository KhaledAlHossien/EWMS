using MediatR;

namespace Application.Features.CentralStudies.Tasks.Queries.GetByProject
{
    /// <summary>كل مهام المشروع (لمخطط Gantt): بترتيب المرحلة ثم البدء المخطط</summary>
    public record GetStudyProjectTasksQuery(int ProjectId) : IRequest<StudyProjectTasksResult>;
}
