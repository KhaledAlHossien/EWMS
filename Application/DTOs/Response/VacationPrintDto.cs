namespace Application.DTOs.Response
{
    /// <summary>
    /// بيانات نموذج "طلب إجازة" الورقي (نموذج المؤسسة الحالي — صورة من المستخدم 2026-10-04):
    /// الترويسة، بيانات مقدم الطلب، نوع الإجازة (خانات اختيار لكل الأنواع)، السبب، رأي رئيس الفرع وتوقيعه.
    /// </summary>
    public class VacationPrintDto
    {
        public VacationResponseDto Vacation { get; set; } = new();

        // ===== ترويسة: الرقم / التاريخ (هجري) / الموافق لـ (ميلادي) =====
        public string SubmittedHijri { get; set; } = string.Empty;   // 1448/4/12
        public DateTime SubmittedAt { get; set; }                      // تاريخ التقديم (محلي)

        // ===== بيانات مقدم الطلب =====
        public string EmployeeName { get; set; } = string.Empty;
        public string? PersonalIdNumber { get; set; }                  // الرقم الذاتي
        public string? Phone { get; set; }                             // رقم التواصل
        public string DepartmentName { get; set; } = string.Empty;    // القسم التابع له
        public string BranchName { get; set; } = string.Empty;

        // ===== نوع الإجازة: كل الأنواع، والمختار مؤشَّر =====
        public List<VacationPrintTypeDto> Types { get; set; } = new();

        // ===== رأي رئيس الفرع =====
        /// <summary>null = لم يُقرَّر بعد (تُطبع أسطر فارغة)</summary>
        public string? BranchOpinion { get; set; }
        public string? SignerName { get; set; }
        public string? SignerSignature { get; set; }                   // صورة التوقيع (data URL) إن رفعها
    }

    public class VacationPrintTypeDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool Selected { get; set; }
    }
}
