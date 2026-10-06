using Application.DTOs.Request;
using Application.DTOs.Response;
using Domain.Entities.Maintenance;

namespace Application.Interfaces
{
    /// <summary>نتيجة حركة مخزون: الرصيد قبلها وبعدها ومتوسط السعر بعدها (لقرار تنبيه الحد الأدنى)</summary>
    public sealed record StockChange(decimal Before, decimal After, decimal AverageCost);

    /// <summary>
    /// مخزون قطع الغيار. حركات الكمية ذرّية: قفل صف القطعة داخل معاملة ثم تحديث الرصيد وتسجيل الحركة،
    /// فلا يُصرف نفس الرصيد مرتين في طلبين متزامنين ولا يصير الرصيد سالباً.
    /// </summary>
    public interface ISparePartService
    {
        /// <summary>مع القسم والتوافق</summary>
        Task<SparePart?> GetByIdAsync(int id);

        /// <summary>departmentId = null: كل الأقسام</summary>
        Task<(List<SparePart> Items, int TotalCount)> SearchAsync(int? departmentId, SparePartFilterDto filter, int page, int pageSize);

        /// <summary>قطع قسم الطلب المتوفرة (رصيد > 0)، المتوافقة مع نوع الجهاز وشركته أولاً</summary>
        Task<List<SparePart>> GetAvailableForRequestAsync(int departmentId, int deviceTypeId, int deviceCompanyId, string? search, int take);

        Task<bool> NameExistsAsync(int departmentId, string name, int? excludeId = null);
        Task<SparePart> AddAsync(SparePart part);
        /// <summary>يحفظ البيانات ويستبدل التوافق (لا يمس الكمية والسعر)</summary>
        Task UpdateAsync(SparePart part, IReadOnlyCollection<int> deviceTypeIds, IReadOnlyCollection<int> deviceCompanyIds);
        Task DeleteAsync(SparePart part);
        Task<bool> HasMovementsAsync(int id);

        // ===== الحركات (ذرّية) — ترمي InvalidOperationException برسالة عربية إن لم يكفِ الرصيد =====
        Task<StockChange> ReceiveAsync(int partId, decimal quantity, decimal unitCost, DateTime date, string source, int userId);
        Task<StockChange> AdjustAsync(int partId, decimal delta, string reason, int userId);
        /// <summary>يصرف على الطلب: ينقص الرصيد، ويسجّل الحركة وقطعة الطلب بسعر المتوسط الحالي</summary>
        Task<(MaintenanceRequestPart Part, StockChange Change)> IssueAsync(int requestId, int partId, decimal quantity, int userId);
        /// <summary>يزيل القطعة من الطلب ويعيد كميتها للمخزون بسعرها المثبَّت</summary>
        Task<StockChange> ReturnAsync(MaintenanceRequestPart requestPart, int userId);

        Task<(List<SparePartMovement> Items, int TotalCount)> GetMovementsAsync(int partId, int page, int pageSize);

        Task<List<MaintenanceRequestPart>> GetRequestPartsAsync(int requestId);
        Task<MaintenanceRequestPart?> GetRequestPartAsync(int id);

        /// <summary>تكلفة قطع الجهاز في كل طلباته</summary>
        Task<decimal> GetDeviceCostAsync(int deviceMaintenanceId);

        /// <summary>departmentId = null: كل الأقسام. الفترة لأكثر القطع صرفاً وتكلفة المصروف؛ تكلفة الأجهزة على مدى عمرها.</summary>
        Task<SparePartReportDto> GetReportAsync(int? departmentId, DateTime from, DateTime to);

        // ===== حراسات الحذف =====
        /// <summary>للطلب قطع مصروفة أو حركات مخزون (حتى بعد الإعادة) — لا يُحذف</summary>
        Task<bool> HasRequestPartsAsync(int requestId);
        Task<bool> ExistsForUserAsync(int userId);
        Task<bool> ExistsForDepartmentAsync(int departmentId);
    }
}
