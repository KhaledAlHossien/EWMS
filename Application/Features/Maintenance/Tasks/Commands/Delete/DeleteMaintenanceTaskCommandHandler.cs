using Application.Interfaces;
using Domain.Entities.Maintenance;
using MediatR;

namespace Application.Features.Maintenance.Tasks.Commands.Delete
{
    public class DeleteMaintenanceTaskCommandHandler : IRequestHandler<DeleteMaintenanceTaskCommand, Unit>
    {
        private readonly IMaintenanceTaskService _taskService;
        private readonly IUserService _userService;
        private readonly IUserPermissionService _permissions;

        public DeleteMaintenanceTaskCommandHandler(IMaintenanceTaskService taskService, IUserService userService, IUserPermissionService permissions)
        {
            _taskService = taskService;
            _userService = userService;
            _permissions = permissions;
        }

        public async Task<Unit> Handle(DeleteMaintenanceTaskCommand request, CancellationToken ct)
        {
            var user = await MaintenanceRules.CurrentUserAsync(_userService);
            var entity = await LoadAsync(request.Id);

            MaintenanceRules.Ensure(MaintenanceRules.In(await MaintenanceRules.BoundaryAsync(_userService, _permissions, "DeleteMaintenanceTask"), entity),
                "لا يمكنك حذف مهمة خارج نطاقك");

            await _taskService.DeleteAsync(entity);
            return Unit.Value;
        }

        private async Task<MaintenanceTask> LoadAsync(int id) =>
            await _taskService.GetByIdAsync(id) ?? throw new KeyNotFoundException("المهمة غير موجودة");
    }
}
