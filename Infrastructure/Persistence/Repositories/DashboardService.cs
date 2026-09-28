using System.Linq.Expressions;
using Application.DTOs.Response;
using Application.Features.Users;
using Application.Features.Vacations;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    public class DashboardService : IDashboardService
    {
        private const int ListSize = 10;
        private const int UpcomingDays = 7;

        private readonly DataContext _context;
        private readonly IVacationService _vacationService;

        // التواريخ بتوقيت الخادم (StartVac/EndVac تواريخ بلا وقت)
        private readonly DateTime _today = DateTime.Today;
        private DateTime Tomorrow => _today.AddDays(1);
        private DateTime UpcomingEnd => _today.AddDays(UpcomingDays + 1);
        private DateTime MonthStart => new(_today.Year, _today.Month, 1);
        private DateTime YearStart => new(_today.Year, 1, 1);

        public DashboardService(DataContext context, IVacationService vacationService)
        {
            _context = context;
            _vacationService = vacationService;
        }

        private IQueryable<User> ActiveUsers => _context.Users.AsNoTracking().Where(u => u.IsActive);
        private IQueryable<Vacation> Vacations => _context.Vacation.AsNoTracking();

        /// <summary>موظف "لديه مهام": مسنَد لمهمة فعّالة من فرعه الحالي</summary>
        private IQueryable<User> WithTasks(IQueryable<User> users) =>
            users.Where(u => _context.UserWorkTasks.Any(a =>
                a.UserId == u.Id && a.WorkTask.IsActive && a.WorkTask.BranchId == u.BranchId));

        // ════════════════════ مدير النظام: المؤسسة ════════════════════

        public async Task<OverviewDashboardDto> GetOverviewAsync()
        {
            var staff = ActiveUsers.Where(u => u.BranchId != null); // SuperAdmin لا يتبع لفرع
            var branches = await _context.Branches.AsNoTracking().OrderBy(b => b.Name).ToListAsync();

            var deptsPerBranch = await _context.Departments.GroupBy(d => d.BranchId)
                .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);
            var officesPerBranch = await _context.Offices.GroupBy(o => o.Department.BranchId)
                .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);
            var usersPerBranch = await staff.GroupBy(u => u.BranchId!.Value)
                .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);
            var tasksPerBranch = await _context.WorkTasks.Where(t => t.IsActive).GroupBy(t => t.BranchId)
                .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);
            var branchManagers = await ManagersByAsync(staff, "BranchManager", u => u.BranchId!.Value);

            return new OverviewDashboardDto
            {
                BranchesCount = branches.Count,
                DepartmentsCount = deptsPerBranch.Values.Sum(),
                OfficesCount = officesPerBranch.Values.Sum(),
                EmployeesCount = usersPerBranch.Values.Sum(),
                WorkTasksCount = tasksPerBranch.Values.Sum(),
                EmployeesWithTasks = await WithTasks(staff).CountAsync(),
                Branches = branches.Select(b => new BranchSummaryDto
                {
                    Id = b.Id,
                    Name = b.Name,
                    ManagerNames = branchManagers.GetValueOrDefault(b.Id, ""),
                    DepartmentsCount = deptsPerBranch.GetValueOrDefault(b.Id),
                    OfficesCount = officesPerBranch.GetValueOrDefault(b.Id),
                    EmployeesCount = usersPerBranch.GetValueOrDefault(b.Id),
                    TasksCount = tasksPerBranch.GetValueOrDefault(b.Id)
                }).ToList(),
                EmployeesByRole = await EmployeesByRoleAsync(staff),
                RecentActivity = await RecentActivityAsync(u => u.BranchId != null, branchId: null, includeTaskCreation: true)
            };
        }

        // ════════════════════ رئيس الفرع ════════════════════

        public async Task<BranchDashboardDto?> GetBranchAsync(int branchId)
        {
            var branch = await _context.Branches.AsNoTracking().FirstOrDefaultAsync(b => b.Id == branchId);
            if (branch == null) return null;

            var users = ActiveUsers.Where(u => u.BranchId == branchId);

            var departments = await _context.Departments.AsNoTracking()
                .Where(d => d.BranchId == branchId).OrderBy(d => d.Name).ToListAsync();
            var officesPerDept = await _context.Offices.Where(o => o.Department.BranchId == branchId)
                .GroupBy(o => o.DepartmentId).Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count);
            var usersPerDept = await users.Where(u => u.DepartmentId != null).GroupBy(u => u.DepartmentId!.Value)
                .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);
            var withTasksPerDept = await WithTasks(users).Where(u => u.DepartmentId != null).GroupBy(u => u.DepartmentId!.Value)
                .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);
            var deptManagers = await ManagersByAsync(users.Where(u => u.DepartmentId != null), "Manager", u => u.DepartmentId!.Value);

            var tasks = await _context.WorkTasks.AsNoTracking()
                .Where(t => t.BranchId == branchId && t.IsActive)
                .OrderBy(t => t.Name)
                .Select(t => new WorkTaskCardDto
                {
                    Id = t.Id,
                    Name = t.Name,
                    Description = t.Description,
                    Icon = t.Icon,
                    BranchName = t.Branch.Name,
                    AssigneesCount = t.Assignments.Count
                }).ToListAsync();

            return new BranchDashboardDto
            {
                BranchId = branch.Id,
                BranchName = branch.Name,
                ManagerNames = await ManagerNamesAsync(users, "BranchManager"),
                DepartmentsCount = departments.Count,
                OfficesCount = officesPerDept.Values.Sum(),
                EmployeesCount = await users.CountAsync(),
                TasksCount = tasks.Count,
                EmployeesWithTasks = await WithTasks(users).CountAsync(),
                Tasks = tasks,
                Departments = departments.Select(d => new DepartmentSummaryDto
                {
                    Id = d.Id,
                    Name = d.Name,
                    ManagerNames = deptManagers.GetValueOrDefault(d.Id, ""),
                    OfficesCount = officesPerDept.GetValueOrDefault(d.Id),
                    EmployeesCount = usersPerDept.GetValueOrDefault(d.Id),
                    EmployeesWithTasks = withTasksPerDept.GetValueOrDefault(d.Id)
                }).ToList(),
                TaskDistribution = await TaskDistributionAsync(branchId, u => u.BranchId == branchId),
                EmployeesByRole = await EmployeesByRoleAsync(users),
                RecentActivity = await RecentActivityAsync(u => u.BranchId == branchId, branchId, includeTaskCreation: true)
            };
        }

        // ════════════════════ رئيس القسم ════════════════════

        public async Task<DepartmentDashboardDto?> GetDepartmentAsync(int departmentId)
        {
            var department = await _context.Departments.AsNoTracking().Include(d => d.Branch)
                .FirstOrDefaultAsync(d => d.Id == departmentId);
            if (department == null) return null;

            var users = ActiveUsers.Where(u => u.DepartmentId == departmentId);

            var offices = await _context.Offices.AsNoTracking()
                .Where(o => o.DepartmentId == departmentId).OrderBy(o => o.Name).ToListAsync();
            var usersPerOffice = await users.Where(u => u.OfficeId != null).GroupBy(u => u.OfficeId!.Value)
                .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);
            var withTasksPerOffice = await WithTasks(users).Where(u => u.OfficeId != null).GroupBy(u => u.OfficeId!.Value)
                .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);
            var officeManagers = await ManagersByAsync(users.Where(u => u.OfficeId != null), "OfficeManager", u => u.OfficeId!.Value);

            var distribution = await TaskDistributionAsync(department.BranchId, u => u.DepartmentId == departmentId);
            var employeesCount = await users.CountAsync();

            return new DepartmentDashboardDto
            {
                DepartmentId = department.Id,
                DepartmentName = department.Name,
                BranchId = department.BranchId,
                BranchName = department.Branch?.Name ?? "",
                ManagerNames = await ManagerNamesAsync(users, "Manager"),
                OfficesCount = offices.Count,
                EmployeesCount = employeesCount,
                TasksCount = distribution.Count,
                EmployeesWithoutTasks = employeesCount - await WithTasks(users).CountAsync(),
                Offices = offices.Select(o => new OfficeSummaryDto
                {
                    Id = o.Id,
                    Name = o.Name,
                    ManagerNames = officeManagers.GetValueOrDefault(o.Id, ""),
                    EmployeesCount = usersPerOffice.GetValueOrDefault(o.Id),
                    EmployeesWithTasks = withTasksPerOffice.GetValueOrDefault(o.Id)
                }).ToList(),
                TaskDistribution = distribution,
                RecentActivity = await RecentActivityAsync(u => u.DepartmentId == departmentId, department.BranchId, includeTaskCreation: false)
            };
        }

        // ════════════════════ رئيس المكتب ════════════════════

        public async Task<OfficeDashboardDto?> GetOfficeAsync(int officeId)
        {
            var office = await _context.Offices.AsNoTracking()
                .Include(o => o.Department).ThenInclude(d => d.Branch)
                .FirstOrDefaultAsync(o => o.Id == officeId);
            if (office == null) return null;

            var branchId = office.Department?.BranchId ?? 0;
            var users = ActiveUsers.Where(u => u.OfficeId == officeId);

            var members = await users.OrderBy(u => u.FullName)
                .Select(u => new { u.Id, u.FullName, u.Email, RoleName = u.Role.Name, u.CreatedAt })
                .ToListAsync();
            var memberIds = members.Select(m => m.Id).ToList();
            var assignments = await _context.UserWorkTasks.AsNoTracking()
                .Where(a => memberIds.Contains(a.UserId) && a.WorkTask.IsActive && a.WorkTask.BranchId == branchId)
                .Select(a => new { a.UserId, a.WorkTask.Name })
                .ToListAsync();

            var distribution = await TaskDistributionAsync(branchId, u => u.OfficeId == officeId);
            var withTasks = assignments.Select(a => a.UserId).Distinct().Count();

            return new OfficeDashboardDto
            {
                OfficeId = office.Id,
                OfficeName = office.Name,
                DepartmentId = office.DepartmentId,
                DepartmentName = office.Department?.Name ?? "",
                BranchId = branchId,
                BranchName = office.Department?.Branch?.Name ?? "",
                ManagerNames = string.Join("، ", members.Where(m => m.RoleName == "OfficeManager").Select(m => m.FullName)),
                EmployeesCount = members.Count,
                TasksCount = distribution.Count,
                EmployeesWithTasks = withTasks,
                EmployeesWithoutTasks = members.Count - withTasks,
                Members = members.Select(m => new OfficeMemberDto
                {
                    UserId = m.Id,
                    FullName = m.FullName,
                    Email = m.Email,
                    RoleName = m.RoleName,
                    JoinedAt = m.CreatedAt,
                    TaskNames = assignments.Where(a => a.UserId == m.Id).Select(a => a.Name).OrderBy(n => n).ToList()
                }).ToList(),
                TaskDistribution = distribution,
                RecentActivity = await RecentActivityAsync(u => u.OfficeId == officeId, branchId, includeTaskCreation: false)
            };
        }

        // ════════════════════ الموظف ════════════════════

        public async Task<EmployeeDashboardDto?> GetEmployeeAsync(int userId)
        {
            var user = await _context.Users.AsNoTracking()
                .Include(u => u.Role).Include(u => u.Branch).Include(u => u.Department).Include(u => u.Office)
                .FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null) return null;

            var limit = VacationRules.MaxPaidVacationDaysPerMonth;
            var canRequest = UserPlacement.For(user.Role?.Name ?? "").NeedsDepartment;
            var paidUsed = canRequest
                ? await _vacationService.GetPaidVacationDaysInMonthAsync(userId, _today.Year, _today.Month)
                : 0;

            // الفريق: زملاء المكتب، أو القسم لمن لا مكتب له (رئيس القسم)
            IQueryable<User>? team = user.OfficeId != null ? ActiveUsers.Where(u => u.OfficeId == user.OfficeId)
                : user.DepartmentId != null ? ActiveUsers.Where(u => u.DepartmentId == user.DepartmentId)
                : null;

            return new EmployeeDashboardDto
            {
                FullName = user.FullName,
                RoleName = user.Role?.Name ?? "",
                BranchName = user.Branch?.Name ?? "",
                DepartmentName = user.Department?.Name ?? "",
                OfficeName = user.Office?.Name ?? "",
                JoinedAt = user.CreatedAt,
                CanRequestVacation = canRequest,
                PaidDaysLimitPerMonth = limit,
                PaidDaysLeftThisMonth = Math.Max(0, limit - paidUsed),
                TeamName = user.Office?.Name ?? user.Department?.Name ?? "",
                Team = team == null ? [] : await team.Where(u => u.Id != userId).OrderBy(u => u.FullName)
                    .Select(u => new TeamMemberDto { FullName = u.FullName, RoleName = u.Role.Name, Email = u.Email })
                    .ToListAsync()
            };
        }

        // ════════════════════ صفحة إحصائيات الإجازات ════════════════════

        public async Task<VacationStatsDto?> GetVacationStatsAsync(DashboardScope scope, int? id)
        {
            IQueryable<Vacation> vacations;
            string scopeName;
            string pendingLabel;
            IQueryable<Vacation> pendingMine;

            switch (scope)
            {
                case DashboardScope.Branch:
                    var branch = await _context.Branches.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id);
                    if (branch == null) return null;
                    vacations = Vacations.Where(v => v.BranchId == branch.Id);
                    scopeName = branch.Name;
                    pendingLabel = "بانتظار اعتماد رئيس الفرع";
                    pendingMine = vacations.Where(v => v.Status == VacationStatus.PendingBranchManager);
                    break;
                case DashboardScope.Department:
                    var department = await _context.Departments.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id);
                    if (department == null) return null;
                    vacations = Vacations.Where(v => v.DepartmentId == department.Id);
                    scopeName = department.Name;
                    pendingLabel = "بانتظار موافقة رئيس القسم";
                    pendingMine = vacations.Where(v => v.Status == VacationStatus.PendingManager);
                    break;
                case DashboardScope.Office:
                    var office = await _context.Offices.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id);
                    if (office == null) return null;
                    // الإجازة لا تخزّن المكتب — نربطها عبر مكتب الموظف الحالي
                    vacations = Vacations.Where(v => v.User.OfficeId == office.Id);
                    scopeName = office.Name;
                    pendingLabel = "طلبات موظفي المكتب قيد الموافقة";
                    pendingMine = vacations.Where(v => v.Status == VacationStatus.PendingManager || v.Status == VacationStatus.PendingBranchManager);
                    break;
                default:
                    vacations = Vacations;
                    scopeName = "كل المؤسسة";
                    pendingLabel = "كل الطلبات قيد الموافقة";
                    pendingMine = vacations.Where(v => v.Status == VacationStatus.PendingManager || v.Status == VacationStatus.PendingBranchManager);
                    break;
            }

            return new VacationStatsDto
            {
                ScopeType = scope.ToString(),
                ScopeName = scopeName,
                OnLeaveToday = await OnLeaveToday(vacations).Select(v => v.UserId).Distinct().CountAsync(),
                UpcomingLeavesCount = await vacations.CountAsync(v =>
                    v.Status == VacationStatus.Approved && v.StartVac >= Tomorrow && v.StartVac < UpcomingEnd),
                PendingRequests = await vacations.CountAsync(v =>
                    v.Status == VacationStatus.PendingManager || v.Status == VacationStatus.PendingBranchManager),
                ApprovedDaysThisMonth = await ApprovedDaysThisMonthAsync(vacations),
                PendingStageLabel = pendingLabel,
                PendingApprovals = await ToRowsAsync(pendingMine.OrderBy(v => v.CreatedAt).Take(ListSize)),
                OnLeave = await ToRowsAsync(vacations
                    .Where(v => v.Status == VacationStatus.Approved && v.StartVac < UpcomingEnd && v.EndVac >= _today)
                    .OrderBy(v => v.StartVac).Take(15)),
                RecentActions = await RecentVacationActionsAsync(vacations),
                VacationsByType = await VacationsByTypeAsync(vacations)
            };
        }

        // ════════════════════ دوال مساعدة: عامة ════════════════════

        /// <summary>الموظفون حسب الدور (اسم الدور كما في النظام — الواجهة تترجمه)</summary>
        private static async Task<List<DashboardCountItemDto>> EmployeesByRoleAsync(IQueryable<User> users)
        {
            return await users.GroupBy(u => u.Role.Name)
                .Select(g => new DashboardCountItemDto { Label = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToListAsync();
        }

        /// <summary>
        /// آخر الإجراءات العامة: انضمام موظفين، إسناد مهام، وإضافة مهام (لنطاق الفرع/المؤسسة).
        /// </summary>
        private async Task<List<DashboardActivityDto>> RecentActivityAsync(
            Expression<Func<User, bool>> inScope, int? branchId, bool includeTaskCreation)
        {
            var joined = await _context.Users.AsNoTracking().Where(inScope)
                .OrderByDescending(u => u.CreatedAt).Take(ListSize)
                .Select(u => new
                {
                    u.FullName,
                    u.CreatedAt,
                    Place = u.Office != null ? u.Office.Name : u.Department != null ? u.Department.Name : u.Branch != null ? u.Branch.Name : ""
                }).ToListAsync();

            var scopedUserIds = _context.Users.Where(inScope).Select(u => u.Id);
            var assigned = await _context.UserWorkTasks.AsNoTracking()
                .Where(a => scopedUserIds.Contains(a.UserId))
                .OrderByDescending(a => a.AssignedAt).Take(ListSize)
                .Select(a => new { a.User.FullName, TaskName = a.WorkTask.Name, a.WorkTask.Icon, a.AssignedAt, BranchName = a.WorkTask.Branch.Name })
                .ToListAsync();

            var activity = joined.Select(j => new DashboardActivityDto
            {
                Type = "UserJoined",
                Icon = "👤",
                Text = $"انضم {j.FullName} إلى النظام",
                Scope = j.Place,
                Date = j.CreatedAt
            }).Concat(assigned.Select(a => new DashboardActivityDto
            {
                Type = "TaskAssigned",
                Icon = string.IsNullOrEmpty(a.Icon) ? "📋" : a.Icon,
                Text = $"أُسندت مهمة «{a.TaskName}» إلى {a.FullName}",
                Scope = a.BranchName,
                Date = a.AssignedAt
            }));

            if (includeTaskCreation)
            {
                var created = await _context.WorkTasks.AsNoTracking()
                    .Where(t => branchId == null || t.BranchId == branchId)
                    .OrderByDescending(t => t.CreatedAt).Take(ListSize)
                    .Select(t => new { t.Name, t.Icon, t.CreatedAt, BranchName = t.Branch.Name })
                    .ToListAsync();

                activity = activity.Concat(created.Select(t => new DashboardActivityDto
                {
                    Type = "TaskCreated",
                    Icon = string.IsNullOrEmpty(t.Icon) ? "📋" : t.Icon,
                    Text = $"أُضيفت مهمة عمل «{t.Name}»",
                    Scope = t.BranchName,
                    Date = t.CreatedAt
                }));
            }

            return activity.OrderByDescending(a => a.Date).Take(ListSize).ToList();
        }

        /// <summary>مهام الفرع الفعّالة المسنَدة لموظفين ضمن النطاق</summary>
        private async Task<List<WorkTaskDistributionDto>> TaskDistributionAsync(
            int branchId, Expression<Func<User, bool>> inScope)
        {
            var scopedUserIds = ActiveUsers.Where(inScope).Select(u => u.Id);

            var rows = await _context.UserWorkTasks.AsNoTracking()
                .Where(a => a.WorkTask.BranchId == branchId && a.WorkTask.IsActive && scopedUserIds.Contains(a.UserId))
                .Select(a => new { a.WorkTaskId, a.WorkTask.Name, a.WorkTask.Icon, a.User.FullName })
                .ToListAsync();

            return rows.GroupBy(r => new { r.WorkTaskId, r.Name, r.Icon })
                .OrderBy(g => g.Key.Name)
                .Select(g => new WorkTaskDistributionDto
                {
                    TaskId = g.Key.WorkTaskId,
                    TaskName = g.Key.Name,
                    Icon = g.Key.Icon,
                    AssigneeNames = g.Select(r => r.FullName).OrderBy(n => n).ToList()
                }).ToList();
        }

        private static async Task<string> ManagerNamesAsync(IQueryable<User> users, string roleName)
        {
            var names = await users.Where(u => u.Role.Name == roleName).Select(u => u.FullName).ToListAsync();
            return string.Join("، ", names);
        }

        private static async Task<Dictionary<int, string>> ManagersByAsync(
            IQueryable<User> users, string roleName, Func<User, int> key)
        {
            var list = await users.Where(u => u.Role.Name == roleName).ToListAsync();
            return list.GroupBy(key).ToDictionary(g => g.Key, g => string.Join("، ", g.Select(u => u.FullName)));
        }

        // ════════════════════ دوال مساعدة: الإجازات ════════════════════

        private IQueryable<Vacation> OnLeaveToday(IQueryable<Vacation> q) =>
            q.Where(v => v.Status == VacationStatus.Approved && v.StartVac < Tomorrow && v.EndVac >= _today);

        /// <summary>أيام الإجازات المعتمدة الواقعة داخل الشهر الحالي (الجزء المتداخل فقط)</summary>
        private async Task<int> ApprovedDaysThisMonthAsync(IQueryable<Vacation> q)
        {
            var monthEnd = MonthStart.AddMonths(1).AddDays(-1);
            var list = await q.Where(v => v.Status == VacationStatus.Approved && v.StartVac <= monthEnd && v.EndVac >= MonthStart)
                .Select(v => new { v.StartVac, v.EndVac }).ToListAsync();
            return list.Sum(v => OverlapDays(v.StartVac, v.EndVac, MonthStart, monthEnd));
        }

        private async Task<List<DashboardCountItemDto>> VacationsByTypeAsync(IQueryable<Vacation> q)
        {
            var list = await q.Where(v => v.Status == VacationStatus.Approved && v.StartVac >= YearStart)
                .Select(v => new { TypeName = v.VacationType.Name, v.VacDayCount }).ToListAsync();
            return list.GroupBy(v => v.TypeName)
                .Select(g => new DashboardCountItemDto { Label = g.Key, Count = g.Count(), Days = g.Sum(v => v.VacDayCount) })
                .OrderByDescending(x => x.Days).ToList();
        }

        private static async Task<List<DashboardVacationRowDto>> ToRowsAsync(IQueryable<Vacation> q)
        {
            var raw = await q.Select(v => new
            {
                v.Id,
                EmployeeName = v.User.FullName,
                DepartmentName = v.Department.Name,
                OfficeName = v.User.Office != null ? v.User.Office.Name : "",
                VacationTypeName = v.VacationType.Name,
                v.StartVac,
                v.EndVac,
                v.VacDayCount,
                v.Status,
                v.IsPaid
            }).ToListAsync();

            return raw.Select(r => new DashboardVacationRowDto
            {
                Id = r.Id,
                EmployeeName = r.EmployeeName,
                DepartmentName = r.DepartmentName,
                OfficeName = r.OfficeName,
                VacationTypeName = r.VacationTypeName,
                StartVac = r.StartVac,
                EndVac = r.EndVac,
                VacDayCount = r.VacDayCount,
                Status = r.Status.ToString(),
                StatusAr = VacationRules.StatusAr(r.Status),
                IsPaid = r.IsPaid
            }).ToList();
        }

        /// <summary>آخر الإجراءات على طلبات الإجازة — مستنتجة من الحالة وآخر تحديث</summary>
        private static async Task<List<DashboardActionDto>> RecentVacationActionsAsync(IQueryable<Vacation> q)
        {
            var raw = await q.OrderByDescending(v => v.UpdatedAt).Take(ListSize).Select(v => new
            {
                v.Id,
                EmployeeName = v.User.FullName,
                DepartmentName = v.Department.Name,
                v.Status,
                v.ManagerAccept,
                v.UpdatedAt,
                v.RejectedAt,
                RejectedBy = v.RejectedByUser != null ? v.RejectedByUser.FullName : null
            }).ToListAsync();

            return raw.Select(r =>
            {
                var (type, text) = r.Status switch
                {
                    VacationStatus.PendingManager => ("Submitted", "قدّم طلب إجازة — بانتظار رئيس القسم"),
                    VacationStatus.PendingBranchManager when r.ManagerAccept => ("Forwarded", "وافق رئيس القسم — بانتظار اعتماد رئيس الفرع"),
                    VacationStatus.PendingBranchManager => ("Submitted", "قدّم طلب إجازة — بانتظار اعتماد رئيس الفرع"),
                    VacationStatus.Approved => ("Approved", "اعتُمدت الإجازة نهائياً"),
                    VacationStatus.Rejected => ("Rejected", "رُفض طلب الإجازة"),
                    VacationStatus.Cancelled => ("Cancelled", "ألغى الموظف طلبه"),
                    _ => ("Unknown", "تحديث على الطلب")
                };
                return new DashboardActionDto
                {
                    VacationId = r.Id,
                    EmployeeName = r.EmployeeName,
                    DepartmentName = r.DepartmentName,
                    ActionType = type,
                    Action = text,
                    ByName = r.Status == VacationStatus.Rejected ? r.RejectedBy : null,
                    Date = r.Status == VacationStatus.Rejected && r.RejectedAt != null ? r.RejectedAt.Value : r.UpdatedAt
                };
            }).ToList();
        }

        private static int OverlapDays(DateTime start, DateTime end, DateTime rangeStart, DateTime rangeEnd)
        {
            var from = start.Date > rangeStart ? start.Date : rangeStart;
            var to = end.Date < rangeEnd ? end.Date : rangeEnd;
            return to < from ? 0 : (to - from).Days + 1;
        }
    }
}
