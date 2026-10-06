namespace Application.DTOs.Response
{
    // ==================== مخزون قطع الغيار ====================

    public class NamedRefDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class SparePartResponseDto
    {
        public int Id { get; set; }
        public int DepartmentId { get; set; }
        public string DepartmentName { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;
        public string PartNumber { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public decimal Quantity { get; set; }
        public decimal MinQuantity { get; set; }
        public decimal AverageCost { get; set; }
        /// <summary>قيمة الرصيد = الكمية × متوسط السعر</summary>
        public decimal StockValue { get; set; }
        /// <summary>تحت الحد الأدنى (والحد أكبر من صفر)</summary>
        public bool IsLow { get; set; }

        public List<NamedRefDto> DeviceTypes { get; set; } = [];
        public List<NamedRefDto> DeviceCompanies { get; set; } = [];

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class SparePartMovementDto
    {
        public int Id { get; set; }
        public int Type { get; set; }
        public string TypeAr { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal UnitCost { get; set; }
        public decimal BalanceAfter { get; set; }
        public DateTime Date { get; set; }
        public string Note { get; set; } = string.Empty;
        public int? MaintenanceRequestId { get; set; }
        public string? RequestNumber { get; set; }
        public string UserName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>قطعة مصروفة على طلب — السعر كما ثُبِّت لحظة الصرف</summary>
    public class MaintenanceRequestPartDto
    {
        public int Id { get; set; }
        public int SparePartId { get; set; }
        public string PartName { get; set; } = string.Empty;
        public string PartNumber { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal UnitCost { get; set; }
        public decimal Total { get; set; }
        public string IssuedByName { get; set; } = string.Empty;
        public DateTime IssuedAt { get; set; }
    }

    /// <summary>قطع طلب الصيانة + تكلفة الجهاز على مدى عمره</summary>
    public class RequestPartsDto
    {
        public List<MaintenanceRequestPartDto> Items { get; set; } = [];
        public decimal Total { get; set; }

        /// <summary>تكلفة قطع الجهاز في كل طلباته</summary>
        public decimal DeviceLifetimeCost { get; set; }
        public decimal? ReplacementCostThreshold { get; set; }
        /// <summary>بلغت تكلفة الجهاز حد الاستبدال لنوعه</summary>
        public bool OverThreshold { get; set; }

        /// <summary>يملك الصرف على هذا الطلب الآن (الصلاحية، النطاق، والطلب مفتوح وله قسم)</summary>
        public bool CanIssue { get; set; }
    }

    public class SparePartUsageDto
    {
        public int SparePartId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal Cost { get; set; }
    }

    public class DeviceCostDto
    {
        public int DeviceMaintenanceId { get; set; }
        public string SerialNumber { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public string DeviceTypeName { get; set; } = string.Empty;
        public int RequestsCount { get; set; }
        public decimal Cost { get; set; }
        public decimal? Threshold { get; set; }
        public bool OverThreshold { get; set; }
    }

    public class SparePartReportDto
    {
        public DateTime From { get; set; }
        public DateTime To { get; set; }

        public int PartsCount { get; set; }
        public int LowStockCount { get; set; }
        public decimal StockValue { get; set; }
        /// <summary>تكلفة القطع المصروفة في الفترة</summary>
        public decimal IssuedCost { get; set; }

        /// <summary>أكثر القطع صرفاً في الفترة</summary>
        public List<SparePartUsageDto> MostUsed { get; set; } = [];
        /// <summary>أعلى الأجهزة تكلفة على مدى عمرها</summary>
        public List<DeviceCostDto> DeviceCosts { get; set; } = [];
    }
}
