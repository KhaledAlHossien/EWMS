using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities.Maintenance;
using MediatR;

namespace Application.Features.Maintenance.Tasks.Commands.Update
{
    public class UpdateMaintenanceTaskCommandHandler : IRequestHandler<UpdateMaintenanceTaskCommand, MaintenanceTaskResponseDto>
    {
        private readonly IMaintenanceTaskService _taskService;
        private readonly IUserService _userService;
        private readonly IUserPermissionService _permissions;
        private readonly IMapper _mapper;
        private readonly MaintenanceTaskSupport _support;

        public UpdateMaintenanceTaskCommandHandler(IMaintenanceTaskService taskService, IUserService userService, IUserPermissionService permissions, IMapper mapper, MaintenanceTaskSupport support)
        {
            _taskService = taskService;
            _userService = userService;
            _permissions = permissions;
            _mapper = mapper;
            _support = support;
        }

        public async Task<MaintenanceTaskResponseDto> Handle(UpdateMaintenanceTaskCommand request, CancellationToken ct)
        {
            var user = await MaintenanceRules.CurrentUserAsync(_userService);
            var entity = await LoadAsync(request.Id);

            MaintenanceRules.Ensure(MaintenanceRules.In(await MaintenanceRules.BoundaryAsync(_userService, _permissions, "EditMaintenanceTask"), entity),
                "لا يمكنك تعديل مهمة خارج نطاقك");

            _mapper.Map(request.Dto, entity);
            MaintenanceTaskSupport.Trim(entity);
            entity.UpdatedAt = DateTime.UtcNow;

            await _taskService.UpdateAsync(entity);
            return await _support.ToDtoAsync(entity);
        }

        private async Task<MaintenanceTask> LoadAsync(int id) =>
            await _taskService.GetByIdAsync(id) ?? throw new KeyNotFoundException("المهمة غير موجودة");
    }
}
