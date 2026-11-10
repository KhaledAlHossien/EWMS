using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.Tasks.Queries.GetByStage
{
    public record GetStudyTasksQuery(int StageId) : IRequest<List<StageTaskDto>>;
}
