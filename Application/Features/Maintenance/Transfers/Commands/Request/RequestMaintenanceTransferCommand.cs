using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Maintenance.Transfers.Commands.Request
{
    // طلب تحويل طلب صيانة (قرار المستخدم 2026-10-05):
    // الفني المسند إليه الطلب (RequestMaintenanceTransfer) يطلب التحويل مع سبب واقتراح زميل اختياري؛
    // من يملك نقل طلبات القسم (AssignMaintenanceRequest، نفس حدّ Assign/{id}) يقبل فيُنقل الطلب، أو يرفض مع ملاحظة.
    public record RequestMaintenanceTransferCommand(int RequestId, RequestMaintenanceTransferDto Dto) : IRequest<MaintenanceTransferDto>;
}
