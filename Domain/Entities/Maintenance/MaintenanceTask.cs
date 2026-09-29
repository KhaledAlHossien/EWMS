namespace Domain.Entities.Maintenance
{
    public class MaintenanceTask //مهمة عمل لقسم الصيانة
    {
        public int Id { get; set; }

        // الموظف الذي عمل على المهمة (يُملأ تلقائياً ولا يتغير)
        public required int UserId { get; set; }
        public User User { get; set; } = null!;

        // قسم الموظف لحظة التسجيل — ليراها رئيس القسم
        public int? DepartmentId { get; set; }
        public Department? Department { get; set; }

        public string TaskLocation { get; set; } = string.Empty; //مكان المهمة
        public string RequestingParty { get; set; } = string.Empty; //الجهة الطالبة
        public string RequiredWork { get; set; } = string.Empty; // الاعمال المطلوبة
        public string CompletedWorks { get; set; } = string.Empty; // الاعمال المنجزة

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }
}
