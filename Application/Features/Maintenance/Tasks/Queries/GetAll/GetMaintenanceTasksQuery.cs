using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Maintenance.Tasks.Queries.GetAll
{
    /// <summary>مهامي، أو مهام قسمي لرئيس القسم، أو فرعي لرئيس الفرع (اطلاع)، أو الكل للسوبر ادمن — UserId اختياري لفلترة موظف معيّن</summary>
    public record GetMaintenanceTasksQuery(int? UserId, int Page, int PageSize)
        : IRequest<PagedResultDto<MaintenanceTaskResponseDto>>;
}
