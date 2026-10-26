using Domain.Entities;
using Domain.Entities.Attendance;

namespace Application.Interfaces
{
    /// <summary>حدّ بمنطقة: كل المناطق، أو منطقة واحدة، أو لا شيء (من لا منطقة لفرعه)</summary>
    public sealed record AreaLimit(bool All, int? AreaId)
    {
        public static readonly AreaLimit Everything = new(true, null);
        public static readonly AreaLimit Nothing = new(false, null);

        public bool IsNothing => !All && AreaId == null;
        public bool Includes(int? areaId) => All || (AreaId != null && areaId == AreaId);
    }

    /// <summary>
    /// حدّ عرض الحضور: اتحاد ما يملكه المستخدم — الكل، أو منطقة (أشخاصها وبصمات أجهزتها) و/أو فرع و/أو قسم و/أو نفسه.
    /// </summary>
    public sealed record AttendanceScope(bool All, int? AreaId, int? BranchId, int? DepartmentId, int? SelfId)
    {
        public static readonly AttendanceScope Everything = new(true, null, null, null, null);
        public bool IsEmpty => !All && AreaId == null && BranchId == null && DepartmentId == null && SelfId == null;
    }

    /// <summary>منطقة مع فروعها وأعدادها</summary>
    public sealed record AreaSummary(AttendanceArea Area, List<Branch> Branches, int Devices, int OnlineDevices, int People);

    public interface IAttendanceAreaService
    {
        /// <summary>كل المناطق بالاسم مع فروعها وأعداد الأجهزة المقبولة (والمتصلة منذ onlineSinceUtc) والأشخاص على الأجهزة</summary>
        Task<List<AreaSummary>> GetAllAsync(DateTime onlineSinceUtc);
        Task<AreaSummary?> GetSummaryAsync(int id, DateTime onlineSinceUtc);
        /// <summary>متتبَّعة</summary>
        Task<AttendanceArea?> GetByIdAsync(int id);
        Task<bool> ExistsByNameAsync(string name, int? excludeId = null);
        /// <summary>كل الفروع مع منطقة كل منها</summary>
        Task<List<Branch>> GetBranchesAsync();
        /// <summary>
        /// يحفظ المنطقة ويضع فيها هذه الفروع بالضبط (الفرع المختار يُنقل إليها من منطقته السابقة، والمحذوف من القائمة يصبح بلا منطقة)،
        /// ويعلّم أجهزة كل منطقة تأثرت «تحتاج مزامنة» — في حفظ واحد.
        /// </summary>
        Task SaveAsync(AttendanceArea area, byte[]? rowVersion, IReadOnlyCollection<int> branchIds);
        /// <summary>سبب يمنع الحذف (فروع، أجهزة، عمال، مناطق إضافية) أو null</summary>
        Task<string?> DeleteBlockerAsync(int id);
        Task DeleteAsync(AttendanceArea area);
    }
}
