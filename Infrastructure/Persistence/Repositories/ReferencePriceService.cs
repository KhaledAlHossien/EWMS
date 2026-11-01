using Application.Features.ReferencePrices;
using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    public class ReferencePriceService : IReferencePriceService
    {
        private const int MaxChain = 50;
        private readonly DataContext _context;

        public ReferencePriceService(DataContext context) => _context = context;

        private IQueryable<ReferencePrice> Graph() => _context.ReferencePrices
            .Include(p => p.Supplier).Include(p => p.Project).Include(p => p.CreatedByUser).Include(p => p.ApprovedByUser).Include(p => p.SupersedesPrice).Include(p => p.Import);

        public Task<ReferencePrice?> GetByIdAsync(int id) => Graph().FirstOrDefaultAsync(p => p.Id == id);

        public async Task<(List<ReferencePrice> Items, int TotalCount)> SearchAsync(ReferencePriceFilter f, int page, int pageSize)
        {
            var query = _context.ReferencePrices.AsNoTracking().AsQueryable();
            if (f.BranchId is int branch) query = query.Where(p => p.BranchId == branch);
            if (f.Status is ReferencePriceStatus status) query = query.Where(p => p.Status == status);
            else if (!f.IncludeSuperseded) query = query.Where(p => p.Status != ReferencePriceStatus.Superseded);
            if (f.SupplierId is int supplier) query = query.Where(p => p.SupplierId == supplier);
            if (f.ProjectId is int project) query = query.Where(p => p.ProjectId == project);
            if (f.From is DateTime from) query = query.Where(p => p.PriceDate >= from.Date);
            if (f.To is DateTime to) query = query.Where(p => p.PriceDate <= to.Date);
            if (!string.IsNullOrWhiteSpace(f.Search))
            {
                var term = f.Search.Trim();
                query = query.Where(p => (p.ItemCode != null && p.ItemCode.Contains(term)) || p.ItemName.Contains(term) || p.Specifications.Contains(term)
                    || p.SourceDocument.Contains(term) || p.Supplier.Name.Contains(term));
            }
            var total = await query.CountAsync();
            var items = await query.Include(p => p.Supplier).Include(p => p.Project).Include(p => p.CreatedByUser).Include(p => p.ApprovedByUser)
                .OrderByDescending(p => p.PriceDate).ThenByDescending(p => p.Id)
                .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
            return (items, total);
        }

        public async Task<List<ReferencePrice>> GetGroupsAsync(IReadOnlyCollection<ReferencePrice> anchors, bool includeSuperseded)
        {
            if (anchors.Count == 0) return [];
            var keys = anchors.Select(ReferencePriceRules.Key).ToHashSet();
            var branches = anchors.Select(a => a.BranchId).Distinct().ToList();
            var codes = anchors.Where(a => !string.IsNullOrWhiteSpace(a.ItemCode)).Select(a => a.ItemCode!).Distinct().ToList();
            var names = anchors.Where(a => string.IsNullOrWhiteSpace(a.ItemCode)).Select(a => a.ItemName).Distinct().ToList();

            // المرشَّحون بالرمز أو الاسم (الحجم محدود بصفحة واحدة)، ثم المفتاح الدقيق (الوحدة والعملة وتطبيع المسافات) في الذاكرة
            var query = _context.ReferencePrices.AsNoTracking().Include(p => p.Supplier).Include(p => p.CreatedByUser).Include(p => p.ApprovedByUser).Include(p => p.Project)
                .Where(p => branches.Contains(p.BranchId)
                    && ((p.ItemCode != null && codes.Contains(p.ItemCode)) || (p.ItemCode == null && names.Contains(p.ItemName))));
            if (!includeSuperseded) query = query.Where(p => p.Status != ReferencePriceStatus.Superseded);
            var rows = await query.ToListAsync();
            return rows.Where(p => keys.Contains(ReferencePriceRules.Key(p))).ToList();
        }

        public Task<ReferencePrice?> GetCorrectionOfAsync(int priceId) => _context.ReferencePrices.AsNoTracking().FirstOrDefaultAsync(p => p.SupersedesPriceId == priceId);

        public async Task<List<ReferencePrice>> GetApprovedAncestorsAsync(ReferencePrice price)
        {
            var result = new List<ReferencePrice>();
            var current = price.SupersedesPriceId;
            for (var i = 0; i < MaxChain && current is int id; i++)
            {
                var ancestor = await _context.ReferencePrices.FirstOrDefaultAsync(p => p.Id == id);
                if (ancestor == null) break;
                if (ancestor.Status == ReferencePriceStatus.ApprovedForCurrentUse) result.Add(ancestor);
                current = ancestor.SupersedesPriceId;
            }
            return result;
        }

        public async Task<ReferencePrice> AddAsync(ReferencePrice price, AuditEntry audit)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            var now = DateTime.UtcNow;
            price.CreatedAt = now;
            price.UpdatedAt = now;
            _context.ReferencePrices.Add(price);
            await SaveAsync();
            _context.AuditLogs.Add(Log(audit with { EntityId = price.Id }, price));
            await SaveAsync();
            await transaction.CommitAsync();
            return price;
        }

        public async Task<ReferencePrice> CorrectAsync(ReferencePrice replacement, ReferencePrice original, ReferencePriceStatus statusAfter, byte[]? rowVersion, AuditEntry audit)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            var now = DateTime.UtcNow;
            if (rowVersion is { Length: > 0 }) _context.Entry(original).Property(p => p.RowVersion).OriginalValue = rowVersion;
            original.Status = statusAfter;
            original.UpdatedAt = now;
            replacement.SupersedesPriceId = original.Id;
            replacement.CreatedAt = now;
            replacement.UpdatedAt = now;
            _context.ReferencePrices.Add(replacement);
            await SaveAsync();
            _context.AuditLogs.Add(Log(audit with { EntityId = replacement.Id }, replacement));
            await SaveAsync();
            await transaction.CommitAsync();
            return replacement;
        }

        public async Task ApproveAsync(ReferencePrice price, IReadOnlyCollection<ReferencePrice> supersededAncestors, byte[]? rowVersion, AuditEntry audit)
        {
            if (rowVersion is { Length: > 0 }) _context.Entry(price).Property(p => p.RowVersion).OriginalValue = rowVersion;
            var now = DateTime.UtcNow;
            price.UpdatedAt = now;
            foreach (var ancestor in supersededAncestors) { ancestor.Status = ReferencePriceStatus.Superseded; ancestor.UpdatedAt = now; }
            _context.AuditLogs.Add(Log(audit, price));
            await SaveAsync();
        }

        public Task<List<ReferencePrice>> GetPossibleDuplicatesAsync(int branchId, IReadOnlyCollection<int> supplierIds, IReadOnlyCollection<DateTime> dates) =>
            _context.ReferencePrices.AsNoTracking()
                .Where(p => p.BranchId == branchId && p.Status != ReferencePriceStatus.Superseded && supplierIds.Contains(p.SupplierId) && dates.Contains(p.PriceDate))
                .ToListAsync();

        public async Task<ReferencePriceImport> ImportAsync(ReferencePriceImport batch, IReadOnlyCollection<ReferencePrice> prices, AuditEntry audit)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            var now = DateTime.UtcNow;
            batch.ImportedAt = now;
            batch.RowCount = prices.Count;
            _context.ReferencePriceImports.Add(batch);
            await SaveAsync();
            foreach (var price in prices) { price.ImportId = batch.Id; price.CreatedAt = now; price.UpdatedAt = now; }
            _context.ReferencePrices.AddRange(prices);
            await SaveAsync();
            _context.AuditLogs.Add(new AuditLog
            {
                CreatedAt = now, UserId = audit.UserId, Action = audit.Action, EntityType = audit.EntityType, EntityId = batch.Id, BranchId = batch.BranchId, Title = audit.Title, Details = audit.Details
            });
            await SaveAsync();
            await transaction.CommitAsync();
            return batch;
        }

        public Task<ProjectDocumentVersion?> GetVersionAsync(int versionId) =>
            _context.ProjectDocumentVersions.Include(v => v.Document).FirstOrDefaultAsync(v => v.Id == versionId);

        private static AuditLog Log(AuditEntry entry, ReferencePrice price) => new()
        {
            CreatedAt = DateTime.UtcNow, UserId = entry.UserId, Action = entry.Action, EntityType = entry.EntityType, EntityId = entry.EntityId,
            ProjectId = price.ProjectId, BranchId = price.BranchId, Title = entry.Title, Details = entry.Details
        };

        private async Task SaveAsync()
        {
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new InvalidOperationException("عدّل مستخدم آخر هذا السعر منذ فتحته — أعد فتحه وكرّر العملية");
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 } sql)
            {
                if (sql.Message.Contains("IX_ReferencePrices_OneCorrection")) throw new InvalidOperationException("لهذا السعر تصحيح قائم: صحّح السجل الأحدث");
                throw;
            }
        }
    }
}
