using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities.Maintenance;
using MediatR;

namespace Application.Features.Maintenance.Requests.Commands.ChangeStatus
{
    public class ChangeMaintenanceRequestStatusCommandHandler : IRequestHandler<ChangeMaintenanceRequestStatusCommand, MaintenanceRequestResponseDto>
    {
        private readonly IMaintenanceRequestService _requestService;
        private readonly IMaintenanceRequestStatusService _statusService;
        private readonly IUserService _userService;
        private readonly INotificationService _notifications;
        private readonly IUserPermissionService _permissions;
        private readonly IMapper _mapper;
        private readonly MaintenanceRequestSupport _support;

        public ChangeMaintenanceRequestStatusCommandHandler(IMaintenanceRequestService requestService, IMaintenanceRequestStatusService statusService, IUserService userService, INotificationService notifications, IUserPermissionService permissions, IMapper mapper, MaintenanceRequestSupport support)
        {
            _requestService = requestService;
            _statusService = statusService;
            _userService = userService;
            _notifications = notifications;
            _permissions = permissions;
            _mapper = mapper;
            _support = support;
        }

        public async Task<MaintenanceRequestResponseDto> Handle(ChangeMaintenanceRequestStatusCommand request, CancellationToken ct)
        {
            var user = await MaintenanceRules.CurrentUserAsync(_userService);
            var entity = await LoadAsync(request.Id);

            MaintenanceRules.Ensure(MaintenanceRules.In(await MaintenanceRules.BoundaryAsync(_userService, _permissions, AppPermissions.ChangeMaintenanceStatus), entity),
                "لا تملك صلاحية تغيير حالة هذا الطلب");

            if (entity.MaintenanceRequestStatusId == request.StatusId)
                return await ToDtoAsync(entity);

            MaintenanceRules.EnsureOpen(entity);
            var newStatus = await _statusService.GetByIdAsync(request.StatusId)
                ?? throw new KeyNotFoundException("حالة الطلب المحددة غير موجودة");

            var oldStatusName = entity.MaintenanceRequestStatus?.Name ?? "";

            var previousStatusId = entity.MaintenanceRequestStatusId;
            entity.MaintenanceRequestStatusId = request.StatusId;
            entity.UpdatedAt = DateTime.UtcNow;
            MaintenanceRules.ApplyStageTimes(entity, newStatus.Stage);
            await _support.StampDeliveryAsync(entity, previousStatusId);
            await _requestService.UpdateAsync(entity);

            var updated = await LoadAsync(entity.Id);
            var what = $"غيّر الحالة من «{oldStatusName}» إلى «{updated.MaintenanceRequestStatus?.Name}»";

            await _support.LogAsync(updated, user, MaintenanceActivityType.StatusChanged, what);
            await MaintenanceNotifier.RequestChangedAsync(_notifications, _permissions, updated, user, what);
            await _support.NotifyClientStatusAsync(updated, user);
            await _support.ClosePendingTransferIfFinalAsync(updated, user);

            return await ToDtoAsync(updated);
        }

        private async Task<MaintenanceRequest> LoadAsync(int id) =>
            await _requestService.GetByIdAsync(id) ?? throw new KeyNotFoundException("طلب الصيانة غير موجود");

        private async Task<MaintenanceRequestResponseDto> ToDtoAsync(MaintenanceRequest entity) =>
            MaintenanceRequestMapping.ToDto(_mapper, entity, await MaintenanceRules.RequestScopesAsync(_userService, _permissions));
    }
}
