using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities;
using Domain.Entities.Maintenance;
using MediatR;

namespace Application.Features.Maintenance.Requests.Commands.Create
{
    public class CreateMaintenanceRequestCommandHandler : IRequestHandler<CreateMaintenanceRequestCommand, MaintenanceRequestResponseDto>
    {
        private readonly IMaintenanceRequestService _requestService;
        private readonly IMaintenanceRequestStatusService _statusService;
        private readonly IUserService _userService;
        private readonly INotificationService _notifications;
        private readonly IUserPermissionService _permissions;
        private readonly IMapper _mapper;
        private readonly MaintenanceRequestSupport _support;

        public CreateMaintenanceRequestCommandHandler(IMaintenanceRequestService requestService, IMaintenanceRequestStatusService statusService, IUserService userService, INotificationService notifications, IUserPermissionService permissions, IMapper mapper, MaintenanceRequestSupport support)
        {
            _requestService = requestService;
            _statusService = statusService;
            _userService = userService;
            _notifications = notifications;
            _permissions = permissions;
            _mapper = mapper;
            _support = support;
        }

        public async Task<MaintenanceRequestResponseDto> Handle(CreateMaintenanceRequestCommand request, CancellationToken ct)
        {
            var user = await MaintenanceRules.CurrentUserAsync(_userService);
            await _support.EnsureReferencesAsync(request.Dto);

            var entity = _mapper.Map<MaintenanceRequest>(request.Dto);
            await _support.ApplyClientAsync(entity, request.Dto);
            MaintenanceRequestSupport.Trim(entity);

            // الفني = من سجّل الطلب، أو موظف من قسمه يسنده إليه من يملك AssignMaintenanceRequest (قرار المستخدم 2026-10-05).
            // القسم يُحفظ لحظة التسجيل (قسم الفني) ليراه رئيس القسم؛ المسجِّل يبقى في سجل الطلب فقط
            User? assignee = null;
            if (request.Dto.AssigneeId is int assigneeId && assigneeId != user.Id)
                assignee = await MaintenanceRules.ResolveAssigneeAsync(_userService,
                    await MaintenanceRules.BoundaryAsync(_userService, _permissions, "AssignMaintenanceRequest"), assigneeId);

            entity.UserId = assignee?.Id ?? user.Id;
            entity.DepartmentId = assignee?.DepartmentId ?? user.DepartmentId;
            entity.CreatedAt = entity.UpdatedAt = DateTime.UtcNow;
            var initial = await _statusService.GetByIdAsync(entity.MaintenanceRequestStatusId)
                ?? throw new KeyNotFoundException("حالة الطلب المحددة غير موجودة");
            if (MaintenanceRules.IsFinal(initial.Stage))
                throw new InvalidOperationException("لا يُسجَّل طلب جديد في حالة نهائية (مُسلَّم أو غير قابل للصيانة)");
            MaintenanceRules.ApplyStageTimes(entity, initial.Stage);
            await _support.StampDeliveryAsync(entity, null);

            var created = await _requestService.AddAsync(entity);

            await _support.LogAsync(created, user, MaintenanceActivityType.Created,
                assignee == null ? "سجّل الطلب" : $"سجّل الطلب وأسنده إلى {assignee.FullName}");
            await MaintenanceNotifier.RequestCreatedAsync(_notifications, _permissions, created, user);
            await MaintenanceNotifier.ClientUpdateAsync(_notifications, created, user, "استُلم جهازك للصيانة",
                $"سُجّل طلب الصيانة {MaintenanceRules.RequestNumber(created.Id, created.CreatedAt)} لجهازك — الحالة: {(await LoadAsync(created.Id)).MaintenanceRequestStatus?.Name}");
            if (assignee != null)
                await MaintenanceNotifier.RequestReassignedAsync(_notifications, created, user, user.Id, assignee);

            return await ToDtoAsync(await LoadAsync(created.Id));
        }

        private async Task<MaintenanceRequest> LoadAsync(int id) =>
            await _requestService.GetByIdAsync(id) ?? throw new KeyNotFoundException("طلب الصيانة غير موجود");

        private async Task<MaintenanceRequestResponseDto> ToDtoAsync(MaintenanceRequest entity) =>
            MaintenanceRequestMapping.ToDto(_mapper, entity, await MaintenanceRules.RequestScopesAsync(_userService, _permissions));
    }
}
