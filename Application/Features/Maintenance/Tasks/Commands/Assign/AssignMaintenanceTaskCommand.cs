using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Maintenance.Tasks.Commands.Assign
{
    /// <summary>نقل المهمة إلى موظف آخر — رئيس القسم (داخل قسمه) أو السوبر ادمن</summary>
    public record AssignMaintenanceTaskCommand(int Id, int UserId) : IRequest<MaintenanceTaskResponseDto>;
}
