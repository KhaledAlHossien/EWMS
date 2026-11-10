using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.ActivityLogs.Queries.GetByProject
{
    public record GetProjectActivityLogQuery(int ProjectId, int Page, int PageSize) : IRequest<PagedResultDto<StudyActivityLogDto>>;
}
