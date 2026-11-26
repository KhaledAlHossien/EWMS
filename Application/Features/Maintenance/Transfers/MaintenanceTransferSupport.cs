using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities;
using Domain.Entities.Maintenance;

namespace Application.Features.Maintenance.Transfers
{
    /// <summary>الدوال المشتركة بين عمليات هذه الميزة</summary>
    public class MaintenanceTransferSupport
    {
        private readonly IMaintenanceRequestService _requests;

        public MaintenanceTransferSupport(IMaintenanceRequestService requests)
        {
            _requests = requests;
        }

        public static string StatusAr(MaintenanceTransferStatus s) => s switch
        {
            MaintenanceTransferStatus.Pending => "بانتظار قرار رئيس القسم",
            MaintenanceTransferStatus.Approved => "قُبل",
            MaintenanceTransferStatus.Rejected => "رُفض",
            MaintenanceTransferStatus.Closed => "أُغلق — نُقل الطلب مباشرة",
            _ => ""
        };

        public static MaintenanceTransferDto ToDto(MaintenanceTransferRequest t) => new()
        {
            Id = t.Id,
            MaintenanceRequestId = t.MaintenanceRequestId,
            RequestNumber = t.MaintenanceRequest == null ? "" : MaintenanceRules.RequestNumber(t.MaintenanceRequest.Id, t.MaintenanceRequest.CreatedAt),
            ClientName = t.MaintenanceRequest?.ClientName ?? "",
            RequestedById = t.RequestedById,
            RequestedByName = t.RequestedBy?.FullName ?? "",
            SuggestedUserId = t.SuggestedUserId,
            SuggestedUserName = t.SuggestedUser?.FullName,
            Reason = t.Reason,
            Status = (int)t.Status,
            StatusAr = StatusAr(t.Status),
            DecidedByName = t.DecidedBy?.FullName,
            NewUserName = t.NewUser?.FullName,
            DecisionNote = t.DecisionNote,
            CreatedAt = t.CreatedAt,
            DecidedAt = t.DecidedAt
        };

        public Task LogAsync(int requestId, User actor, MaintenanceActivityType type, string text) =>
            _requests.AddActivityAsync(new MaintenanceRequestActivity
            {
                MaintenanceRequestId = requestId, UserId = actor.Id, Type = type, Text = text, CreatedAt = DateTime.UtcNow
            });
    }
}
