namespace Domain.Entities
{
    /// <summary>جهاز في الكتالوج: نوع/موديل قابل للتركيب عدة مرات (الاسم + الموديل فريدان معاً)</summary>
    public class Device
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        /// <summary>الفئة: كاميرا، مسجّل، سويتش… (نص حر مع اقتراحات في الواجهة)</summary>
        public string Category { get; set; } = string.Empty;
        public string Manufacturer { get; set; } = string.Empty;

        /// <summary>منع محو تعديل الآخرين: التعديل يُرفض إن تغيّر السجل منذ فتحه</summary>
        public byte[] RowVersion { get; set; } = [];
    }
}
