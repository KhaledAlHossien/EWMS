using System.Linq.Expressions;
using Application.DTOs.Response;
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

    /// <summary>
    /// مزوّد طبقة من بيانات النظام: ميزة سجلاتها مرتبطة بالمواقع المشتركة (SiteId) وتحمل وحدتها (BranchId/DepartmentId)،
    /// فتظهر على خريطة الفرع بلا إدخال يدوي. أول مزوّد: تركيبات الأجهزة. ميزة جديدة لها مكان = مزوّد جديد يُسجَّل في DI.
    /// الخريطة تعرض المواقع والأعداد فقط؛ تفاصيل السجلات في صفحات الميزة نفسها وبصلاحياتها.
    /// </summary>
    public interface IMapLayerProvider
    {
        string Key { get; }
        string Title { get; }
        /// <summary>وحدة العدّ في الموقع (مثل «تركيب»)</summary>
        string Unit { get; }
        /// <summary>مفاتيح الفلاتر التي يفهمها المزوّد</summary>
        IReadOnlyCollection<string> FilterKeys { get; }
        /// <summary>الفلاتر بخياراتها من بيانات الفرع (لنموذج الطبقة)</summary>
        Task<List<MapSourceFilterDto>> GetFiltersAsync(int branchId);
        /// <summary>مواقع سجلات الفرع المطابقة للفلتر مع العدد في كل موقع (Count)</summary>
        Task<List<MapSiteDto>> GetSitesAsync(int branchId, IReadOnlyDictionary<string, List<string>> filter);
        /// <summary>الفروع التي لها بيانات من هذا المزوّد (لقائمة فروع الخريطة)</summary>
        Task<List<int>> BranchesWithDataAsync();
    }

    /// <summary>ملفات تُربط بالسجل أو تُحذف منه مع حفظه (نفس المعاملة)</summary>
    public sealed record MapEntryFileChanges(IReadOnlyList<MapEntryFile> Attach, IReadOnlyList<MapEntryFile> Remove)
    {
        public static readonly MapEntryFileChanges None = new([], []);
    }

    /// <summary>إعدادات طبقات الخرائط من إعدادات الخادم</summary>
    public interface IMapLayerSettings
    {
        /// <summary>سقف حجم الملف الواحد بالـ MB لكل حقول الملفات (MapLayers:MaxFileMegabytes، الافتراضي 100)</summary>
        int MaxFileMegabytes { get; }
    }

    /// <summary>ملفات حقول «ملفات»: تُرفع معلّقة ثم تُربط بالسجل عند حفظه</summary>
    public interface IMapEntryFileService
    {
        /// <summary>data = المحتوى في القاعدة، أو null إن حُفظ في مجلد المرفقات (file.StorageKey)</summary>
        Task AddPendingAsync(MapEntryFile file, byte[]? data);
        /// <summary>متتبَّعة، بلا المحتوى</summary>
        Task<List<MapEntryFile>> GetByIdsAsync(IReadOnlyCollection<int> ids);
        /// <summary>مع السجل (لفحص الحد) ومن رفعه، بلا المحتوى</summary>
        Task<MapEntryFile?> GetByIdAsync(int id);
        Task<byte[]?> GetDataAsync(int id);
        Task DeleteAsync(MapEntryFile file);
        /// <summary>حذف الملفات المعلّقة (لم تُربط بسجل) المرفوعة قبل الوقت المحدد</summary>
        Task<int> DeletePendingBeforeAsync(DateTime cutoffUtc);
    }

    public interface IMapEntryService
    {
        /// <summary>مع الطبقة (وحقولها) والموقع والقسم والفرع ومن أنشأ/عدّل</summary>
        Task<MapEntry?> GetByIdAsync(int id);
        /// <summary>سجلات موقع ضمن حدّ المستخدم</summary>
        Task<List<MapEntry>> GetForSiteAsync(int siteId, EntryScope scope);
        Task<bool> ExistsAsync(int layerId, int siteId, int? excludeId);
        Task<int> CountForLayerAsync(int layerId);

        /// <summary>يحفظ السجل ويربط ملفاته المعلّقة ويحذف المزالة منها في حفظ واحد</summary>
        Task AddAsync(MapEntry entry, MapEntryFileChanges files);
        Task UpdateAsync(MapEntry entry, byte[]? rowVersion, MapEntryFileChanges files);
        Task DeleteAsync(MapEntry entry);

        Task<bool> ExistsForSiteAsync(int siteId);
        Task<bool> ExistsForDepartmentAsync(int departmentId);
        Task<bool> ExistsForBranchAsync(int branchId);
        Task<bool> ExistsForUserAsync(int userId);
    }
}
