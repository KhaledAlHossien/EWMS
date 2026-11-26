using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Maintenance.Transfers.Queries.GetPendingForMe
{
    /// <summary>طلبات التحويل المعلّقة التي أستطيع البت فيها (طلبات قسمي)</summary>
    public record GetPendingTransfersForMeQuery : IRequest<List<MaintenanceTransferDto>>;
}
