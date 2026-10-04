namespace Application.DTOs.Response
{
    // ════════════════════════════════════════════════════════════════════
    // لوحات المتابعة = إحصائيات عامة للمؤسسة (الهيكل، الكادر، مهام العمل، النشاط).
    // إحصائيات الإجازات لها صفحة مستقلة للرؤساء (VacationStatsDto) — قرار المستخدم 2026-09-28:
    // التطبيق منصة عامة لمؤسسة متعددة الفروع والمهام، والإجازات ميزة واحدة منه.
    // ════════════════════════════════════════════════════════════════════

    /// <summary>عنصر إحصائي بعدد (مثل الموظفين حسب الدور، الإجازات حسب النوع)</summary>
    public class DashboardCountItemDto
    {
        public string Label { get; set; } = string.Empty;
        public int Count { get; set; }
        public int Days { get; set; }
    }

    /// <summary>
    /// "آخر الإجراءات" العامة في النطاق: انضمام موظف، إضافة مهمة عمل، إسناد مهمة.
    /// لا يوجد سجل تدقيق منفصل — تُستنتج من تواريخ الإنشاء والإسناد.
    /// </summary>
    public class DashboardActivityDto
    {
        public string Type { get; set; } = string.Empty; // UserJoined | TaskCreated | TaskAssigned
        public string Icon { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string Scope { get; set; } = string.Empty; // الفرع / القسم / المكتب المعني
        public DateTime Date { get; set; }
    }

    /// <summary>توزيع مهام العمل على موظفي النطاق</summary>
    public class WorkTaskDistributionDto
    {
        public int TaskId { get; set; }
        public string TaskName { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public List<string> AssigneeNames { get; set; } = [];
    }

    // ════════════════════ مدير النظام: المؤسسة ════════════════════

    public class OverviewDashboardDto
    {
        public int BranchesCount { get; set; }
        public int DepartmentsCount { get; set; }
        public int OfficesCount { get; set; }
        public int EmployeesCount { get; set; }
        public int WorkTasksCount { get; set; }
        public int EmployeesWithTasks { get; set; }

        public List<BranchSummaryDto> Branches { get; set; } = [];
        public List<DashboardCountItemDto> EmployeesByRole { get; set; } = [];
        public List<DashboardActivityDto> RecentActivity { get; set; } = [];
    }

    public class BranchSummaryDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string ManagerNames { get; set; } = string.Empty;
        public int DepartmentsCount { get; set; }
        public int OfficesCount { get; set; }
        public int EmployeesCount { get; set; }
        public int TasksCount { get; set; }
    }

    // ════════════════════ رئيس الفرع ════════════════════

    public class BranchDashboardDto
    {
        public int BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public string ManagerNames { get; set; } = string.Empty;

        public int DepartmentsCount { get; set; }
        public int OfficesCount { get; set; }
        public int EmployeesCount { get; set; }
        public int TasksCount { get; set; }
        public int EmployeesWithTasks { get; set; }

        public List<WorkTaskCardDto> Tasks { get; set; } = [];
        public List<DepartmentSummaryDto> Departments { get; set; } = [];
        public List<WorkTaskDistributionDto> TaskDistribution { get; set; } = [];
        public List<DashboardCountItemDto> EmployeesByRole { get; set; } = [];
        public List<DashboardActivityDto> RecentActivity { get; set; } = [];
    }

    public class DepartmentSummaryDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string ManagerNames { get; set; } = string.Empty;
        public int OfficesCount { get; set; }
        public int EmployeesCount { get; set; }
        public int EmployeesWithTasks { get; set; }
    }

    // ════════════════════ رئيس القسم ════════════════════

    public class DepartmentDashboardDto
    {
        public int DepartmentId { get; set; }
        public string DepartmentName { get; set; } = string.Empty;
        public int BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public string ManagerNames { get; set; } = string.Empty;

        public int OfficesCount { get; set; }
        public int EmployeesCount { get; set; }
        public int TasksCount { get; set; }            // مهام العمل المسنَدة لموظفي القسم (بدون تكرار)
        public int EmployeesWithoutTasks { get; set; }

        public List<OfficeSummaryDto> Offices { get; set; } = [];
        public List<WorkTaskDistributionDto> TaskDistribution { get; set; } = [];
        public List<DashboardActivityDto> RecentActivity { get; set; } = [];
    }

    public class OfficeSummaryDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string ManagerNames { get; set; } = string.Empty;
        public int EmployeesCount { get; set; }
        public int EmployeesWithTasks { get; set; }
    }

    // ════════════════════ رئيس المكتب ════════════════════

    public class OfficeDashboardDto
    {
        public int OfficeId { get; set; }
        public string OfficeName { get; set; } = string.Empty;
        public int DepartmentId { get; set; }
        public string DepartmentName { get; set; } = string.Empty;
        public int BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public string ManagerNames { get; set; } = string.Empty;

        public int EmployeesCount { get; set; }
        public int TasksCount { get; set; }
        public int EmployeesWithTasks { get; set; }
        public int EmployeesWithoutTasks { get; set; }

        public List<OfficeMemberDto> Members { get; set; } = [];
        public List<WorkTaskDistributionDto> TaskDistribution { get; set; } = [];
        public List<DashboardActivityDto> RecentActivity { get; set; } = [];
    }

    public class OfficeMemberDto
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public List<string> TaskNames { get; set; } = [];
        public DateTime JoinedAt { get; set; }
    }

    // ════════════════════ الموظف ════════════════════

    public class EmployeeDashboardDto
    {
        public string FullName { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
        public string BranchName { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public string OfficeName { get; set; } = string.Empty;
        public DateTime JoinedAt { get; set; }

        public bool CanRequestVacation { get; set; }
        public int PaidDaysLimitPerMonth { get; set; }
        public int PaidDaysLeftThisMonth { get; set; }

        /// <summary>زملاء المكتب (أو القسم لمن لا مكتب له)</summary>
        public string TeamName { get; set; } = string.Empty;
        public List<TeamMemberDto> Team { get; set; } = [];
    }

    public class TeamMemberDto
    {
        public string FullName { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }

    // ════════════════════ صفحة إحصائيات الإجازات (للرؤساء) ════════════════════

    public class DashboardVacationRowDto
    {
        public int Id { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public string OfficeName { get; set; } = string.Empty;
        public string VacationTypeName { get; set; } = string.Empty;
        public DateTime StartVac { get; set; }
        public DateTime EndVac { get; set; }
        public int VacDayCount { get; set; }
        public string Status { get; set; } = string.Empty;
        public string StatusAr { get; set; } = string.Empty;
        public bool IsPaid { get; set; }
        public int PaidDays { get; set; }
        public int UnpaidDays { get; set; }
        public string PaymentStatusAr { get; set; } = string.Empty;
    }

    /// <summary>آخر ما حدث على طلبات الإجازة — مستنتج من حالة الطلب وتاريخ آخر تحديث</summary>
    public class DashboardActionDto
    {
        public int VacationId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public string ActionType { get; set; } = string.Empty; // Submitted | Forwarded | Approved | Rejected | Cancelled
        public string Action { get; set; } = string.Empty;
        public string? ByName { get; set; }
        public DateTime Date { get; set; }
    }

    public class VacationStatsDto
    {
        public string ScopeType { get; set; } = string.Empty; // All | Branch | Department | Office
        public string ScopeName { get; set; } = string.Empty;

        public int OnLeaveToday { get; set; }
        public int UpcomingLeavesCount { get; set; }   // تبدأ خلال 7 أيام
        public int PendingRequests { get; set; }       // كل الطلبات قيد الموافقة في النطاق
        public int ApprovedDaysThisMonth { get; set; }

        /// <summary>الطلبات التي تنتظر قرار صاحب النطاق (رئيس القسم: المرحلة الأولى، رئيس الفرع: النهائية)</summary>
        public string PendingStageLabel { get; set; } = string.Empty;
        public List<DashboardVacationRowDto> PendingApprovals { get; set; } = [];
        public List<DashboardVacationRowDto> OnLeave { get; set; } = [];
        public List<DashboardActionDto> RecentActions { get; set; } = [];
        public List<DashboardCountItemDto> VacationsByType { get; set; } = [];
    }
}
