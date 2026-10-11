using System.Linq.Expressions;
using Domain.Entities;

namespace Application.Interfaces
{
    /// <summary>
    /// سجلات الطبقات التي يراها المستخدم: كلها (نظرة المؤسسة)، أو سجلات طبقات فرعه (ViewBranchMapEntries)،
    /// أو ما أدخله قسمه. يحسبه MapLayerRules.EntryScopeFor.
    /// </summary>
    public sealed record EntryScope(bool All, int? BranchId, int? DepartmentId)
    {
        public static readonly EntryScope Everything = new(true, null, null);
        public static readonly EntryScope Nothing = new(false, null, null);

        public bool Includes(MapEntry e) =>
            All || (BranchId != null && e.BranchId == BranchId) || (DepartmentId != null && e.DepartmentId == DepartmentId);

        public Expression<Func<MapEntry, bool>> Predicate()
        {
            if (All) return e => true;
            if (BranchId is int branchId) return e => e.BranchId == branchId;
            if (DepartmentId is int departmentId) return e => e.DepartmentId == departmentId;
            return e => false;
        }
    }

    /// <summary>طبقة مع عدد سجلاتها</summary>
    public sealed record LayerWithCount(MapLayer Layer, int Entries);

    public interface IMapLayerService
    {
        /// <summary>طبقات فرع (null = كل الفروع) مع حقولها مرتبة وعدد سجلاتها</summary>
        Task<List<LayerWithCount>> GetAllAsync(int? branchId, bool includeInactive);
        /// <summary>متتبَّعة، مع الحقول والفرع</summary>
        Task<MapLayer?> GetByIdAsync(int id);
        Task<MapLayerField?> GetFieldAsync(int fieldId);
        Task<bool> NameExistsAsync(int branchId, string name, int? excludeId);

        Task AddAsync(MapLayer layer);
        /// <summary>rowVersion كما وصل: تعديل متزامن → رسالة</summary>
        Task UpdateAsync(MapLayer layer, byte[]? rowVersion);
        Task DeleteAsync(MapLayer layer);
        Task SaveChangesAsync();

        /// <summary>عدد السجلات التي لها قيمة في هذا الحقل</summary>
        Task<int> FieldUsageAsync(MapLayerField field);
        /// <summary>حذف نهائي: يُزال الحقل وقيمه من كل سجلات طبقته في معاملة واحدة</summary>
        Task PurgeFieldAsync(MapLayerField field);

        Task<bool> ExistsForBranchAsync(int branchId);
        Task<bool> ExistsForUserAsync(int userId);
    }

    public interface IMapEntryService
    {
        /// <summary>مع الطبقة (وحقولها) والموقع والقسم والفرع ومن أنشأ/عدّل</summary>
        Task<MapEntry?> GetByIdAsync(int id);
        /// <summary>سجلات موقع ضمن حدّ المستخدم</summary>
        Task<List<MapEntry>> GetForSiteAsync(int siteId, EntryScope scope);
        Task<bool> ExistsAsync(int layerId, int siteId, int? excludeId);
        Task<int> CountForLayerAsync(int layerId);

        Task AddAsync(MapEntry entry);
        Task UpdateAsync(MapEntry entry, byte[]? rowVersion);
        Task DeleteAsync(MapEntry entry);

        Task<bool> ExistsForSiteAsync(int siteId);
        Task<bool> ExistsForDepartmentAsync(int departmentId);
        Task<bool> ExistsForBranchAsync(int branchId);
        Task<bool> ExistsForUserAsync(int userId);
    }
}
