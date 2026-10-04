namespace Domain.Entities.Maintenance
{
    /// <summary>
    /// جهاز يُحضَر للصيانة (قطعة فعلية برقم تسلسلي فريد). تتجمّع تحته كل طلبات الصيانة التي فُتحت له،
    /// فيصبح سجل إصلاحاته كاملاً — قرار المستخدم 2026-10-04.
    /// </summary>
    public class DeviceMaintenance
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public required string SerialNumber { get; set; }
        public string Model { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public required int DeviceTypeId { get; set; }
        public DeviceType DeviceType { get; set; } = null!;

        public required int DeviceCompanyId { get; set; }
        public DeviceCompany DeviceCompany { get; set; } = null!;
    }
}
