namespace Domain.Entities.Maintenance
{
    public enum MaintenanceTransferStatus
    {
        Pending = 1,   // بانتظار قرار رئيس القسم
        Approved = 2,  // قُبل ونُقل الطلب
        Rejected = 3,  // رُفض
        Closed = 4     // أُغلق تلقائياً: نُقل الطلب مباشرة (Assign/{id}) قبل البت فيه
    }

    /// <summary>
    /// طلب تحويل طلب صيانة إلى موظف آخر (قرار المستخدم 2026-10-05): يطلبه الفني المسند إليه الطلب مع سبب
    /// (واقتراح زميل اختياري)، ويقرّره من يملك نقل طلبات القسم (AssignMaintenanceRequest).
    /// طلب تحويل واحد معلّق على الأكثر لكل طلب صيانة.
    /// </summary>
    public class MaintenanceTransferRequest
    {
        public int Id { get; set; }

        public int MaintenanceRequestId { get; set; }
        public MaintenanceRequest MaintenanceRequest { get; set; } = null!;

        // من طلب التحويل (الفني المسند إليه وقتها)
        public int RequestedById { get; set; }
        public User RequestedBy { get; set; } = null!;

        // زميل يقترحه (اختياري)
        public int? SuggestedUserId { get; set; }
        public User? SuggestedUser { get; set; }

        public string Reason { get; set; } = string.Empty;
        public MaintenanceTransferStatus Status { get; set; } = MaintenanceTransferStatus.Pending;

        // القرار
        public int? DecidedById { get; set; }
        public User? DecidedBy { get; set; }
        public int? NewUserId { get; set; }            // من نُقل إليه الطلب عند القبول
        public User? NewUser { get; set; }
        public string? DecisionNote { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? DecidedAt { get; set; }
    }
}
