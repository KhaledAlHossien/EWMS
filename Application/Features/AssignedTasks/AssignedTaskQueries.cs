using System.Linq.Expressions;
using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using MediatR;

namespace Application.Features.AssignedTasks
{
    /// <summary>
    /// Mode: incoming (الواردة — أنا المنفِّذ)، outgoing (الصادرة — أنا المُسنِد)، scope (كل مهام نطاقي).
    /// المنجزة تُعرض لآخر 30 يوماً فقط حتى لا تزدحم اللوحة.
    /// </summary>
    public record GetTaskBoardQuery(string Mode, int? DoneDays = null) : IRequest<TaskBoardDto>;
    /// <summary>تصدير مهام العرض الحالي (بنفس فلاتر الواجهة) إلى Excel</summary>
    public record ExportAssignedTasksQuery(AssignedTaskExportFilterDto Filter) : IRequest<byte[]>;
    public record GetAssignedTaskQuery(int Id) : IRequest<AssignedTaskDetailDto>;
    /// <summary>
    /// الجهات التي يستطيع المستخدم الإسناد إليها من نوع معيّن (أقسام فرعه / مكاتب قسمه / موظفو مكتبه)،
    /// أو جهات التفويض من مهمة واردة (مكاتب قسم المهمة / موظفو مكتبها).
    /// </summary>
    public record GetTaskTargetsQuery(string? Type = null, int? ParentTaskId = null) : IRequest<List<TaskTargetOptionDto>>;

    public class AssignedTaskQueriesHandler :
        IRequestHandler<GetTaskBoardQuery, TaskBoardDto>,
        IRequestHandler<ExportAssignedTasksQuery, byte[]>,
        IRequestHandler<GetAssignedTaskQuery, AssignedTaskDetailDto>,
        IRequestHandler<GetTaskTargetsQuery, List<TaskTargetOptionDto>>
    {
        private readonly IAssignedTaskService _taskService;
        private readonly IUserService _userService;
        private readonly IUserPermissionService _permissions;
        private readonly IDepartmentService _departmentService;
        private readonly IOfficeService _officeService;
        private readonly IAssignedTaskSpreadsheet _spreadsheet;

        public AssignedTaskQueriesHandler(
            IAssignedTaskService taskService,
            IUserService userService,
            IUserPermissionService permissions,
            IDepartmentService departmentService,
            IOfficeService officeService,
            IAssignedTaskSpreadsheet spreadsheet)
        {
            _spreadsheet = spreadsheet;
            _taskService = taskService;
            _userService = userService;
            _permissions = permissions;
            _departmentService = departmentService;
            _officeService = officeService;
        }

        private Task<Viewer> CurrentAsync() => Viewer.CurrentAsync(_userService, _permissions);

        public async Task<TaskBoardDto> Handle(GetTaskBoardQuery request, CancellationToken ct)
        {
            var viewer = await CurrentAsync();
            var mode = (request.Mode ?? "incoming").ToLowerInvariant();
            var filter = AssignedTaskRules.ModeFilter(viewer, mode);

            // المنجزة: آخر 30 يوماً افتراضياً، ويمكن توسيعها (حتى 10 سنوات) من فلتر اللوحة
            var doneSince = DateTime.UtcNow.AddDays(-Math.Clamp(request.DoneDays ?? 30, 1, 3650));
            var tasks = await _taskService.GetBoardAsync(filter);
            var targetTypes = AssignedTaskRules.TargetTypesFor(viewer);

            return new TaskBoardDto
            {
                Mode = mode is "outgoing" or "scope" ? mode : "incoming",
                CanCreate = targetTypes.Count > 0,
                TargetTypeLabel = AssignedTaskRules.TargetTypeLabel(targetTypes.FirstOrDefault()),
                TargetTypes = targetTypes.Select(t => new TaskTargetTypeDto
                {
                    Value = t.ToString(),
                    Label = AssignedTaskRules.TargetTypeLabel(t)
                }).ToList(),
                HasScope = AssignedTaskRules.HasScope(viewer),
                Tasks = tasks
                    .Where(t => t.Status != AssignedTaskStatus.Done || (t.CompletedAt ?? t.UpdatedAt) >= doneSince)
                    .Select(t => AssignedTaskRules.ToCard(t, viewer))
                    .ToList()
            };
        }

        public async Task<byte[]> Handle(ExportAssignedTasksQuery request, CancellationToken ct)
        {
            var viewer = await CurrentAsync();
            var f = request.Filter;
            var doneSince = DateTime.UtcNow.AddDays(-Math.Clamp(f.DoneDays ?? 30, 1, 3650));
            var cards = (await _taskService.GetBoardAsync(AssignedTaskRules.ModeFilter(viewer, f.Mode)))
                .Where(t => t.Status != AssignedTaskStatus.Done || (t.CompletedAt ?? t.UpdatedAt) >= doneSince)
                .Select(t => AssignedTaskRules.ToCard(t, viewer))
                .Where(c => AssignedTaskRules.MatchesExportFilter(c, f))
                .ToList();
            return _spreadsheet.Export(cards);
        }

        public async Task<AssignedTaskDetailDto> Handle(GetAssignedTaskQuery request, CancellationToken ct)
        {
            var viewer = await CurrentAsync();
            var task = await _taskService.GetByIdAsync(request.Id)
                ?? throw new KeyNotFoundException("المهمة غير موجودة");

            if (!AssignedTaskRules.CanView(task, viewer))
                throw new UnauthorizedAccessException("لا تملك صلاحية عرض هذه المهمة");

            return AssignedTaskRules.ToDetail(task, viewer);
        }

        public async Task<List<TaskTargetOptionDto>> Handle(GetTaskTargetsQuery request, CancellationToken ct)
        {
            var viewer = await CurrentAsync();
            var user = viewer.User;

            // التفويض: الجهات أدنى بدرجة ضمن وحدة المهمة الأصل
            if (request.ParentTaskId is int parentId)
            {
                var parent = await _taskService.GetByIdAsync(parentId)
                    ?? throw new KeyNotFoundException("المهمة غير موجودة");
                if (!AssignedTaskRules.CanHandle(parent, viewer))
                    throw new UnauthorizedAccessException("يمكنك تفويض المهام الواردة إليك فقط");

                return AssignedTaskRules.DelegationTargetFor(parent) switch
                {
                    AssignedTaskTargetType.Office when parent.DepartmentId is int d => await OfficesAsync(d),
                    AssignedTaskTargetType.User when parent.OfficeId is int o => await UsersAsync(o, user.Id),
                    _ => []
                };
            }

            var types = AssignedTaskRules.TargetTypesFor(viewer);
            var type = string.IsNullOrWhiteSpace(request.Type)
                ? types.Cast<AssignedTaskTargetType?>().FirstOrDefault()
                : Enum.TryParse<AssignedTaskTargetType>(request.Type, true, out var parsed) && types.Contains(parsed) ? parsed
                : throw new UnauthorizedAccessException("لا تملك صلاحية الإسناد لهذا النوع من الجهات");

            return type switch
            {
                AssignedTaskTargetType.Department when user.BranchId is int b => await DepartmentsAsync(b),
                AssignedTaskTargetType.Office when user.DepartmentId is int d => await OfficesAsync(d),
                AssignedTaskTargetType.User when user.OfficeId is int o => await UsersAsync(o, user.Id),
                _ => []
            };
        }

        private async Task<List<TaskTargetOptionDto>> DepartmentsAsync(int branchId)
        {
            var handlers = await _permissions.GetUsersWithPermissionAsync(AppPermissions.HandleUnitTasks, branchId: branchId);
            return (await _departmentService.GetAllAsync())
                .Where(d => d.BranchId == branchId).OrderBy(d => d.Name)
                .Select(d => new TaskTargetOptionDto
                {
                    Id = d.Id,
                    Name = d.Name,
                    HeadNames = string.Join("، ", handlers
                        .Where(u => OrganizationRole.IsInDepartmentItself(u, d.Id)).Select(u => u.FullName))
                }).ToList();
        }

        private async Task<List<TaskTargetOptionDto>> OfficesAsync(int departmentId)
        {
            var handlers = await _permissions.GetUsersWithPermissionAsync(AppPermissions.HandleUnitTasks, departmentId: departmentId);
            return (await _officeService.GetByDepartmentAsync(departmentId))
                .OrderBy(o => o.Name)
                .Select(o => new TaskTargetOptionDto
                {
                    Id = o.Id,
                    Name = o.Name,
                    HeadNames = string.Join("، ", handlers.Where(u => u.OfficeId == o.Id).Select(u => u.FullName))
                }).ToList();
        }

        private async Task<List<TaskTargetOptionDto>> UsersAsync(int officeId, int excludeUserId) =>
            (await _userService.GetByOfficeAsync(officeId))
                .Where(u => u.IsActive && u.Id != excludeUserId).OrderBy(u => u.FullName)
                .Select(u => new TaskTargetOptionDto { Id = u.Id, Name = u.FullName })
                .ToList();
    }
}
