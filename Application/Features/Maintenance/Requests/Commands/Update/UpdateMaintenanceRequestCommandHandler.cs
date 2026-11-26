using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities.Maintenance;
using MediatR;

namespace Application.Features.Maintenance.Requests.Commands.Update
{
    public class UpdateMaintenanceRequestCommandHandler : IRequestHandler<UpdateMaintenanceRequestCommand, MaintenanceRequestResponseDto>
    {
        private readonly IMaintenanceRequestService _requestService;
        private readonly IMaintenanceRequestStatusService _statusService;
        private readonly IUserService _userService;
        private readonly INotificationService _notifications;
        private readonly IUserPermissionService _permissions;
        private readonly IMapper _mapper;
        private readonly MaintenanceRequestSupport _support;

        public UpdateMaintenanceRequestCommandHandler(IMaintenanceRequestService requestService, IMaintenanceRequestStatusService statusService, IUserService userService, INotificationService notifications, IUserPermissionService permissions, IMapper mapper, MaintenanceRequestSupport support)
        {
            _requestService = requestService;
            _statusService = statusService;
            _userService = userService;
            _notifications = notifications;
            _permissions = permissions;
            _mapper = mapper;
            _support = support;
        }

        public async Task<MaintenanceRequestResponseDto> Handle(UpdateMaintenanceRequestCommand request, CancellationToken ct)
        {
            var user = await MaintenanceRules.CurrentUserAsync(_userService);
            var entity = await LoadAsync(request.Id);

            MaintenanceRules.Ensure(MaintenanceRules.In(await MaintenanceRules.BoundaryAsync(_userService, _permissions, "EditMaintenanceRequest"), entity),
                "لا يمكنك تعديل طلب صيانة خارج نطاقك");

            await _support.EnsureReferencesAsync(request.Dto);

            // تغيير الحالة عبر نموذج التعديل يحتاج صلاحية تغيير الحالة أيضاً (دون تعديل بقية البيانات إن لم يملكها)
            if (request.Dto.MaintenanceRequestStatusId != entity.MaintenanceRequestStatusId)
                MaintenanceRules.Ensure(MaintenanceRules.In(await MaintenanceRules.BoundaryAsync(_userService, _permissions, AppPermissions.ChangeMaintenanceStatus), entity),
                    "لا تملك صلاحية تغيير حالة الطلب — أبقِ الحالة كما هي");

            MaintenanceRules.EnsureOpen(entity);

            var oldStatusId = entity.MaintenanceRequestStatusId;
            var oldStatusName = entity.MaintenanceRequestStatus?.Name ?? "";

            // الفني والقسم وتاريخ الإنشاء لا تتغير (ليست في الـ DTO)
            _mapper.Map(request.Dto, entity);
            await _support.ApplyClientAsync(entity, request.Dto);
            MaintenanceRequestSupport.Trim(entity);
            entity.UpdatedAt = DateTime.UtcNow;
            if (entity.MaintenanceRequestStatusId != oldStatusId)
                MaintenanceRules.ApplyStageTimes(entity, (await _statusService.GetByIdAsync(entity.MaintenanceRequestStatusId))!.Stage);
            await _support.StampDeliveryAsync(entity, oldStatusId);

            await _requestService.UpdateAsync(entity);

            var updated = await LoadAsync(entity.Id);
            var statusChanged = oldStatusId != updated.MaintenanceRequestStatusId;
            var what = statusChanged
                ? $"غيّر الحالة من «{oldStatusName}» إلى «{updated.MaintenanceRequestStatus?.Name}»"
                : "عدّل بيانات الطلب";

            await _support.LogAsync(updated, user,
                statusChanged ? MaintenanceActivityType.StatusChanged : MaintenanceActivityType.Edited, what);

            await MaintenanceNotifier.RequestChangedAsync(_notifications, _permissions, updated, user, what);
            if (statusChanged) { await _support.NotifyClientStatusAsync(updated, user); await _support.ClosePendingTransferIfFinalAsync(updated, user); }

            return await ToDtoAsync(updated);
        }

        private async Task<MaintenanceRequest> LoadAsync(int id) =>
            await _requestService.GetByIdAsync(id) ?? throw new KeyNotFoundException("طلب الصيانة غير موجود");

        private async Task<MaintenanceRequestResponseDto> ToDtoAsync(MaintenanceRequest entity) =>
            MaintenanceRequestMapping.ToDto(_mapper, entity, await MaintenanceRules.RequestScopesAsync(_userService, _permissions));
    }
}
