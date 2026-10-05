namespace Application.DTOs.Response
{
    /// <summary>
    /// «سجل الموظف» في بطاقة طلب الإجازة قبل القرار (قرار المستخدم 2026-10-04: البنود 1 و2 و3 و4 و6):
    /// آخر إجازة، الشهر الجاري، الدفع المتوقع لو اعتُمد الآن، زملاء القسم في نفس الفترة، طلباته المعلّقة الأخرى.
    /// </summary>
    public class VacationApprovalContextDto
    {
        // 1) آخر إجازة معتمدة بدأت حتى اليوم
        public VacationContextItemDto? LastVacation { get; set; }
        public int? DaysSinceLastVacation { get; set; }
        /// <summary>إجازات معتمدة لم تبدأ بعد، الأقرب أولاً (كي لا يظن المعتمِد أن الموظف بلا إجازات)</summary>
        public List<VacationContextItemDto> UpcomingApproved { get; set; } = new();

        // 2) الشهر الجاري: الإجازات المعتمدة وأيام العمل فيها
        public int MonthApprovedCount { get; set; }
        public int MonthApprovedDays { get; set; }

        // 3) الدفع لو اعتُمد الطلب الآن (نفس قاعدة الاعتماد النهائي)
        public int ProjectedWorkingDays { get; set; }
        public int ProjectedPaidDays { get; set; }
        public int ProjectedUnpaidDays { get; set; }
        public bool TypeIsPaid { get; set; }
        public int MaxPaidDaysPerMonth { get; set; }
        /// <summary>الأيام المدفوعة المعتمدة مسبقاً في كل شهر يمر به الطلب</summary>
        public List<VacationMonthQuotaDto> MonthlyQuota { get; set; } = new();

        // 4) زملاء القسم في إجازة (معتمدة أو معلّقة) تتداخل مع فترة الطلب
        public string DepartmentName { get; set; } = string.Empty;
        public int DepartmentActiveUsers { get; set; }
        public List<VacationContextItemDto> ColleaguesOnLeave { get; set; } = new();
        /// <summary>عدد الزملاء المختلفين (قد يكون لزميل أكثر من طلب في الفترة)</summary>
        public int ColleaguesOnLeaveCount { get; set; }

        // 6) طلبات أخرى معلّقة لنفس الموظف
        public List<VacationContextItemDto> OtherPending { get; set; } = new();
    }

    public class VacationContextItemDto
    {
        public int Id { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string VacationTypeName { get; set; } = string.Empty;
        public DateTime StartVac { get; set; }
        public DateTime EndVac { get; set; }
        public int VacDayCount { get; set; }
        public string Status { get; set; } = string.Empty;
        public string StatusAr { get; set; } = string.Empty;
    }

    public class VacationMonthQuotaDto
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public int PaidUsed { get; set; }
    }
}
