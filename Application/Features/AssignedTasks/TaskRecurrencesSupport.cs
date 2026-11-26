using Application.Common;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;

namespace Application.Features.AssignedTasks
{
    /// <summary>الدوال المشتركة بين عمليات هذه الميزة</summary>
    public class TaskRecurrencesSupport
    {
        private readonly IAssignedTaskPlanningService _planning;
        private readonly IUserService _userService;
        private readonly IUserPermissionService _permissions;
        private readonly IDepartmentService _departments;
        private readonly IOfficeService _offices;

        public TaskRecurrencesSupport(IAssignedTaskPlanningService planning, IUserService userService, IUserPermissionService permissions, IDepartmentService departments, IOfficeService offices)
        {
            _planning = planning;
            _userService = userService;
            _permissions = permissions;
            _departments = departments;
            _offices = offices;
        }

        public async Task<Viewer> AssignerAsync()
        {
            var viewer = await Viewer.CurrentAsync(_userService, _permissions);
            if (AssignedTaskRules.TargetTypesFor(viewer).Count == 0)
                throw new UnauthorizedAccessException("القوالب والمهام الدورية لمن يملك صلاحية إسناد المهام");
            return viewer;
        }

        public async Task<string> TargetNameAsync(AssignedTaskTargetType type, int id) => type switch
        {
            AssignedTaskTargetType.Department => (await _departments.GetByIdAsync(id))?.Name ?? "(قسم محذوف)",
            AssignedTaskTargetType.Office => (await _offices.GetByIdAsync(id))?.Name ?? "(مكتب محذوف)",
            _ => (await _userService.GetByIdAsync(id))?.FullName ?? "(موظف محذوف)"
        };

        public async Task<AssignedTaskRecurrence> OwnedAsync(int id, Viewer viewer)
        {
            var r = await _planning.GetRecurrenceAsync(id) ?? throw new KeyNotFoundException("المهمة الدورية غير موجودة");
            if (r.OwnerUserId != viewer.Id) throw new UnauthorizedAccessException("المهمة الدورية لصاحبها فقط");
            return r;
        }
    }
}
