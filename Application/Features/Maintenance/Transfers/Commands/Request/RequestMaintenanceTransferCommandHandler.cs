using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities;
using Domain.Entities.Maintenance;
using MediatR;

namespace Application.Features.Maintenance.Transfers.Commands.Request
{
    public class RequestMaintenanceTransferCommandHandler : IRequestHandler<RequestMaintenanceTransferCommand, MaintenanceTransferDto>
    {
        private readonly IMaintenanceRequestService _requests;
        private readonly IUserService _users;
        private readonly IUserPermissionService _permissions;
        private readonly INotificationService _notifications;
        private readonly MaintenanceTransferSupport _support;

        public RequestMaintenanceTransferCommandHandler(IMaintenanceRequestService requests, IUserService users, IUserPermissionService permissions, INotificationService notifications, MaintenanceTransferSupport support)
        {
            _requests = requests;
            _users = users;
            _permissions = permissions;
            _notifications = notifications;
            _support = support;
        }

        public async Task<MaintenanceTransferDto> Handle(RequestMaintenanceTransferCommand request, CancellationToken ct)
        {
            var user = await MaintenanceRules.CurrentUserAsync(_users);
            var entity = await _requests.GetByIdAsync(request.RequestId)
                ?? throw new KeyNotFoundException("طلب الصيانة غير موجود");

            // الصلاحية (Policy) تفتح العملية، والحد: الطلب مسند إليّ أنا
            MaintenanceRules.Ensure(entity.UserId == user.Id, "يطلب التحويل الفني المسند إليه الطلب فقط");
            MaintenanceRules.EnsureOpen(entity);

            if (await _requests.GetPendingTransferAsync(entity.Id) != null)
                throw new InvalidOperationException("يوجد طلب تحويل بانتظار قرار رئيس القسم لهذا الطلب");

            User? suggested = null;
            if (request.Dto.SuggestedUserId is int suggestedId)
            {
                MaintenanceRules.Ensure(suggestedId != user.Id, "اختر زميلاً غيرك");
                suggested = await _users.GetByIdAsync(suggestedId) ?? throw new KeyNotFoundException("الزميل المقترح غير موجود");
                if (!suggested.IsActive) throw new InvalidOperationException("الزميل المقترح حسابه معطّل");
                if (suggested.DepartmentId == null || suggested.DepartmentId != entity.DepartmentId)
                    throw new InvalidOperationException("اقترح زميلاً من قسم الطلب");
            }

            var reason = request.Dto.Reason.Trim();
            var transfer = new MaintenanceTransferRequest
            {
                MaintenanceRequestId = entity.Id,
                RequestedById = user.Id,
                SuggestedUserId = suggested?.Id,
                Reason = reason,
                Status = MaintenanceTransferStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };
            await _requests.AddTransferAsync(transfer);

            await _support.LogAsync(entity.Id, user, MaintenanceActivityType.TransferRequested,
                $"طلب تحويل الطلب{(suggested != null ? $" إلى {suggested.FullName}" : "")} — السبب: {reason}");
            await MaintenanceNotifier.TransferRequestedAsync(_notifications, _permissions, entity, user, reason);

            return MaintenanceTransferSupport.ToDto((await _requests.GetTransferAsync(transfer.Id))!);
        }
    }
}
