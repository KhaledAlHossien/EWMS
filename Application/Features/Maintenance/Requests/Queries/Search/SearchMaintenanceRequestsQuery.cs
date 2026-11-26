using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Maintenance.Requests.Queries.Search
{
    /// <summary>بحث/عرض الطلبات ضمن نطاقي (طلباتي، أو طلبات قسمي لرئيس القسم، أو فرعي لرئيس الفرع، أو الكل للسوبر ادمن)</summary>
    public record SearchMaintenanceRequestsQuery(MaintenanceRequestFilterDto Filter)
        : IRequest<PagedResultDto<MaintenanceRequestResponseDto>>;
}
