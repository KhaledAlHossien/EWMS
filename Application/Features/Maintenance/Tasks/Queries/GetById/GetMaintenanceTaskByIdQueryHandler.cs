using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities.Maintenance;
using MediatR;

namespace Application.Features.Maintenance.Tasks.Queries.GetById
{
    public class GetMaintenanceTaskByIdQueryHandler : IRequestHandler<GetMaintenanceTaskByIdQuery, MaintenanceTaskResponseDto>
    {
        private readonly IMaintenanceTaskService _taskService;
        private readonly IUserService _userService;
        private readonly IUserPermissionService _permissions;
        private readonly MaintenanceTaskSupport _support;

        public GetMaintenanceTaskByIdQueryHandler(IMaintenanceTaskService taskService, IUserService userService, IUserPermissionService permissions, MaintenanceTaskSupport support)
        {
            _taskService = taskService;
            _userService = userService;
            _permissions = permissions;
            _support = support;
        }

        public async Task<MaintenanceTaskResponseDto> Handle(GetMaintenanceTaskByIdQuery request, CancellationToken ct)
        {
            var scopes = await MaintenanceRules.TaskScopesAsync(_userService, _permissions);
            var entity = await LoadAsync(request.Id);

            MaintenanceRules.Ensure(MaintenanceRules.In(scopes.View, entity), "لا يمكنك عرض مهمة خارج نطاقك");

            return _support.ToDto(entity, scopes);
        }

        private async Task<MaintenanceTask> LoadAsync(int id) =>
            await _taskService.GetByIdAsync(id) ?? throw new KeyNotFoundException("المهمة غير موجودة");
    }
}
