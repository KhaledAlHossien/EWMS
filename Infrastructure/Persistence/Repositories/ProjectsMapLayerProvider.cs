using Application.Common;
using Application.DTOs.Response;
using Application.Features.Projects;
using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    /// <summary>
    /// مزوّد «المشاريع» لخرائط الفروع: المواقع التي لمشاريع الفرع (Project.BranchId) عليها، مع فلاتر القسم والمرحلة.
    /// بلا فلتر مرحلة تُستثنى المشاريع الملغاة (ما بقي يظهر على الخريطة، ومنه المغلقة). الخريطة تعرض المواقع والأعداد فقط؛
    /// تفاصيل المشروع تتبع صلاحيات المشاريع (ProjectRules.Visible).
    /// </summary>
    public class ProjectsMapLayerProvider : IMapLayerProvider
    {
        public const string ProviderKey = "projects";
        private const string Department = "department", Stage = "stage";
        private static readonly string[] Keys = [Department, Stage];

        private readonly DataContext _context;

        public ProjectsMapLayerProvider(DataContext context) => _context = context;

        public string Key => ProviderKey;
        public string Title => "المشاريع";
        public string Unit => "مشروع";
        public IReadOnlyCollection<string> FilterKeys => Keys;

        public async Task<List<MapSourceFilterDto>> GetFiltersAsync(int branchId)
        {
            var departments = await _context.Departments.AsNoTracking()
                .Where(d => d.BranchId == branchId).OrderBy(d => d.Name)
                .Select(d => new MapSourceOptionDto(d.Id.ToString(), d.Name)).ToListAsync();

            return
            [
                new() { Key = Department, Label = "القسم", Options = departments },
                new()
                {
                    Key = Stage, Label = "المرحلة",
                    Options = Enum.GetValues<ProjectStage>()
                        .Select(s => new MapSourceOptionDto(((int)s).ToString(), ProjectRules.StageAr(s))).ToList()
                }
            ];
        }

        public async Task<List<MapSiteDto>> GetSitesAsync(int branchId, IReadOnlyDictionary<string, List<string>> filter)
        {
            var query = _context.Projects.AsNoTracking().Where(p => p.BranchId == branchId && p.SiteId != null);
            if (Ints(filter, Department) is { Count: > 0 } departments)
                query = query.Where(p => p.DepartmentId != null && departments.Contains(p.DepartmentId.Value));
            if (Ints(filter, Stage) is { Count: > 0 } stages)
            {
                var values = stages.Select(s => (ProjectStage)s).ToList();
                query = query.Where(p => values.Contains(p.Stage));
            }
            else
                query = query.Where(p => p.Stage != ProjectStage.Cancelled);

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
            _context.Projects.AsNoTracking().Where(p => p.SiteId != null).Select(p => p.BranchId).Distinct().ToListAsync();

        private static List<int> Ints(IReadOnlyDictionary<string, List<string>> filter, string key) =>
            filter.TryGetValue(key, out var values) ? values.Select(v => int.TryParse(v, out var n) ? n : 0).Where(n => n > 0).ToList() : [];
    }
}
