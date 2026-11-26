using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Maintenance.Requests.Commands.ChangeStatus
{
    /// <summary>تغيير الحالة فقط (لوحة الحالات) — نفس نطاق التعديل</summary>
    public record ChangeMaintenanceRequestStatusCommand(int Id, int StatusId) : IRequest<MaintenanceRequestResponseDto>;
}
