using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Maintenance.Requests.Queries.GetPrint
{
    /// <summary>بيانات الطباعة: الطلب + اسم وتوقيع من يملك SignMaintenanceReceipt في قسم الطلب</summary>
    public record GetMaintenanceRequestPrintQuery(int Id) : IRequest<MaintenancePrintDto>;
}
