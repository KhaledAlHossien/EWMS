namespace Domain.Entities.Maintenance
{
    /// <summary>
    /// مرحلة ثابتة يفهمها النظام لكل حالة (قرار المستخدم 2026-10-05). الحالات أسماء وألوان يضيفها المستخدم،
    /// والمرحلة تحدد السلوك: أوقات البدء/الإنجاز التلقائية، تثبيت توقيع التسليم، وقفل الطلب في المرحلتين الأخيرتين.
    /// </summary>
    public enum MaintenanceStage
    {
        New = 1,            // استُلم ولم يبدأ العمل
        InProgress = 2,     // الفني يعمل عليه
        Ready = 3,          // أُصلح وينتظر التسليم
        Delivered = 4,      // سُلّم للعميل — نهائية (يُقفل الطلب، ويُثبَّت توقيع ورقة التسليم)
        NotRepairable = 5   // غير قابل للصيانة — نهائية (يُقفل الطلب)
    }

    public class MaintenanceRequestStatus //حالة طلب الصيانة
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Color { get; set; } = "#FFFFFF";
        public MaintenanceStage Stage { get; set; } = MaintenanceStage.InProgress;
    }
}
