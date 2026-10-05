namespace Domain.Entities.Maintenance
{
    public enum MaintenanceActivityType
    {
        Created = 1,        // تسجيل الطلب
        StatusChanged = 2,  // تغيير الحالة
        Reassigned = 3,     // نقل الطلب إلى فني آخر
        Edited = 4,         // تعديل بيانات الطلب
        TransferRequested = 5, // طلب الفني تحويل الطلب إلى موظف آخر
        TransferRejected = 6   // رفض رئيس القسم طلب التحويل (القبول يُسجَّل Reassigned)
    }

    /// <summary>سجل طلب الصيانة: من فعل ماذا ومتى (يُحذف مع الطلب)</summary>
    public class MaintenanceRequestActivity
    {
        public int Id { get; set; }

        public int MaintenanceRequestId { get; set; }
        public MaintenanceRequest MaintenanceRequest { get; set; } = null!;

        // من قام بالإجراء
        public int UserId { get; set; }
        public User User { get; set; } = null!;

        public MaintenanceActivityType Type { get; set; }

        // نص جاهز للعرض — الحالات جدول يُعدَّل ويُحذف، فنحفظ أسماءها كما كانت لحظة الإجراء
        public string Text { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }
    }
}
