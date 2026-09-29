namespace Domain.Entities.Maintenance
{
    public class DeviceCompany //الشركة المصنعة
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;
    }
}
