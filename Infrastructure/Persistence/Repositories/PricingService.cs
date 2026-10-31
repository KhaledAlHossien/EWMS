using System.Linq.Expressions;
using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    public class PricingService : IPricingService
    {
        private readonly DataContext _context;

        public PricingService(DataContext context) => _context = context;

        // ───────── الطلبات ─────────

        private IQueryable<PricingRequest> Graph() => _context.PricingRequests
            .Include(r => r.Project).ThenInclude(p => p!.Members).ThenInclude(m => m.User)
            .Include(r => r.Contract)
            .Include(r => r.RegisteredByUser)
            .Include(r => r.Assignee)
            .Include(r => r.AssignedByUser)
            .Include(r => r.RecommendedByUser)
            .Include(r => r.DecidedByUser)
            .Include(r => r.ResultSentByUser)
            .Include(r => r.Items)
            .Include(r => r.Offers).ThenInclude(o => o.Supplier)
            .Include(r => r.Offers).ThenInclude(o => o.RecordedByUser)
            .Include(r => r.Offers).ThenInclude(o => o.Lines)
            .AsSplitQuery();

        public Task<PricingRequest?> GetByIdAsync(int id) => Graph().FirstOrDefaultAsync(r => r.Id == id);

        public Task<PricingRequest?> GetByOfferAsync(int offerId) => Graph().FirstOrDefaultAsync(r => r.Offers.Any(o => o.Id == offerId));

        public Task<PricingRequest?> GetByLineAsync(int lineId) => Graph().FirstOrDefaultAsync(r => r.Offers.Any(o => o.Lines.Any(l => l.Id == lineId)));

        public async Task<(List<PricingRequest> Items, int TotalCount)> SearchAsync(Expression<Func<PricingRequest, bool>> visible, PricingFilter f, int page, int pageSize)
        {
            var query = _context.PricingRequests.AsNoTracking().Where(visible);

            if (f.BranchId is int branch) query = query.Where(r => r.BranchId == branch);
            if (f.ProjectId is int project) query = query.Where(r => r.ProjectId == project);
            if (f.Status is PricingRequestStatus status) query = query.Where(r => r.Status == status);
            if (f.MineUserId is int mine) query = query.Where(r => r.RegisteredByUserId == mine || r.AssigneeUserId == mine);
            if (!string.IsNullOrWhiteSpace(f.Search))
            {
                var term = f.Search.Trim();
                query = query.Where(r => r.Title.Contains(term) || (r.Project != null && r.Project.Name.Contains(term)));
            }
            if (f.OverdueOnly)
            {
                var today = f.Today.Date;
                query = query.Where(r => (r.Status == PricingRequestStatus.Registered || r.Status == PricingRequestStatus.Collecting || r.Status == PricingRequestStatus.Recommended)
                    && r.DueDate != null && r.DueDate < today);
            }

            var total = await query.CountAsync();
            var items = await query.Include(r => r.Project).Include(r => r.Assignee).Include(r => r.RegisteredByUser).Include(r => r.Items).Include(r => r.Offers)
                .OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id)
                .Skip((page - 1) * pageSize).Take(pageSize).AsSplitQuery().ToListAsync();
            return (items, total);
        }

        public async Task<PricingRequest> AddAsync(PricingRequest request, AuditEntry audit)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            var now = DateTime.UtcNow;
            request.CreatedAt = now;
            request.UpdatedAt = now;
            _context.PricingRequests.Add(request);
            await SaveAsync();
            _context.AuditLogs.Add(NewLog(audit with { EntityId = request.Id }, request));
            await SaveAsync();
            await transaction.CommitAsync();
            return request;
        }

        public async Task UpdateAsync(PricingRequest request, byte[]? rowVersion, AuditEntry audit)
        {
            Touch(request, rowVersion);
            _context.AuditLogs.Add(NewLog(audit, request));
            await SaveAsync();
        }

        public async Task ReplaceItemsAsync(PricingRequest request, IReadOnlyCollection<PricingRequestItem> removed, IReadOnlyCollection<PricingRequestItem> added, byte[]? rowVersion, AuditEntry audit)
        {
            Touch(request, rowVersion);
            _context.PricingRequestItems.RemoveRange(removed);
            foreach (var item in added) item.PricingRequestId = request.Id;
            _context.PricingRequestItems.AddRange(added);
            _context.AuditLogs.Add(NewLog(audit, request));
            await SaveAsync();
        }

        // ───────── العروض وأسعار البنود ─────────

        public async Task AddOfferAsync(PricingRequest request, PricingOffer offer, AuditEntry audit)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            offer.PricingRequestId = request.Id;
            _context.PricingOffers.Add(offer);
            Touch(request, null);
            await SaveAsync();
            _context.AuditLogs.Add(NewLog(audit, request));
            await SaveAsync();
            await transaction.CommitAsync();
        }

        public async Task SaveOfferAsync(PricingRequest request, PricingOffer offer, IReadOnlyCollection<PricingOfferLine> removed, byte[]? rowVersion, AuditEntry audit)
        {
            if (rowVersion is { Length: > 0 }) _context.Entry(offer).Property(o => o.RowVersion).OriginalValue = rowVersion;
            if (removed.Count > 0) _context.PricingOfferLines.RemoveRange(removed);
            Touch(request, null);
            _context.AuditLogs.Add(NewLog(audit, request));
            await SaveAsync();
        }

        public async Task SelectLineAsync(PricingRequest request, PricingOfferLine line, PricingOfferLine? previous, string reason, AuditEntry audit)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            if (previous != null && previous.Id != line.Id)
            {
                // يُلغى المختار سابقاً أولاً: فهرس فريد لسطر مختار واحد لكل بند
                previous.IsSelected = false;
                previous.SelectionReason = string.Empty;
                await SaveAsync();
            }
            line.IsSelected = true;
            line.SelectionReason = reason;
            Touch(request, null);
            _context.AuditLogs.Add(NewLog(audit, request));
            await SaveAsync();
            await transaction.CommitAsync();
        }

        public async Task UnselectLineAsync(PricingRequest request, PricingOfferLine line, AuditEntry audit)
        {
            line.IsSelected = false;
            line.SelectionReason = string.Empty;
            Touch(request, null);
            _context.AuditLogs.Add(NewLog(audit, request));
            await SaveAsync();
        }

        // ───────── محاضر الاتفاق ─────────

        private IQueryable<PricingAgreement> AgreementGraph() => _context.PricingAgreements
            .Include(a => a.Supplier).Include(a => a.Project).Include(a => a.PreparedByUser).Include(a => a.ApprovedByUser).Include(a => a.Lines)
            .AsSplitQuery();

        public Task<PricingAgreement?> GetAgreementAsync(int id) => AgreementGraph().FirstOrDefaultAsync(a => a.Id == id);

        public async Task<(List<PricingAgreement> Items, int TotalCount)> SearchAgreementsAsync(AgreementFilter f, int page, int pageSize)
        {
            var query = _context.PricingAgreements.AsNoTracking().AsQueryable();
            if (f.BranchId is int branch) query = query.Where(a => a.BranchId == branch);
            if (f.Status is AgreementStatus status) query = query.Where(a => a.Status == status);
            if (f.SupplierId is int supplier) query = query.Where(a => a.SupplierId == supplier);
            if (!string.IsNullOrWhiteSpace(f.Search))
            {
                var term = f.Search.Trim();
                query = query.Where(a => (a.Number != null && a.Number.Contains(term)) || a.Supplier.Name.Contains(term) || (a.Project != null && a.Project.Name.Contains(term)));
            }
            var total = await query.CountAsync();
            var items = await query.Include(a => a.Supplier).Include(a => a.Project).Include(a => a.PreparedByUser).Include(a => a.Lines)
                .OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id)
                .Skip((page - 1) * pageSize).Take(pageSize).AsSplitQuery().ToListAsync();
            return (items, total);
        }

        public Task<bool> AgreementNumberExistsAsync(int branchId, string number, int? excludeId = null) =>
            _context.PricingAgreements.AnyAsync(a => a.BranchId == branchId && a.Number == number && (excludeId == null || a.Id != excludeId));

        public async Task<PricingAgreement> AddAgreementAsync(PricingAgreement agreement, AuditEntry audit)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            var now = DateTime.UtcNow;
            agreement.CreatedAt = now;
            agreement.UpdatedAt = now;
            _context.PricingAgreements.Add(agreement);
            await SaveAsync();
            _context.AuditLogs.Add(AgreementLog(audit with { EntityId = agreement.Id }, agreement));
            await SaveAsync();
            await transaction.CommitAsync();
            return agreement;
        }

        public async Task SaveAgreementAsync(PricingAgreement agreement, IReadOnlyCollection<PricingAgreementLine> removed, byte[]? rowVersion, AuditEntry audit)
        {
            if (rowVersion is { Length: > 0 }) _context.Entry(agreement).Property(a => a.RowVersion).OriginalValue = rowVersion;
            if (removed.Count > 0) _context.PricingAgreementLines.RemoveRange(removed);
            agreement.UpdatedAt = DateTime.UtcNow;
            _context.AuditLogs.Add(AgreementLog(audit, agreement));
            await SaveAsync();
        }

        // ───────── التذكيرات ─────────

        public Task<List<PricingRequest>> GetOverdueCandidatesAsync(DateTime today, int take)
        {
            var day = today.Date;
            return _context.PricingRequests.AsNoTracking()
                .Where(r => (r.Status == PricingRequestStatus.Registered || r.Status == PricingRequestStatus.Collecting || r.Status == PricingRequestStatus.Recommended)
                            && r.DueDate != null && r.DueDate < day && (r.OverdueNotifiedFor == null || r.OverdueNotifiedFor != r.DueDate))
                .OrderBy(r => r.DueDate).ThenBy(r => r.Id).Take(take).ToListAsync();
        }

        public Task MarkOverdueNotifiedAsync(int requestId, DateTime dueDate) =>
            _context.PricingRequests.Where(r => r.Id == requestId && r.DueDate == dueDate)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.OverdueNotifiedFor, dueDate));

        // ───────── الوثائق ─────────

        public Task<ProjectDocumentVersion?> GetVersionAsync(int versionId) =>
            _context.ProjectDocumentVersions.Include(v => v.Document).FirstOrDefaultAsync(v => v.Id == versionId);

        // ───────── مساعدات ─────────

        private void Touch(PricingRequest request, byte[]? rowVersion)
        {
            if (rowVersion is { Length: > 0 }) _context.Entry(request).Property(r => r.RowVersion).OriginalValue = rowVersion;
            request.UpdatedAt = DateTime.UtcNow;
        }

        private static AuditLog NewLog(AuditEntry entry, PricingRequest request) => new()
        {
            CreatedAt = DateTime.UtcNow, UserId = entry.UserId, Action = entry.Action, EntityType = entry.EntityType, EntityId = entry.EntityId,
            ProjectId = request.ProjectId, BranchId = request.BranchId, Title = entry.Title, Details = entry.Details
        };

        private static AuditLog AgreementLog(AuditEntry entry, PricingAgreement agreement) => new()
        {
            CreatedAt = DateTime.UtcNow, UserId = entry.UserId, Action = entry.Action, EntityType = entry.EntityType, EntityId = entry.EntityId,
            ProjectId = agreement.ProjectId, BranchId = agreement.BranchId, Title = entry.Title, Details = entry.Details
        };

        private async Task SaveAsync()
        {
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new InvalidOperationException("عدّل مستخدم آخر هذا السجل منذ فتحته — أعد فتحه وكرّر العملية");
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 } sql)
            {
                if (sql.Message.Contains("IX_PricingOffers_PricingRequestId_SupplierId")) throw new InvalidOperationException("لهذا المورد عرض مسجَّل على الطلب");
                if (sql.Message.Contains("IX_PricingOfferLines_PricingOfferId_PricingRequestItemId")) throw new InvalidOperationException("بند مكرر في العرض");
                if (sql.Message.Contains("IX_PricingOfferLines_OneSelected")) throw new InvalidOperationException("اختيرت لهذا البند أسعار بالتزامن — أعد فتح الطلب وكرّر");
                if (sql.Message.Contains("IX_PricingAgreements_BranchNumber")) throw new InvalidOperationException("رقم المحضر مستعمل في الفرع");
                throw;
            }
        }
    }
}
