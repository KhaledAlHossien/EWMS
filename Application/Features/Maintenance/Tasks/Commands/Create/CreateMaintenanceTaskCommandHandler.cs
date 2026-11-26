using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities.Maintenance;
using MediatR;

namespace Application.Features.Maintenance.Tasks.Commands.Create
{
    public class CreateMaintenanceTaskCommandHandler : IRequestHandler<CreateMaintenanceTaskCommand, MaintenanceTaskResponseDto>
    {
        private readonly IMaintenanceTaskService _taskService;
        private readonly IUserService _userService;
        private readonly INotificationService _notifications;
        private readonly IUserPermissionService _permissions;
        private readonly IMapper _mapper;
        private readonly MaintenanceTaskSupport _support;

        public CreateMaintenanceTaskCommandHandler(IMaintenanceTaskService taskService, IUserService userService, INotificationService notifications, IUserPermissionService permissions, IMapper mapper, MaintenanceTaskSupport support)
        {
            _taskService = taskService;
            _userService = userService;
            _notifications = notifications;
            _permissions = permissions;
            _mapper = mapper;
            _support = support;
        }

        public async Task<MaintenanceTaskResponseDto> Handle(CreateMaintenanceTaskCommand request, CancellationToken ct)
        {
            var user = await MaintenanceRules.CurrentUserAsync(_userService);

            var entity = _mapper.Map<MaintenanceTask>(request.Dto);
            MaintenanceTaskSupport.Trim(entity);

            // صاحب المهمة = الموظف الذي وجّهها إليه رئيس القسم، وإلا من سجّلها.
            // القسم يُحفظ لحظة التسجيل ليراها رئيس القسم
            var owner = request.Dto.AssigneeId is int assigneeId && assigneeId != user.Id
                ? await MaintenanceRules.ResolveAssigneeAsync(_userService, await MaintenanceRules.BoundaryAsync(_userService, _permissions, "AssignMaintenanceTask"), assigneeId)
                : user;

            entity.UserId = owner.Id;
            entity.DepartmentId = owner.DepartmentId;
            entity.CreatedAt = entity.UpdatedAt = DateTime.UtcNow;

            var created = await _taskService.AddAsync(entity);
            var loaded = await LoadAsync(created.Id);

            if (owner.Id != user.Id)
                await MaintenanceNotifier.TaskAssignedAsync(_notifications, loaded, user);

            return await _support.ToDtoAsync(loaded);
        }

        private async Task<MaintenanceTask> LoadAsync(int id) =>
            await _taskService.GetByIdAsync(id) ?? throw new KeyNotFoundException("المهمة غير موجودة");
    }
}
