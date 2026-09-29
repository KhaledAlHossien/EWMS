namespace Domain.Entities.Maintenance
{
    public class MaintenanceRequestStatus //حالة طلب الصيانة
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Color { get; set; } = "#FFFFFF";
    }
}
