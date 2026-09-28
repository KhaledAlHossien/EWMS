namespace Domain.Entities
{
    // جدول كسر العلاقة بين Device و Site (many-to-many) + بيانات الاتصال بالجهاز في هذا الموقع تحديداً
    public class DeviceSite
    {
        public int Id { get; set; }

        public required int DeviceId { get; set; }
        public Device Device { get; set; } = null!;

        public required int SiteId { get; set; }
        public Site Site { get; set; } = null!;

        public string Ip { get; set; } = string.Empty;
        public string SubnetMask { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Pass { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
    }
}
