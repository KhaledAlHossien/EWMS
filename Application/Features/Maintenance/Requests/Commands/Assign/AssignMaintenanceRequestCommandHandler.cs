using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities.Maintenance;
using MediatR;

namespace Application.Features.Maintenance.Requests.Commands.Assign
{
    public class AssignMaintenanceRequestCommandHandler : IRequestHandler<AssignMaintenanceRequestCommand, MaintenanceRequestResponseDto>
    {
        private readonly IMaintenanceRequestService _requestService;
        private readonly IUserService _userService;
        private readonly INotificationService _notifications;
        private readonly IUserPermissionService _permissions;
        private readonly IMapper _mapper;
        private readonly MaintenanceRequestSupport _support;

        public AssignMaintenanceRequestCommandHandler(IMaintenanceRequestService requestService, IUserService userService, INotificationService notifications, IUserPermissionService permissions, IMapper mapper, MaintenanceRequestSupport support)
        {
            _requestService = requestService;
            _userService = userService;
            _notifications = notifications;
            _permissions = permissions;
            _mapper = mapper;
            _support = support;
        }

        public async Task<MaintenanceRequestResponseDto> Handle(AssignMaintenanceRequestCommand request, CancellationToken ct)
        {
            var user = await MaintenanceRules.CurrentUserAsync(_userService);
            var entity = await LoadAsync(request.Id);

            var assign = await MaintenanceRules.BoundaryAsync(_userService, _permissions, "AssignMaintenanceRequest");
            MaintenanceRules.Ensure(MaintenanceRules.In(assign, entity), "لا يمكنك نقل طلب صيانة خارج نطاقك");
            MaintenanceRules.EnsureOpen(entity);

            var target = await MaintenanceRules.ResolveAssigneeAsync(_userService, assign, request.UserId);

            if (target.Id == entity.UserId)
                return await ToDtoAsync(entity);

            var previousUserId = entity.UserId;
            var previousName = entity.User?.FullName ?? "";

            entity.UserId = target.Id;
            entity.DepartmentId = target.DepartmentId;
            entity.UpdatedAt = DateTime.UtcNow;
            await _requestService.UpdateAsync(entity);

            var updated = await LoadAsync(entity.Id);

            await _support.LogAsync(updated, user, MaintenanceActivityType.Reassigned,
                $"نقل الطلب من {previousName} إلى {target.FullName}");
            await MaintenanceNotifier.RequestReassignedAsync(_notifications, updated, user, previousUserId, target);

            // طلب تحويل معلّق صار بلا معنى بعد النقل المباشر — يُغلق تلقائياً
            if (await _requestService.GetPendingTransferAsync(updated.Id) is { } pending
                && await _requestService.GetTransferAsync(pending.Id) is { } transfer)
            {
                transfer.Status = MaintenanceTransferStatus.Closed;
                transfer.DecidedById = user.Id;
                transfer.DecidedAt = DateTime.UtcNow;
                transfer.NewUserId = target.Id;
                transfer.DecisionNote = "نُقل الطلب مباشرة قبل البت في طلب التحويل";
                await _requestService.UpdateTransferAsync(transfer);
            }

            return await ToDtoAsync(updated);
        }

        private async Task<MaintenanceRequest> LoadAsync(int id) =>
            await _requestService.GetByIdAsync(id) ?? throw new KeyNotFoundException("طلب الصيانة غير موجود");

        private async Task<MaintenanceRequestResponseDto> ToDtoAsync(MaintenanceRequest entity) =>
            MaintenanceRequestMapping.ToDto(_mapper, entity, await MaintenanceRules.RequestScopesAsync(_userService, _permissions));
    }
}
