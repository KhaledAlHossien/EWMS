using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    public class PriceReconciliationService : IPriceReconciliationService
    {
        private readonly DataContext _context;

        public PriceReconciliationService(DataContext context) => _context = context;

        private IQueryable<PriceReconciliation> Graph() => _context.PriceReconciliations
            .Include(r => r.Supplier).Include(r => r.Project).Include(r => r.Contract).Include(r => r.PreparedByUser).Include(r => r.DecidedByUser)
            .Include(r => r.ReferredByUser).Include(r => r.Lines)
            .AsSplitQuery();

        public Task<PriceReconciliation?> GetByIdAsync(int id) => Graph().FirstOrDefaultAsync(r => r.Id == id);

        public async Task<(List<PriceReconciliation> Items, int TotalCount)> SearchAsync(ReconciliationFilter f, int page, int pageSize)
        {
            var query = _context.PriceReconciliations.AsNoTracking().AsQueryable();
            if (f.BranchId is int branch) query = query.Where(r => r.BranchId == branch);
            if (f.Status is ReconciliationStatus status) query = query.Where(r => r.Status == status);
            if (f.SupplierId is int supplier) query = query.Where(r => r.SupplierId == supplier);
            if (f.ProjectId is int project) query = query.Where(r => r.ProjectId == project);
            if (!string.IsNullOrWhiteSpace(f.Search))
            {
                var term = f.Search.Trim();
                query = query.Where(r => r.DocumentNumber.Contains(term) || r.Supplier.Name.Contains(term) || (r.Project != null && r.Project.Name.Contains(term)));
            }
            var total = await query.CountAsync();
            var items = await query.Include(r => r.Supplier).Include(r => r.Project).Include(r => r.PreparedByUser).Include(r => r.Lines)
                .OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id)
                .Skip((page - 1) * pageSize).Take(pageSize).AsSplitQuery().ToListAsync();
            return (items, total);
        }

        public Task<bool> DocumentExistsAsync(int branchId, int supplierId, ReconciliationDocumentKind kind, string number, int? excludeId = null) =>
            _context.PriceReconciliations.AnyAsync(r => r.BranchId == branchId && r.SupplierId == supplierId && r.DocumentKind == kind && r.DocumentNumber == number
                && (excludeId == null || r.Id != excludeId));

        public async Task<PriceReconciliation> AddAsync(PriceReconciliation reconciliation, AuditEntry audit)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            var now = DateTime.UtcNow;
            reconciliation.CreatedAt = now;
            reconciliation.UpdatedAt = now;
            _context.PriceReconciliations.Add(reconciliation);
            await SaveChangesAsync();
            _context.AuditLogs.Add(Log(audit with { EntityId = reconciliation.Id }, reconciliation));
            await SaveChangesAsync();
            await transaction.CommitAsync();
            return reconciliation;
        }

        public async Task SaveAsync(PriceReconciliation reconciliation, IReadOnlyCollection<PriceReconciliationLine> removed, byte[]? rowVersion, AuditEntry audit)
        {
            if (rowVersion is { Length: > 0 }) _context.Entry(reconciliation).Property(r => r.RowVersion).OriginalValue = rowVersion;
            if (removed.Count > 0) _context.PriceReconciliationLines.RemoveRange(removed);
            reconciliation.UpdatedAt = DateTime.UtcNow;
            _context.AuditLogs.Add(Log(audit, reconciliation));
            await SaveChangesAsync();
        }

        public Task<PricingOfferLine?> GetOfferLineAsync(int lineId) => _context.PricingOfferLines
            .Include(l => l.PricingOffer).ThenInclude(o => o.PricingRequest).Include(l => l.Item)
            .FirstOrDefaultAsync(l => l.Id == lineId);

        private static readonly PricingRequestStatus[] Decided = [PricingRequestStatus.Approved, PricingRequestStatus.ResultSent];

        public Task<List<PricingOfferLine>> GetOfferLineCandidatesAsync(int branchId, int supplierId, string? search, int take)
        {
            var query = _context.PricingOfferLines.AsNoTracking()
                .Include(l => l.PricingOffer).ThenInclude(o => o.PricingRequest).Include(l => l.Item)
                .Where(l => l.IsSelected && l.PricingOffer.SupplierId == supplierId && l.PricingOffer.PricingRequest.BranchId == branchId
                            && Decided.Contains(l.PricingOffer.PricingRequest.Status));
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(l => l.Item.Description.Contains(term) || l.PricingOffer.PricingRequest.Title.Contains(term));
            }
            return query.OrderByDescending(l => l.PricingOffer.PricingRequest.CreatedAt).ThenBy(l => l.Id).Take(take).ToListAsync();
        }

        public Task<List<ReferencePrice>> GetReferencePriceCandidatesAsync(int branchId, int supplierId, string? search, int take)
        {
            var query = _context.ReferencePrices.AsNoTracking()
                .Where(p => p.BranchId == branchId && p.SupplierId == supplierId && p.Status == ReferencePriceStatus.ApprovedForCurrentUse);
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(p => p.ItemName.Contains(term) || (p.ItemCode != null && p.ItemCode.Contains(term)));
            }
            return query.OrderByDescending(p => p.PriceDate).ThenByDescending(p => p.Id).Take(take).ToListAsync();
        }

        public Task<ReferencePrice?> GetReferencePriceAsync(int id) => _context.ReferencePrices.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);

        public Task<ProjectDocumentVersion?> GetVersionAsync(int versionId) =>
            _context.ProjectDocumentVersions.Include(v => v.Document).FirstOrDefaultAsync(v => v.Id == versionId);

        private static AuditLog Log(AuditEntry entry, PriceReconciliation reconciliation) => new()
        {
            CreatedAt = DateTime.UtcNow, UserId = entry.UserId, Action = entry.Action, EntityType = entry.EntityType, EntityId = entry.EntityId,
            ProjectId = reconciliation.ProjectId, BranchId = reconciliation.BranchId, Title = entry.Title, Details = entry.Details
        };

        private async Task SaveChangesAsync()
        {
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new InvalidOperationException("عدّل مستخدم آخر هذه المطابقة منذ فتحتها — أعد فتحها وكرّر العملية");
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 } sql)
            {
                if (sql.Message.Contains("IX_PriceReconciliations_Document")) throw new InvalidOperationException("هذا المستند مسجَّل سابقاً لهذا المورد في الفرع");
                throw;
            }
        }
    }
}
