namespace Domain.Entities.Maintenance
{
    /// <summary>
    /// قطعة غيار في مخزون قسم صيانة (قرار المستخدم 2026-10-05: مخزون لكل قسم). الكمية ومتوسط السعر لا يُعدَّلان مباشرة —
    /// يتغيّران فقط بحركات المخزون (SparePartMovement) داخل معاملة واحدة.
    /// </summary>
    public class SparePart
    {
        public int Id { get; set; }

        // القسم صاحب المخزون — يرى قطعه ويصرف منها موظفوه
        public int DepartmentId { get; set; }
        public Department Department { get; set; } = null!;

        public string Name { get; set; } = string.Empty;
        public string PartNumber { get; set; } = string.Empty;   // رقم القطعة (اختياري)
        public string Unit { get; set; } = string.Empty;         // قطعة، متر، علبة…
        public string Description { get; set; } = string.Empty;

        public decimal Quantity { get; set; }       // الرصيد الحالي (لا يقل عن صفر)
        public decimal MinQuantity { get; set; }    // حد التنبيه: إشعار عند النزول تحته
        public decimal AverageCost { get; set; }    // متوسط مرجّح لأسعار الإدخال (ل.س)

        // التوافق (اختياري): أنواع الأجهزة والشركات التي تناسبها القطعة
        public List<SparePartDeviceType> DeviceTypes { get; set; } = [];
        public List<SparePartDeviceCompany> DeviceCompanies { get; set; } = [];

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class SparePartDeviceType
    {
        public int SparePartId { get; set; }
        public SparePart SparePart { get; set; } = null!;
        public int DeviceTypeId { get; set; }
        public DeviceType DeviceType { get; set; } = null!;
    }

    public class SparePartDeviceCompany
    {
        public int SparePartId { get; set; }
        public SparePart SparePart { get; set; } = null!;
        public int DeviceCompanyId { get; set; }
        public DeviceCompany DeviceCompany { get; set; } = null!;
    }

    public enum SparePartMovementType
    {
        Receive = 1,  // إدخال: استلام قطع بسعرها ومصدرها
        Issue = 2,    // صرف على طلب صيانة
        Return = 3,   // إرجاع من طلب صيانة (إزالة القطعة من الطلب)
        Adjust = 4    // تسوية: جرد أو تالف
    }

    /// <summary>حركة مخزون — سجل لا يُحذف ولا يُعدَّل. الكمية موجبة للداخل وسالبة للخارج.</summary>
    public class SparePartMovement
    {
        public int Id { get; set; }

        public int SparePartId { get; set; }
        public SparePart SparePart { get; set; } = null!;

        public SparePartMovementType Type { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitCost { get; set; }       // سعر الوحدة في هذه الحركة
        public decimal BalanceAfter { get; set; }   // الرصيد بعد الحركة

        // تاريخ الحركة: تاريخ الاستلام للإدخال (يدخله المستخدم)، ووقت التنفيذ لغيره
        public DateTime Date { get; set; }
        public string Note { get; set; } = string.Empty;   // المصدر عند الإدخال، السبب عند التسوية

        // طلب الصيانة للصرف والإرجاع
        public int? MaintenanceRequestId { get; set; }
        public MaintenanceRequest? MaintenanceRequest { get; set; }

        public int UserId { get; set; }
        public User User { get; set; } = null!;

        public DateTime CreatedAt { get; set; }
    }

    /// <summary>قطعة مصروفة على طلب صيانة — سعرها مثبَّت لحظة الصرف (لا يتغيّر بتغيّر سعر القطعة لاحقاً)</summary>
    public class MaintenanceRequestPart
    {
        public int Id { get; set; }

        public int MaintenanceRequestId { get; set; }
        public MaintenanceRequest MaintenanceRequest { get; set; } = null!;

        public int SparePartId { get; set; }
        public SparePart SparePart { get; set; } = null!;

        public decimal Quantity { get; set; }
        public decimal UnitCost { get; set; }

        public int IssuedById { get; set; }
        public User IssuedBy { get; set; } = null!;
        public DateTime IssuedAt { get; set; }
    }
}
