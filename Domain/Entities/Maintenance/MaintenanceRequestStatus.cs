namespace Domain.Entities.Maintenance
{
    public class MaintenanceRequestStatus //حالة طلب الصيانة
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Color { get; set; } = "#FFFFFF";

        // حالة تسليم: عند تحويل طلب إليها يُثبَّت موقّع ورقة التسليم وتوقيعه (قرار المستخدم 2026-10-04)
        public bool IsDelivery { get; set; }
    }
}
