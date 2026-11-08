using Application.Common;
using Application.DTOs.Response;
using Application.Features.CentralStudies;
using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    /// <summary>
    /// مزوّد «الدراسات» لخرائط الفروع: مواقع مشاريع الفرع (StudyProject.SiteId المنسوخ من طلب الدراسة) مع عدد المشاريع في كل موقع،
    /// وفلاتر حالة المشروع ونوع العمل (إنشاء أو صيانة وترميم) — فيعرّف الفرع طبقات مثل «مشاريع الترميم الجارية» بلا إدخال.
    /// المشاريع الملغاة لا تظهر ما لم تُطلب في فلتر الحالة صراحةً. تفاصيل كل موقع تتبع صلاحيات المشاريع.
    /// </summary>
    public class StudiesMapLayerProvider : IMapLayerProvider
    {
        public const string ProviderKey = "studies";
        private const string Status = "status", WorkType = "workType";
        private static readonly string[] Keys = [Status, WorkType];

        private readonly DataContext _context;

        public StudiesMapLayerProvider(DataContext context) => _context = context;

        public string Key => ProviderKey;
        public string Title => "الدراسات";
        public string Unit => "مشروع";
        public IReadOnlyCollection<string> FilterKeys => Keys;

        // قيم التعدادين ثابتة، فتُعرض كلها للفرع (لا يُشترط أن يملك مشروعاً بكل حالة كي يعرّف طبقة بها)
        public Task<List<MapSourceFilterDto>> GetFiltersAsync(int branchId) =>
            Task.FromResult<List<MapSourceFilterDto>>(
            [
                new()
                {
                    Key = Status, Label = "حالة المشروع",
                    Options = Enum.GetValues<StudyProjectStatus>()
                        .Select(s => new MapSourceOptionDto(((int)s).ToString(), StudyRules.Name(s))).ToList()
                },
                new()
                {
                    Key = WorkType, Label = "نوع العمل",
                    Options = Enum.GetValues<StudyWorkType>()
                        .Select(s => new MapSourceOptionDto(((int)s).ToString(), StudyRules.Name(s))).ToList()
                }
            ]);

        public async Task<List<MapSiteDto>> GetSitesAsync(int branchId, IReadOnlyDictionary<string, List<string>> filter)
        {
            var query = _context.StudyProjects.AsNoTracking().Where(p => p.BranchId == branchId && p.SiteId != null);

            if (Ints(filter, Status) is { Count: > 0 } statuses)
            {
                var values = statuses.Select(s => (StudyProjectStatus)s).ToList();
                query = query.Where(p => values.Contains(p.Status));
            }
            else
                query = query.Where(p => p.Status != StudyProjectStatus.Cancelled);

            if (Ints(filter, WorkType) is { Count: > 0 } workTypes)
            {
                var values = workTypes.Select(s => (StudyWorkType)s).ToList();
                query = query.Where(p => values.Contains(p.StartOrder.Response.Assignment.Request.WorkType));
            }

            var perSite = await query
                .GroupBy(p => p.SiteId!.Value)
                .Select(g => new { SiteId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.SiteId, x => x.Count);
            var siteIds = perSite.Keys.ToList();
            var sites = await _context.Sites.AsNoTracking()
                .Where(s => siteIds.Contains(s.Id))
                .OrderBy(s => s.Name)
                .Select(s => new MapSiteDto
                {
                    Id = s.Id, Name = s.Name, Description = s.Description, GovernorateCode = s.GovernorateCode,
                    Latitude = s.Latitude, Longitude = s.Longitude
                }).ToListAsync();

            foreach (var site in sites)
            {
                site.GovernorateName = Governorates.NameOf(site.GovernorateCode);
                site.Count = perSite[site.Id];
            }
            return sites;
        }

        public Task<List<int>> BranchesWithDataAsync() =>
            _context.StudyProjects.AsNoTracking().Where(p => p.SiteId != null).Select(p => p.BranchId).Distinct().ToListAsync();

        private static List<int> Ints(IReadOnlyDictionary<string, List<string>> filter, string key) =>
            filter.TryGetValue(key, out var values) ? values.Select(v => int.TryParse(v, out var n) ? n : 0).Where(n => n > 0).ToList() : [];
    }
}
