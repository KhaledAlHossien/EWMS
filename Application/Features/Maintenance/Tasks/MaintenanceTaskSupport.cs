using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities.Maintenance;

namespace Application.Features.Maintenance.Tasks
{
    /// <summary>الدوال المشتركة بين عمليات هذه الميزة</summary>
    public class MaintenanceTaskSupport
    {
        private readonly IUserService _userService;
        private readonly IUserPermissionService _permissions;
        private readonly IMapper _mapper;

        public MaintenanceTaskSupport(IUserService userService, IUserPermissionService permissions, IMapper mapper)
        {
            _userService = userService;
            _permissions = permissions;
            _mapper = mapper;
        }

        // التحويل إلى DTO مع ما يستطيعه المستخدم الحالي على المهمة
        public MaintenanceTaskResponseDto ToDto(MaintenanceTask entity, MaintenanceRules.Scopes scopes)
        {
            var dto = _mapper.Map<MaintenanceTaskResponseDto>(entity);
            dto.CanEdit = MaintenanceRules.In(scopes.Edit, entity);
            dto.CanDelete = MaintenanceRules.In(scopes.Delete, entity);
            dto.CanAssign = MaintenanceRules.In(scopes.Assign, entity);
            return dto;
        }

        public async Task<MaintenanceTaskResponseDto> ToDtoAsync(MaintenanceTask entity) =>
            ToDto(entity, await MaintenanceRules.TaskScopesAsync(_userService, _permissions));

        public static void Trim(MaintenanceTask t)
        {
            t.TaskLocation = t.TaskLocation.Trim();
            t.RequestingParty = t.RequestingParty.Trim();
            t.RequiredWork = t.RequiredWork.Trim();
            t.CompletedWorks = t.CompletedWorks.Trim();
        }
    }
}
