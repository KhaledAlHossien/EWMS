using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities.Maintenance;
using MediatR;

namespace Application.Features.Maintenance.Tasks.Commands.Assign
{
    public class AssignMaintenanceTaskCommandHandler : IRequestHandler<AssignMaintenanceTaskCommand, MaintenanceTaskResponseDto>
    {
        private readonly IMaintenanceTaskService _taskService;
        private readonly IUserService _userService;
        private readonly INotificationService _notifications;
        private readonly IUserPermissionService _permissions;
        private readonly MaintenanceTaskSupport _support;

        public AssignMaintenanceTaskCommandHandler(IMaintenanceTaskService taskService, IUserService userService, INotificationService notifications, IUserPermissionService permissions, MaintenanceTaskSupport support)
        {
            _taskService = taskService;
            _userService = userService;
            _notifications = notifications;
            _permissions = permissions;
            _support = support;
        }

        public async Task<MaintenanceTaskResponseDto> Handle(AssignMaintenanceTaskCommand request, CancellationToken ct)
        {
            var user = await MaintenanceRules.CurrentUserAsync(_userService);
            var entity = await LoadAsync(request.Id);

            var assign = await MaintenanceRules.BoundaryAsync(_userService, _permissions, "AssignMaintenanceTask");
            MaintenanceRules.Ensure(MaintenanceRules.In(assign, entity), "لا يمكنك نقل مهمة خارج نطاقك");

            var target = await MaintenanceRules.ResolveAssigneeAsync(_userService, assign, request.UserId);

            if (target.Id == entity.UserId)
                return await _support.ToDtoAsync(entity);

            var previousUserId = entity.UserId;

            entity.UserId = target.Id;
            entity.DepartmentId = target.DepartmentId;
            entity.UpdatedAt = DateTime.UtcNow;
            await _taskService.UpdateAsync(entity);

            var updated = await LoadAsync(entity.Id);
            await MaintenanceNotifier.TaskReassignedAsync(_notifications, updated, user, previousUserId, target);

            return await _support.ToDtoAsync(updated);
        }

        private async Task<MaintenanceTask> LoadAsync(int id) =>
            await _taskService.GetByIdAsync(id) ?? throw new KeyNotFoundException("المهمة غير موجودة");
    }
}
