namespace Application.DTOs.Request
{
    // ==================== مخزون قطع الغيار ====================

    /// <summary>بيانات القطعة (الإضافة والتعديل). الكمية والسعر لا يُرسلان هنا — يتغيّران بالإدخال والصرف والتسوية فقط.</summary>
    public class SparePartRequestDto
    {
        /// <summary>قسم المخزون — يُقبل عند الإنشاء لمن يدير أكثر من قسم (مدير النظام)، وإلا فقسم المستخدم</summary>
        public int? DepartmentId { get; set; }

        public string Name { get; set; } = string.Empty;
        public string PartNumber { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal MinQuantity { get; set; }

        public List<int> DeviceTypeIds { get; set; } = [];
        public List<int> DeviceCompanyIds { get; set; } = [];
    }

    public class SparePartFilterDto
    {
        /// <summary>يحتوي في الاسم، أو يبدأ برقم القطعة</summary>
        public string? Search { get; set; }
        public int? DepartmentId { get; set; }
        public int? DeviceTypeId { get; set; }
        /// <summary>القطع التي نزلت تحت حدها الأدنى فقط</summary>
        public bool LowStock { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 25;
    }

    /// <summary>إدخال: استلام قطع بتاريخها وسعرها ومصدرها</summary>
    public class ReceiveSparePartDto
    {
        public decimal Quantity { get; set; }
        public decimal UnitCost { get; set; }
        public DateTime? Date { get; set; }
        public string Source { get; set; } = string.Empty;
    }

    /// <summary>تسوية: فرق الكمية (موجب = زيادة بالجرد، سالب = نقص أو تالف) مع السبب</summary>
    public class AdjustSparePartDto
    {
        public decimal Delta { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    /// <summary>صرف قطعة على طلب صيانة</summary>
    public class IssueSparePartDto
    {
        public int SparePartId { get; set; }
        public decimal Quantity { get; set; }
    }
}
