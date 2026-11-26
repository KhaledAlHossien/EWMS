using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities.Maintenance;
using MediatR;

namespace Application.Features.Maintenance.Transfers.Commands.Decide
{
    public class DecideMaintenanceTransferCommandHandler : IRequestHandler<DecideMaintenanceTransferCommand, MaintenanceTransferDto>
    {
        private readonly IMaintenanceRequestService _requests;
        private readonly IUserService _users;
        private readonly IUserPermissionService _permissions;
        private readonly INotificationService _notifications;
        private readonly MaintenanceTransferSupport _support;

        public DecideMaintenanceTransferCommandHandler(IMaintenanceRequestService requests, IUserService users, IUserPermissionService permissions, INotificationService notifications, MaintenanceTransferSupport support)
        {
            _requests = requests;
            _users = users;
            _permissions = permissions;
            _notifications = notifications;
            _support = support;
        }

        public async Task<MaintenanceTransferDto> Handle(DecideMaintenanceTransferCommand request, CancellationToken ct)
        {
            var user = await MaintenanceRules.CurrentUserAsync(_users);
            var transfer = await _requests.GetTransferAsync(request.TransferId)
                ?? throw new KeyNotFoundException("طلب التحويل غير موجود");
            if (transfer.Status != MaintenanceTransferStatus.Pending)
                throw new InvalidOperationException("بُتّ في طلب التحويل هذا مسبقاً");

            var entity = await _requests.GetByIdAsync(transfer.MaintenanceRequestId)
                ?? throw new KeyNotFoundException("طلب الصيانة غير موجود");

            // نفس حدّ Assign/{id}: طلبات قسمي فقط (SuperAdmin الكل)
            var assign = await MaintenanceRules.BoundaryAsync(_users, _permissions, "AssignMaintenanceRequest");
            MaintenanceRules.Ensure(MaintenanceRules.In(assign, entity), "لا يمكنك البت في تحويل طلب خارج قسمك");

            var note = string.IsNullOrWhiteSpace(request.Dto.Note) ? null : request.Dto.Note.Trim();
            transfer.DecidedById = user.Id;
            transfer.DecidedAt = DateTime.UtcNow;
            transfer.DecisionNote = note;

            if (!request.Dto.Approve)
            {
                transfer.Status = MaintenanceTransferStatus.Rejected;
                await _requests.UpdateTransferAsync(transfer);
                await _support.LogAsync(entity.Id, user, MaintenanceActivityType.TransferRejected,
                    "رفض طلب التحويل" + (note == null ? "" : $" — {note}"));
                await MaintenanceNotifier.TransferRejectedAsync(_notifications, entity, user, transfer.RequestedById, note);
                return MaintenanceTransferSupport.ToDto((await _requests.GetTransferAsync(transfer.Id))!);
            }

            MaintenanceRules.EnsureOpen(entity);
            var targetId = request.Dto.UserId ?? transfer.SuggestedUserId
                ?? throw new InvalidOperationException("اختر الموظف الذي يُحوَّل إليه الطلب");
            var target = await MaintenanceRules.ResolveAssigneeAsync(_users, assign, targetId);
            MaintenanceRules.Ensure(target.Id != entity.UserId, "اختر موظفاً غير الفني الحالي");

            var previousName = entity.User?.FullName ?? "";
            entity.UserId = target.Id;
            entity.DepartmentId = target.DepartmentId;
            entity.UpdatedAt = DateTime.UtcNow;
            await _requests.UpdateAsync(entity);

            transfer.Status = MaintenanceTransferStatus.Approved;
            transfer.NewUserId = target.Id;
            await _requests.UpdateTransferAsync(transfer);

            await _support.LogAsync(entity.Id, user, MaintenanceActivityType.Reassigned,
                $"قبل طلب التحويل ونقل الطلب من {previousName} إلى {target.FullName} — السبب: {transfer.Reason}" + (note == null ? "" : $" — {note}"));
            await MaintenanceNotifier.TransferApprovedAsync(_notifications, entity, user, transfer.RequestedById, target);

            return MaintenanceTransferSupport.ToDto((await _requests.GetTransferAsync(transfer.Id))!);
        }
    }
}
