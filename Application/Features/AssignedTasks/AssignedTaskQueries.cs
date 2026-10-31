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
        private readonly IReportLogService _reportLog;

        public AssignedTaskQueriesHandler(
            IAssignedTaskService taskService,
            IUserService userService,
            IUserPermissionService permissions,
            IDepartmentService departmentService,
            IOfficeService officeService,
            IAssignedTaskSpreadsheet spreadsheet,
            IReportLogService reportLog)
        {
            _reportLog = reportLog;
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
            var content = _spreadsheet.Export(cards);
            await _reportLog.AddAsync(Reports.ReportRules.Log(viewer, Reports.ReportHandler.TasksKey, Reports.ReportHandler.TasksTitle,
                ReportFormat.Excel, ExportFiltersText(f), cards.Count));
            return content;
        }

        /// <summary>نص فلاتر تصدير اللوحة لسجل التقارير</summary>
        private static string ExportFiltersText(AssignedTaskExportFilterDto f)
        {
            var parts = new List<string>
            {
                "العرض: " + ((f.Mode ?? "incoming").ToLowerInvariant() switch { "outgoing" => "الصادرة مني", "scope" => "ضمن نطاقي", _ => "الواردة إليّ" }),
                $"المنجزة: آخر {Math.Clamp(f.DoneDays ?? 30, 1, 3650)} يوماً"
            };
            if (!string.IsNullOrWhiteSpace(f.Q)) parts.Add($"بحث: {f.Q.Trim()}");
            if (Enum.TryParse<AssignedTaskPriority>(f.Priority, true, out var priority)) parts.Add($"الأولوية: {AssignedTaskRules.PriorityAr(priority)}");
            if (f.OverdueOnly) parts.Add("المتأخرة فقط");
            if (f.DueFrom != null) parts.Add($"الاستحقاق من: {f.DueFrom:yyyy-MM-dd}");
            if (f.DueTo != null) parts.Add($"الاستحقاق إلى: {f.DueTo:yyyy-MM-dd}");
            return string.Join(" · ", parts);
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

        /// <summary>
        /// جهات الإسناد في قائمة واحدة مجمّعة (أقسام ثم مكاتب ثم موظفون) بحسب ما تسمح به صلاحياتي، وكل عنصر يحمل نوعه (Kind) فيستنتج
        /// الخادم النوع من الجهة المختارة ولا يختاره المستخدم. type اختياري (لتقييد القائمة بنوع واحد)، وparentTaskId لجهات التفويض.
        /// </summary>
        public async Task<List<TaskTargetOptionDto>> Handle(GetTaskTargetsQuery request, CancellationToken ct)
        {
            var viewer = await CurrentAsync();

            AssignedTask? parent = null;
            List<AssignedTaskTargetType> allowed;
            if (request.ParentTaskId is int parentId)
            {
                // التفويض: الجهات أدنى بدرجة ضمن وحدة المهمة الأصل
                parent = await _taskService.GetByIdAsync(parentId) ?? throw new KeyNotFoundException("المهمة غير موجودة");
                if (!AssignedTaskRules.CanHandle(parent, viewer))
                    throw new UnauthorizedAccessException("يمكنك تفويض المهام الواردة إليك فقط");
                allowed = AssignedTaskRules.DelegationTypesFor(parent, viewer);
            }
            else allowed = AssignedTaskRules.TargetTypesFor(viewer);

            List<AssignedTaskTargetType> wanted = string.IsNullOrWhiteSpace(request.Type) ? allowed
                : Enum.TryParse<AssignedTaskTargetType>(request.Type, true, out var asked) && allowed.Contains(asked) ? [asked]
                : throw new UnauthorizedAccessException(parent == null ? "لا تملك صلاحية الإسناد لهذا النوع من الجهات" : "لا تملك صلاحية التفويض لهذا النوع من الجهات");

            var result = new List<TaskTargetOptionDto>();
            foreach (var type in wanted)
            {
                var options = await OptionsAsync(viewer, parent, type);
                foreach (var option in options) option.Kind = type.ToString();
                result.AddRange(options);
            }
            return result;
        }

        private Task<List<TaskTargetOptionDto>> OptionsAsync(Viewer viewer, AssignedTask? parent, AssignedTaskTargetType type)
        {
            var user = viewer.User;
            if (parent != null)
            {
                return type switch
                {
                    AssignedTaskTargetType.Office when parent.DepartmentId is int d => OfficesAsync(d),
                    AssignedTaskTargetType.User when parent.TargetType == AssignedTaskTargetType.Office && parent.OfficeId is int o => UsersAsync(o, user.Id),
                    AssignedTaskTargetType.User when parent.DepartmentId is int d => UsersOfDepartmentAsync(d, user.Id),
                    _ => Task.FromResult(new List<TaskTargetOptionDto>())
                };
            }
            return type switch
            {
                AssignedTaskTargetType.Department when user.BranchId is int b => DepartmentsAsync(b),
                AssignedTaskTargetType.Office when user.DepartmentId is int d => OfficesAsync(d),
                AssignedTaskTargetType.User => AssignableUsersAsync(viewer),
                _ => Task.FromResult(new List<TaskTargetOptionDto>())
            };
        }

        /// <summary>موظفو حدّي: يُجمع الأوسع من صلاحياتي (الفرع ثم القسم ثم المكتب) ثم يُفلتر بقاعدة الإسناد نفسها</summary>
        private async Task<List<TaskTargetOptionDto>> AssignableUsersAsync(Viewer viewer)
        {
            var me = viewer.User;
            var candidates = viewer.Has(AppPermissions.AssignTaskToBranchUser) && me.BranchId is int b ? await _userService.GetByBranchAsync(b)
                : viewer.Has(AppPermissions.AssignTaskToDepartmentUser) && me.DepartmentId is int d ? await _userService.GetByDepartmentAsync(d)
                : me.OfficeId is int o ? await _userService.GetByOfficeAsync(o)
                : [];
            return candidates.Where(u => AssignedTaskRules.CanAssignToUser(viewer, u)).OrderBy(u => u.FullName).Select(ToUserOption).ToList();
        }

        private async Task<List<TaskTargetOptionDto>> UsersOfDepartmentAsync(int departmentId, int excludeUserId) =>
            (await _userService.GetByDepartmentAsync(departmentId))
                .Where(u => u.IsActive && u.Id != excludeUserId).OrderBy(u => u.FullName).Select(ToUserOption).ToList();

        private static TaskTargetOptionDto ToUserOption(User u) => new()
        {
            Id = u.Id,
            Name = u.FullName,
            Detail = string.Join(" / ", new[] { u.Department?.Name, u.Office?.Name }.Where(x => !string.IsNullOrEmpty(x)))
        };

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
