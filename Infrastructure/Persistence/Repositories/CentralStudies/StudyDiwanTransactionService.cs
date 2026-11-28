using Application.DTOs.Request;
using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    public class StudyDiwanTransactionService : StudyRepositoryBase, IStudyDiwanTransactionService
    {
        public StudyDiwanTransactionService(DataContext context) : base(context) { }

        private IQueryable<StudyDiwanTransaction> WithDetails() => _context.StudyDiwanTransactions
            .Include(t => t.Response).ThenInclude(r => r.Assignment).ThenInclude(a => a.Request).ThenInclude(q => q.Branch)
            .Include(t => t.Response).ThenInclude(r => r.StartOrder)
            .Include(t => t.CreatedBy)
            .AsSplitQuery();

        public async Task<StudyDiwanTransaction?> GetByIdAsync(int id) =>
            await WithDetails().FirstOrDefaultAsync(t => t.Id == id);

        public async Task<(List<StudyDiwanTransaction> Items, int Total)> SearchAsync(
            StudyDocumentFilterDto filter, int? branchLimit, int page, int pageSize)
        {
            var query = _context.StudyDiwanTransactions.AsQueryable();
            if (branchLimit != null) query = query.Where(t => t.Response.Assignment.Request.BranchId == branchLimit);
            if (filter.BranchId is int branchId) query = query.Where(t => t.Response.Assignment.Request.BranchId == branchId);
            if (filter.Status is int status) query = query.Where(t => (int)t.Status == status);
            if (filter.From is DateTime from) query = query.Where(t => t.DispatchDate >= from.Date);
            if (filter.To is DateTime to) query = query.Where(t => t.DispatchDate <= to.Date);
            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var text = filter.Search.Trim();
                query = query.Where(t => t.TransactionNumber.Contains(text) || t.DispatchNumber.Contains(text)
                                         || (t.ReplyNumber != null && t.ReplyNumber.Contains(text))
                                         || t.Response.ResponseNumber.Contains(text)
                                         || (t.Response.Assignment.ProjectCode != null && t.Response.Assignment.ProjectCode.Contains(text))
                                         || t.Response.Assignment.Request.MainBeneficiary.Contains(text));
            }

            var total = await query.CountAsync();
            var ids = await query.OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.Id)
                .Skip((page - 1) * pageSize).Take(pageSize).Select(t => t.Id).ToListAsync();
            var items = await WithDetails().Where(t => ids.Contains(t.Id)).AsNoTracking().ToListAsync();
            return (items.OrderBy(t => ids.IndexOf(t.Id)).ToList(), total);
        }

        public async Task<bool> ExistsForResponseAsync(int responseId) =>
            await _context.StudyDiwanTransactions.AnyAsync(t => t.ResponseId == responseId);

        public async Task<StudyDiwanTransaction> AddAsync(StudyDiwanTransaction transaction)
        {
            _context.StudyDiwanTransactions.Add(transaction);
            await SaveChangesAsync();
            return transaction;
        }

        public async Task UpdateAsync(StudyDiwanTransaction transaction, byte[]? expectedRowVersion = null)
        {
            ExpectVersion(transaction, expectedRowVersion);
            await SaveChangesAsync();
        }

        public async Task DeleteAsync(StudyDiwanTransaction transaction)
        {
            _context.StudyDiwanTransactions.Remove(transaction);
            await SaveChangesAsync();
        }
    }
}
