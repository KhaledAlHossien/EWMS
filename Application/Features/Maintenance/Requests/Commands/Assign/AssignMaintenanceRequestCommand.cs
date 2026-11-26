using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Maintenance.Requests.Commands.Assign
{
    /// <summary>نقل الطلب إلى فني آخر — رئيس القسم (داخل قسمه) أو السوبر ادمن</summary>
    public record AssignMaintenanceRequestCommand(int Id, int UserId) : IRequest<MaintenanceRequestResponseDto>;
}
