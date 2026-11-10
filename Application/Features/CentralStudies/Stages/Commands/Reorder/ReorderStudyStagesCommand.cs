using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.Stages.Commands.Reorder
{
    public record ReorderStudyStagesCommand(int ProjectId, List<int> StageIds) : IRequest<List<ProjectStageDto>>;
}
