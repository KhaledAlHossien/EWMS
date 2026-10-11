using System.Text.Json;
using System.Text.Json.Nodes;
using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    /// <summary>طبقات خرائط الفروع وحقولها (1.2.0)</summary>
    public class MapLayerService : IMapLayerService
    {
        private readonly DataContext _context;

        public MapLayerService(DataContext context) => _context = context;

        public async Task<List<LayerWithCount>> GetAllAsync(int? branchId, bool includeInactive)
        {
            var query = _context.MapLayers.AsNoTracking()
                .Include(l => l.Branch)
                .Include(l => l.Fields.OrderBy(f => f.SortOrder).ThenBy(f => f.Id))
                .AsQueryable();
            if (branchId is int b) query = query.Where(l => l.BranchId == b);
            if (!includeInactive) query = query.Where(l => l.IsActive);

            var layers = await query.OrderBy(l => l.Branch.Name).ThenBy(l => l.SortOrder).ThenBy(l => l.Name).ToListAsync();
            var ids = layers.Select(l => l.Id).ToList();
            var counts = await _context.MapEntries.AsNoTracking()
                .Where(e => ids.Contains(e.LayerId))
                .GroupBy(e => e.LayerId)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count);
            return layers.Select(l => new LayerWithCount(l, counts.GetValueOrDefault(l.Id))).ToList();
        }

        public Task<MapLayer?> GetByIdAsync(int id) =>
            _context.MapLayers
                .Include(l => l.Branch)
                .Include(l => l.Fields.OrderBy(f => f.SortOrder).ThenBy(f => f.Id))
                .FirstOrDefaultAsync(l => l.Id == id);

        public Task<MapLayerField?> GetFieldAsync(int fieldId) =>
            _context.MapLayerFields.Include(f => f.Layer).FirstOrDefaultAsync(f => f.Id == fieldId);

        public Task<bool> NameExistsAsync(int branchId, string name, int? excludeId) =>
            _context.MapLayers.AnyAsync(l => l.BranchId == branchId && l.Name == name && l.Id != excludeId);

        public async Task AddAsync(MapLayer layer)
        {
            _context.MapLayers.Add(layer);
            await SaveAsync();
        }

        public async Task UpdateAsync(MapLayer layer, byte[]? rowVersion)
        {
            if (rowVersion is { Length: > 0 })
                _context.Entry(layer).Property(l => l.RowVersion).OriginalValue = rowVersion;
            await SaveAsync();
        }

        public async Task DeleteAsync(MapLayer layer)
        {
            _context.MapLayers.Remove(layer);
            await SaveAsync();
        }

        public Task SaveChangesAsync() => SaveAsync();

        private Task SaveAsync() => DeviceInventorySave.SaveAsync(_context, "توجد طبقة بنفس الاسم في هذا الفرع");

        public async Task<int> FieldUsageAsync(MapLayerField field)
        {
            var key = field.Id.ToString();
            var values = await _context.MapEntries.AsNoTracking().Where(e => e.LayerId == field.LayerId).Select(e => e.Values).ToListAsync();
            return values.Count(v => HasValue(v, key));
        }

        public async Task PurgeFieldAsync(MapLayerField field)
        {
            var key = field.Id.ToString();
            await using var transaction = await _context.Database.BeginTransactionAsync();
            var entries = await _context.MapEntries.Where(e => e.LayerId == field.LayerId).ToListAsync();
            foreach (var entry in entries)
            {
                if (JsonNode.Parse(entry.Values) is JsonObject obj && obj.Remove(key))
                    entry.Values = obj.ToJsonString();
            }
            _context.MapLayerFields.Remove(field);
            await SaveAsync();
            await transaction.CommitAsync();
        }

        private static bool HasValue(string json, string key)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty(key, out var v)) return false;
                return v.ValueKind switch
                {
                    JsonValueKind.Null or JsonValueKind.Undefined => false,
                    JsonValueKind.String => v.GetString()!.Length > 0,
                    _ => true
                };
            }
            catch (JsonException) { return false; }
        }

        public Task<bool> ExistsForBranchAsync(int branchId) => _context.MapLayers.AnyAsync(l => l.BranchId == branchId);

        public Task<bool> ExistsForUserAsync(int userId) => _context.MapLayers.AnyAsync(l => l.CreatedByUserId == userId);
    }

    /// <summary>سجلات طبقات الخرائط على المواقع (1.2.0)</summary>
    public class MapEntryService : IMapEntryService
    {
        private readonly DataContext _context;

        public MapEntryService(DataContext context) => _context = context;

        private IQueryable<MapEntry> WithDetails() => _context.MapEntries
            .Include(e => e.Layer).ThenInclude(l => l.Fields)
            .Include(e => e.Layer).ThenInclude(l => l.Branch)
            .Include(e => e.Site)
            .Include(e => e.Branch)
            .Include(e => e.Department)
            .Include(e => e.CreatedByUser)
            .Include(e => e.UpdatedByUser);

        public Task<MapEntry?> GetByIdAsync(int id) => WithDetails().FirstOrDefaultAsync(e => e.Id == id);

        public Task<List<MapEntry>> GetForSiteAsync(int siteId, EntryScope scope) =>
            WithDetails().AsNoTracking()
                .Where(e => e.SiteId == siteId)
                .Where(scope.Predicate())
                .OrderBy(e => e.Layer.SortOrder).ThenBy(e => e.Layer.Name).ThenBy(e => e.CreatedAt)
                .ToListAsync();

        public Task<bool> ExistsAsync(int layerId, int siteId, int? excludeId) =>
            _context.MapEntries.AnyAsync(e => e.LayerId == layerId && e.SiteId == siteId && e.Id != excludeId);

        public Task<int> CountForLayerAsync(int layerId) => _context.MapEntries.CountAsync(e => e.LayerId == layerId);

        public async Task AddAsync(MapEntry entry)
        {
            _context.MapEntries.Add(entry);
            await SaveAsync();
        }

        public async Task UpdateAsync(MapEntry entry, byte[]? rowVersion)
        {
            if (rowVersion is { Length: > 0 })
                _context.Entry(entry).Property(e => e.RowVersion).OriginalValue = rowVersion;
            await SaveAsync();
        }

        public async Task DeleteAsync(MapEntry entry)
        {
            _context.MapEntries.Remove(entry);
            await SaveAsync();
        }

        private Task SaveAsync() => DeviceInventorySave.SaveAsync(_context, "تعارض في بيانات السجل");

        public Task<bool> ExistsForSiteAsync(int siteId) => _context.MapEntries.AnyAsync(e => e.SiteId == siteId);
        public Task<bool> ExistsForDepartmentAsync(int departmentId) => _context.MapEntries.AnyAsync(e => e.DepartmentId == departmentId);
        public Task<bool> ExistsForBranchAsync(int branchId) => _context.MapEntries.AnyAsync(e => e.BranchId == branchId);
        public Task<bool> ExistsForUserAsync(int userId) =>
            _context.MapEntries.AnyAsync(e => e.CreatedByUserId == userId || e.UpdatedByUserId == userId);
    }
}
