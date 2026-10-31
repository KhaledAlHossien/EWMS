using System.Linq.Expressions;
using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    public class ContractService : IContractService
    {
        private readonly DataContext _context;

        public ContractService(DataContext context) => _context = context;

        // ───────── المقاولون ─────────

        public Task<List<Contractor>> GetContractorsAsync(int? branchId, bool includeInactive) =>
            _context.Contractors.AsNoTracking()
                .Where(c => (branchId == null || c.BranchId == branchId) && (includeInactive || c.IsActive))
                .OrderBy(c => c.Name).ToListAsync();

        public Task<Contractor?> GetContractorAsync(int id) => _context.Contractors.FirstOrDefaultAsync(c => c.Id == id);

        public async Task<Contractor> AddContractorAsync(Contractor contractor, AuditEntry audit)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            _context.Contractors.Add(contractor);
            await SaveAsync();
            _context.AuditLogs.Add(NewLog(audit with { EntityId = contractor.Id }, null, contractor.BranchId));
            await SaveAsync();
            await transaction.CommitAsync();
            return contractor;
        }

        public async Task UpdateContractorAsync(Contractor contractor, byte[]? rowVersion, AuditEntry audit)
        {
            if (rowVersion is { Length: > 0 }) _context.Entry(contractor).Property(c => c.RowVersion).OriginalValue = rowVersion;
            _context.AuditLogs.Add(NewLog(audit, null, contractor.BranchId));
            await SaveAsync();
        }

        public async Task DeleteContractorAsync(Contractor contractor, AuditEntry audit)
        {
            _context.Contractors.Remove(contractor);
            _context.AuditLogs.Add(NewLog(audit, null, contractor.BranchId));
            await SaveAsync();
        }

        public Task<bool> ContractorNameExistsAsync(int branchId, string name, int? excludeId = null) =>
            _context.Contractors.AnyAsync(c => c.BranchId == branchId && c.Name == name && (excludeId == null || c.Id != excludeId));

        public async Task<bool> ContractorInUseAsync(int id) =>
            await _context.Contracts.AnyAsync(c => c.ContractorId == id)
            || await _context.ContractOffers.AnyAsync(o => o.ContractorId == id)
            || await _context.ContractorEvaluations.AnyAsync(e => e.ContractorId == id);

        public Task<List<ContractorEvaluation>> GetContractorEvaluationsAsync(int contractorId, Expression<Func<Project, bool>> visibleProjects)
        {
            var visibleIds = _context.Projects.Where(visibleProjects).Select(p => p.Id);
            return _context.ContractorEvaluations.AsNoTracking()
                .Include(e => e.Contractor).Include(e => e.EvaluatedByUser).Include(e => e.ReviewedByUser)
                .Include(e => e.Contract).ThenInclude(c => c.Project)
                .Include(e => e.Scores).ThenInclude(s => s.Criterion)
                .Where(e => e.ContractorId == contractorId && e.Status == EvaluationStatus.Reviewed && visibleIds.Contains(e.Contract.ProjectId))
                .OrderByDescending(e => e.EvaluationDate).ThenByDescending(e => e.Id)
                .AsSplitQuery().ToListAsync();
        }

        // ───────── معايير التقييم ─────────

        public Task<List<EvaluationCriterion>> GetCriteriaAsync(int? branchId, bool includeInactive) =>
            _context.EvaluationCriteria.AsNoTracking()
                .Where(c => (branchId == null || c.BranchId == branchId) && (includeInactive || c.IsActive))
                .OrderBy(c => c.SortOrder).ThenBy(c => c.Name).ToListAsync();

        public Task<EvaluationCriterion?> GetCriterionAsync(int id) => _context.EvaluationCriteria.FirstOrDefaultAsync(c => c.Id == id);

        public Task<Dictionary<int, EvaluationCriterion>> GetCriteriaByIdsAsync(IReadOnlyCollection<int> ids) =>
            _context.EvaluationCriteria.AsNoTracking().Where(c => ids.Contains(c.Id)).ToDictionaryAsync(c => c.Id);

        public async Task<EvaluationCriterion> AddCriterionAsync(EvaluationCriterion criterion, AuditEntry audit)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            _context.EvaluationCriteria.Add(criterion);
            await SaveAsync();
            _context.AuditLogs.Add(NewLog(audit with { EntityId = criterion.Id }, null, criterion.BranchId));
            await SaveAsync();
            await transaction.CommitAsync();
            return criterion;
        }

        public async Task UpdateCriterionAsync(EvaluationCriterion criterion, byte[]? rowVersion, AuditEntry audit)
        {
            if (rowVersion is { Length: > 0 }) _context.Entry(criterion).Property(c => c.RowVersion).OriginalValue = rowVersion;
            _context.AuditLogs.Add(NewLog(audit, null, criterion.BranchId));
            await SaveAsync();
        }

        public async Task DeleteCriterionAsync(EvaluationCriterion criterion, AuditEntry audit)
        {
            _context.EvaluationCriteria.Remove(criterion);
            _context.AuditLogs.Add(NewLog(audit, null, criterion.BranchId));
            await SaveAsync();
        }

        public Task<bool> CriterionNameExistsAsync(int branchId, string name, int? excludeId = null) =>
            _context.EvaluationCriteria.AnyAsync(c => c.BranchId == branchId && c.Name == name && (excludeId == null || c.Id != excludeId));

        public Task<bool> CriterionInUseAsync(int id) => _context.EvaluationScores.AnyAsync(s => s.CriterionId == id);

        // ───────── العقود ─────────

        private IQueryable<Contract> Graph() => _context.Contracts
            .Include(c => c.Project).ThenInclude(p => p.Members).ThenInclude(m => m.User)
            .Include(c => c.Correspondence)
            .Include(c => c.Contractor)
            .Include(c => c.CreatedByUser)
            .Include(c => c.Offers).ThenInclude(o => o.Contractor)
            .Include(c => c.Offers).ThenInclude(o => o.RecordedByUser)
            .Include(c => c.Amendments).ThenInclude(a => a.RecordedByUser)
            .Include(c => c.Committees).ThenInclude(x => x.Members).ThenInclude(m => m.User)
            .Include(c => c.RetentionEntries).ThenInclude(r => r.RecordedByUser)
            .Include(c => c.RetentionEntries).ThenInclude(r => r.Decisions).ThenInclude(d => d.RecordedByUser)
            .Include(c => c.Evaluations).ThenInclude(e => e.Scores).ThenInclude(s => s.Criterion)
            .Include(c => c.Evaluations).ThenInclude(e => e.Contractor)
            .Include(c => c.Evaluations).ThenInclude(e => e.EvaluatedByUser)
            .Include(c => c.Evaluations).ThenInclude(e => e.ReviewedByUser)
            .AsSplitQuery();

        public Task<Contract?> GetByIdAsync(int id) => Graph().FirstOrDefaultAsync(c => c.Id == id);

        public Task<Contract?> GetByOfferAsync(int offerId) => Graph().FirstOrDefaultAsync(c => c.Offers.Any(o => o.Id == offerId));

        public Task<Contract?> GetByCommitteeAsync(int committeeId) => Graph().FirstOrDefaultAsync(c => c.Committees.Any(x => x.Id == committeeId));

        public Task<Contract?> GetByRetentionEntryAsync(int entryId) => Graph().FirstOrDefaultAsync(c => c.RetentionEntries.Any(r => r.Id == entryId));

        public Task<Contract?> GetByEvaluationAsync(int evaluationId) => Graph().FirstOrDefaultAsync(c => c.Evaluations.Any(e => e.Id == evaluationId));

        public async Task<(List<Contract> Items, int TotalCount)> SearchAsync(Expression<Func<Project, bool>> visibleProjects, ContractFilter f, int page, int pageSize)
        {
            var visibleIds = _context.Projects.Where(visibleProjects).Select(p => p.Id);
            var query = _context.Contracts.AsNoTracking().Where(c => visibleIds.Contains(c.ProjectId));

            if (f.ProjectId is int project) query = query.Where(c => c.ProjectId == project);
            if (f.BranchId is int branch) query = query.Where(c => c.BranchId == branch);
            if (f.Status is ContractStatus status) query = query.Where(c => c.Status == status);
            if (f.Method is ContractingMethod method) query = query.Where(c => c.Method == method);
            if (f.ContractorId is int contractor) query = query.Where(c => c.ContractorId == contractor);
            if (!string.IsNullOrWhiteSpace(f.Search))
            {
                var term = f.Search.Trim();
                query = query.Where(c => (c.Number != null && c.Number.Contains(term)) || c.Project.Name.Contains(term) || (c.Contractor != null && c.Contractor.Name.Contains(term))
                    || (c.TenderNumber != null && c.TenderNumber.Contains(term)));
            }
            if (f.WarrantyEndingOnly)
            {
                var today = f.Today.Date;
                var until = today.AddDays(f.WarrantyWindowDays);
                query = query.Where(c => (c.Status == ContractStatus.Active || c.Status == ContractStatus.Completed)
                    && c.WarrantyEnd != null && c.WarrantyEnd >= today && c.WarrantyEnd <= until);
            }

            var total = await query.CountAsync();
            var items = await query.Include(c => c.Project).Include(c => c.Contractor).Include(c => c.Amendments)
                .OrderByDescending(c => c.CreatedAt).ThenByDescending(c => c.Id)
                .Skip((page - 1) * pageSize).Take(pageSize).AsSplitQuery().ToListAsync();
            return (items, total);
        }

        public Task<bool> NumberExistsAsync(int branchId, string number, int? excludeContractId = null) =>
            _context.Contracts.AnyAsync(c => c.BranchId == branchId && c.Number == number && (excludeContractId == null || c.Id != excludeContractId));

        public async Task<Contract> AddAsync(Contract contract, AuditEntry audit)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            var now = DateTime.UtcNow;
            contract.CreatedAt = now;
            contract.UpdatedAt = now;
            _context.Contracts.Add(contract);
            await SaveAsync();
            _context.AuditLogs.Add(NewLog(audit with { EntityId = contract.Id }, contract.ProjectId, contract.BranchId));
            await SaveAsync();
            await transaction.CommitAsync();
            return contract;
        }

        public async Task UpdateAsync(Contract contract, byte[]? rowVersion, AuditEntry audit)
        {
            Touch(contract, rowVersion);
            _context.AuditLogs.Add(NewLog(audit, contract.ProjectId, contract.BranchId));
            await SaveAsync();
        }

        public async Task AddOfferAsync(Contract contract, ContractOffer offer, AuditEntry audit)
        {
            offer.ContractId = contract.Id;
            _context.ContractOffers.Add(offer);
            Touch(contract, null);
            _context.AuditLogs.Add(NewLog(audit, contract.ProjectId, contract.BranchId));
            await SaveAsync();
        }

        public async Task AddAmendmentAsync(Contract contract, ContractAmendment amendment, AuditEntry audit)
        {
            amendment.ContractId = contract.Id;
            _context.ContractAmendments.Add(amendment);
            Touch(contract, null);
            _context.AuditLogs.Add(NewLog(audit, contract.ProjectId, contract.BranchId));
            await SaveAsync();
        }

        public async Task AddRetentionAsync(Contract contract, RetentionEntry entry, AuditEntry audit)
        {
            entry.ContractId = contract.Id;
            _context.RetentionEntries.Add(entry);
            Touch(contract, null);
            _context.AuditLogs.Add(NewLog(audit, contract.ProjectId, contract.BranchId));
            await SaveAsync();
        }

        public async Task AddRetentionDecisionAsync(Contract contract, RetentionDecision decision, AuditEntry audit)
        {
            _context.RetentionDecisions.Add(decision);
            Touch(contract, null);
            _context.AuditLogs.Add(NewLog(audit, contract.ProjectId, contract.BranchId));
            await SaveAsync();
        }

        public async Task SaveCommitteeAsync(Contract contract, Committee committee, bool isNew, IReadOnlyCollection<CommitteeMember> added,
            IReadOnlyCollection<CommitteeMember> removed, byte[]? rowVersion, AuditEntry audit)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            Touch(contract, null);
            if (isNew)
            {
                committee.ContractId = contract.Id;
                _context.Committees.Add(committee);
            }
            else
            {
                if (rowVersion is { Length: > 0 }) _context.Entry(committee).Property(c => c.RowVersion).OriginalValue = rowVersion;
                _context.CommitteeMembers.RemoveRange(removed);
            }
            await SaveAsync();

            // الأعضاء الجدد بعد حذف القدامى (رئيس واحد في الفهرس الفريد)
            if (!isNew && added.Count > 0)
            {
                foreach (var member in added) member.CommitteeId = committee.Id;
                _context.CommitteeMembers.AddRange(added);
            }
            _context.AuditLogs.Add(NewLog(audit, contract.ProjectId, contract.BranchId));
            await SaveAsync();
            await transaction.CommitAsync();
        }

        // ───────── تقييم المقاول ─────────

        public async Task AddEvaluationAsync(Contract contract, ContractorEvaluation evaluation, AuditEntry audit)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            evaluation.ContractId = contract.Id;
            _context.ContractorEvaluations.Add(evaluation);
            Touch(contract, null);
            await SaveAsync();
            _context.AuditLogs.Add(NewLog(audit with { EntityId = evaluation.Id }, contract.ProjectId, contract.BranchId));
            await SaveAsync();
            await transaction.CommitAsync();
        }

        public async Task SaveEvaluationAsync(Contract contract, ContractorEvaluation evaluation, IReadOnlyCollection<EvaluationScore> removed, byte[]? rowVersion, AuditEntry audit)
        {
            if (rowVersion is { Length: > 0 }) _context.Entry(evaluation).Property(e => e.RowVersion).OriginalValue = rowVersion;
            evaluation.UpdatedAt = DateTime.UtcNow;
            if (removed.Count > 0) _context.EvaluationScores.RemoveRange(removed);
            Touch(contract, null);
            _context.AuditLogs.Add(NewLog(audit, contract.ProjectId, contract.BranchId));
            await SaveAsync();
        }

        public async Task DeleteEvaluationAsync(Contract contract, ContractorEvaluation evaluation, AuditEntry audit)
        {
            _context.EvaluationScores.RemoveRange(evaluation.Scores);
            _context.ContractorEvaluations.Remove(evaluation);
            Touch(contract, null);
            _context.AuditLogs.Add(NewLog(audit, contract.ProjectId, contract.BranchId));
            await SaveAsync();
        }

        // ───────── التذكيرات ─────────

        public Task<List<Contract>> GetWarrantyCandidatesAsync(DateTime today, int windowDays, int take)
        {
            var day = today.Date;
            var until = day.AddDays(windowDays);
            return _context.Contracts.AsNoTracking()
                .Include(c => c.Project).ThenInclude(p => p.Members).ThenInclude(m => m.User)
                .Where(c => (c.Status == ContractStatus.Active || c.Status == ContractStatus.Completed)
                            && c.WarrantyEnd != null && c.WarrantyEnd >= day && c.WarrantyEnd <= until
                            && (c.WarrantyNotifiedFor == null || c.WarrantyNotifiedFor != c.WarrantyEnd))
                .OrderBy(c => c.WarrantyEnd).ThenBy(c => c.Id).Take(take).AsSplitQuery().ToListAsync();
        }

        public Task MarkWarrantyNotifiedAsync(int contractId, DateTime warrantyEnd) =>
            _context.Contracts.Where(c => c.Id == contractId && c.WarrantyEnd == warrantyEnd)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.WarrantyNotifiedFor, warrantyEnd));

        // ───────── ملف المشروع ─────────

        public Task<List<Contract>> GetByProjectAsync(int projectId) =>
            _context.Contracts.AsNoTracking().Include(c => c.RetentionEntries).ThenInclude(r => r.Decisions)
                .Where(c => c.ProjectId == projectId).AsSplitQuery().ToListAsync();

        // ───────── الوثائق ─────────

        public Task<ProjectDocumentVersion?> GetVersionAsync(int versionId) =>
            _context.ProjectDocumentVersions.Include(v => v.Document).FirstOrDefaultAsync(v => v.Id == versionId);

        // ───────── مساعدات ─────────

        private void Touch(Contract contract, byte[]? rowVersion)
        {
            if (rowVersion is { Length: > 0 }) _context.Entry(contract).Property(c => c.RowVersion).OriginalValue = rowVersion;
            contract.UpdatedAt = DateTime.UtcNow;
        }

        private static AuditLog NewLog(AuditEntry entry, int? projectId, int branchId) => new()
        {
            CreatedAt = DateTime.UtcNow, UserId = entry.UserId, Action = entry.Action, EntityType = entry.EntityType, EntityId = entry.EntityId,
            ProjectId = projectId, BranchId = branchId, Title = entry.Title, Details = entry.Details
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
                if (sql.Message.Contains("IX_Contractors_BranchId_Name")) throw new InvalidOperationException("يوجد مقاول بهذا الاسم في الفرع");
                if (sql.Message.Contains("IX_EvaluationCriteria_BranchId_Name")) throw new InvalidOperationException("يوجد معيار بهذا الاسم في الفرع");
                if (sql.Message.Contains("IX_Contracts_BranchNumber")) throw new InvalidOperationException("رقم العقد الرسمي مستعمل في الفرع");
                if (sql.Message.Contains("IX_ContractOffers_OneAwarded")) throw new InvalidOperationException("رسا التعاقد على عرض مسبقاً");
                if (sql.Message.Contains("IX_ContractOffers_ContractId_ContractorId")) throw new InvalidOperationException("لهذا المقاول عرض مسجَّل على العقد");
                if (sql.Message.Contains("IX_ContractAmendments_ContractId_Number")) throw new InvalidOperationException("سُجّل ملحق بالتزامن — أعد فتح العقد وكرّر");
                if (sql.Message.Contains("IX_CommitteeMembers_OneChair")) throw new InvalidOperationException("للجنة رئيس واحد فقط");
                if (sql.Message.Contains("IX_CommitteeMembers_OnePerUser")) throw new InvalidOperationException("موظف مكرر في أعضاء اللجنة");
                if (sql.Message.Contains("IX_EvaluationScores_EvaluationId_CriterionId")) throw new InvalidOperationException("معيار مكرر في التقييم");
                throw;
            }
        }
    }
}
