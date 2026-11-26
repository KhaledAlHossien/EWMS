using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Maintenance.Transfers.Queries.GetPending
{
    /// <summary>طلب التحويل المعلّق لطلب صيانة (null إن لم يوجد) — لمن يرى الطلب</summary>
    public record GetPendingTransferQuery(int RequestId) : IRequest<MaintenanceTransferDto?>;
}
