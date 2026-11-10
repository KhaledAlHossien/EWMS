using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.Tasks.Queries.GetMine
{
    public record GetMyStudyTasksQuery(bool OpenOnly, int Page, int PageSize) : IRequest<PagedResultDto<StageTaskDto>>;
}
