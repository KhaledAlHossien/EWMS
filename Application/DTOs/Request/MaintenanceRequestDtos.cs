namespace Application.DTOs.Request
{
    // ==================== الجداول المساعدة (نفس الـ DTO للإضافة والتعديل) ====================

    public class DeviceTypeRequestDto
    {
        public string Name { get; set; } = string.Empty;
    }

    public class DeviceCompanyRequestDto
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public class DamageTypeRequestDto
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public class MaintenanceRequestStatusRequestDto
    {
        public string Name { get; set; } = string.Empty;
        public string Color { get; set; } = "#FFFFFF";
        /// <summary>المرحلة الثابتة (MaintenanceStage): 1 جديد، 2 قيد العمل، 3 جاهز، 4 مُسلَّم، 5 غير قابل للصيانة</summary>
        public int Stage { get; set; } = 2;
    }

    // ==================== جهاز الصيانة ====================
    public class DeviceMaintenanceRequestDto
    {
        public string Name { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int DeviceTypeId { get; set; }
        public int DeviceCompanyId { get; set; }
    }

    /// <summary>
    /// بحث أجهزة الصيانة — كل الحقول اختيارية وتُجمع بـ AND.
    /// SerialNumber/Model: "يبدأ بـ" (يستفيد من الفهرس)، Name: "يحتوي".
    /// </summary>
    public class DeviceMaintenanceFilterDto
    {
        public string? SerialNumber { get; set; }
        public string? Model { get; set; }
        public string? Name { get; set; }
        public int? DeviceTypeId { get; set; }
        public int? DeviceCompanyId { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    // ==================== طلب الصيانة ====================
    // الفني (UserId) والقسم لا يُرسلان: يُؤخذان من المستخدم الحالي عند الإنشاء.
    // الجهاز يُختار من أجهزة الصيانة الموجودة (يُضاف أولاً من MaintenanceDevices إن كان جديداً).
    public class SaveMaintenanceRequestDto
    {
        /// <summary>العميل موظف (من ClientLookup) أو null لعميل من خارج المؤسسة؛ اسم الموظف يُؤخذ من حسابه</summary>
        public int? ClientUserId { get; set; }
        public string ClientName { get; set; } = string.Empty;
        public string ClientPhone { get; set; } = string.Empty;
        public int DeviceMaintenanceId { get; set; }
        /// <summary>
        /// الفني المسؤول عند الإنشاء فقط (null = من يسجّل الطلب). اختيار غيره يحتاج AssignMaintenanceRequest وموظفاً من قسمه.
        /// يُتجاهل في التعديل — النقل بعد الإنشاء عبر Assign/{id} أو طلب تحويل.
        /// </summary>
        public int? AssigneeId { get; set; }
        public int DamageTypeId { get; set; }
        public string Accessories { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int MaintenanceRequestStatusId { get; set; }
        // أوقات البدء والإنجاز تُسجَّل تلقائياً من مرحلة الحالة (لا تُرسل)
    }

    /// <summary>
    /// بحث طلبات الصيانة — كل الحقول اختيارية وتُجمع بـ AND.
    /// SerialNumber/Model: "يبدأ بـ" على جهاز الطلب (يستفيد من فهرس DeviceMaintenances)، ClientName: "يحتوي".
    /// DeviceMaintenanceId: كل طلبات جهاز واحد (سجل إصلاحاته).
    /// </summary>
    public class MaintenanceRequestFilterDto
    {
        public int? DeviceMaintenanceId { get; set; }
        public string? SerialNumber { get; set; }
        public string? Model { get; set; }
        public string? ClientName { get; set; }
        public int? DeviceCompanyId { get; set; }
        public int? TechnicianId { get; set; }
        public int? DeviceTypeId { get; set; }
        public int? DamageTypeId { get; set; }
        public int? StatusId { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    /// <summary>تغيير حالة الطلب فقط (السحب والإفلات في لوحة الحالات)</summary>
    public class ChangeMaintenanceStatusDto
    {
        public int StatusId { get; set; }
    }

    /// <summary>الفني يطلب تحويل طلب مسند إليه: السبب إجباري، والزميل المقترح اختياري</summary>
    public class RequestMaintenanceTransferDto
    {
        public string Reason { get; set; } = string.Empty;
        public int? SuggestedUserId { get; set; }
    }

    /// <summary>قرار رئيس القسم: القبول ينقل الطلب إلى UserId (أو المقترح إن لم يُحدَّد)، والرفض مع ملاحظة</summary>
    public class DecideMaintenanceTransferDto
    {
        public bool Approve { get; set; }
        public int? UserId { get; set; }
        public string? Note { get; set; }
    }

    /// <summary>نقل طلب/مهمة إلى موظف آخر (رئيس القسم)</summary>
    public class AssignMaintenanceDto
    {
        public int UserId { get; set; }
    }

    /// <summary>صورة التوقيع كـ Data URL (PNG/JPEG)، أو فارغة لإيقاف التوقيع — مع كلمة مرور المستخدم للتأكيد</summary>
    public class SignatureRequestDto
    {
        public string? Image { get; set; }
        public string Password { get; set; } = string.Empty;
    }

    // ==================== مهمة الصيانة ====================
    public class SaveMaintenanceTaskDto
    {
        /// <summary>
        /// الموظف الموجَّهة إليه المهمة (عند الإنشاء فقط): رئيس القسم يختار من موظفي قسمه.
        /// فارغ = المهمة لمن سجّلها. بعد الإنشاء يُغيَّر الموظف بـ Assign/{id}.
        /// </summary>
        public int? AssigneeId { get; set; }

        public string TaskLocation { get; set; } = string.Empty;
        public string RequestingParty { get; set; } = string.Empty;
        public string RequiredWork { get; set; } = string.Empty;
        public string CompletedWorks { get; set; } = string.Empty;
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }
}
