namespace Domain.Entities.Maintenance
{
    public class MaintenanceRequest //طلب صيانة
    {
        public int Id { get; set; }

        // الفني: من سجّل الطلب وهو نفسه من يقوم بالصيانة (يُملأ تلقائياً ولا يتغير)
        public required int UserId { get; set; }
        public User User { get; set; } = null!;

        // قسم الفني لحظة التسجيل — ليراه رئيس القسم حتى لو انتقل الفني لاحقاً
        public int? DepartmentId { get; set; }
        public Department? Department { get; set; }

        public string ClientName { get; set; } = string.Empty; //اسم العميل
        public string ClientPhone { get; set; } = string.Empty; //هاتف العميل

        public required int DeviceTypeId { get; set; }
        public DeviceType DeviceType { get; set; } = null!;

        public required int DamageTypeId { get; set; }
        public DamageType DamageType { get; set; } = null!;

        public required int DeviceCompanyId { get; set; }
        public DeviceCompany DeviceCompany { get; set; } = null!;

        public string Model { get; set; } = string.Empty;

        public string Accessories { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public required int MaintenanceRequestStatusId { get; set; }
        public MaintenanceRequestStatus MaintenanceRequestStatus { get; set; } = null!;

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }
}
