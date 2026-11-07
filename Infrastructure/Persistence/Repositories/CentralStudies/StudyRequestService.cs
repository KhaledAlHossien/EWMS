using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    public class StudyRequestService : StudyRepositoryBase, IStudyRequestService
    {
        public StudyRequestService(DataContext context) : base(context) { }

        public async Task<StudyRequest?> GetByIdAsync(int id) =>
            await _context.StudyRequests
                .Include(r => r.Branch)
                .Include(r => r.Site)
                .Include(r => r.CreatedBy)
                .Include(r => r.Assignment)
                .FirstOrDefaultAsync(r => r.Id == id);

        public async Task<(List<StudyRequest> Items, int Total)> SearchAsync(
            StudyDocumentFilterDto filter, int? branchLimit, int page, int pageSize)
        {
            var query = _context.StudyRequests.AsQueryable();
            if (branchLimit != null) query = query.Where(r => r.BranchId == branchLimit);
            if (filter.BranchId is int branchId) query = query.Where(r => r.BranchId == branchId);
            if (filter.Status is int status) query = query.Where(r => (int)r.Status == status);
            if (filter.From is DateTime from) query = query.Where(r => r.RequestDate >= from.Date);
            if (filter.To is DateTime to) query = query.Where(r => r.RequestDate <= to.Date);
            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var text = filter.Search.Trim();
                query = query.Where(r => r.RequestNumber.Contains(text) || r.MainBeneficiary.Contains(text)
                                         || (r.SubBeneficiary != null && r.SubBeneficiary.Contains(text)));
            }

            var total = await query.CountAsync();
            var items = await query
                .OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Include(r => r.Branch).Include(r => r.Site).Include(r => r.CreatedBy).Include(r => r.Assignment)
                .AsNoTracking().ToListAsync();
            return (items, total);
        }

        public async Task<StudyRequest> AddAsync(StudyRequest request)
        {
            _context.StudyRequests.Add(request);
            await SaveChangesAsync();
            return request;
        }

        public async Task UpdateAsync(StudyRequest request, byte[]? expectedRowVersion = null)
        {
            ExpectVersion(request, expectedRowVersion);
            await SaveChangesAsync();
        }

        public async Task DeleteAsync(StudyRequest request)
        {
            _context.StudyRequests.Remove(request);
            await SaveChangesAsync();
        }

        public async Task<StudyChainDto> GetChainAsync(int requestId) =>
            await _context.StudyRequests.Where(r => r.Id == requestId)
                .Select(r => new StudyChainDto
                {
                    RequestId = r.Id,
                    RequestNumber = r.RequestNumber,
                    RequestStatus = (int)r.Status,
                    AssignmentId = r.Assignment == null ? null : r.Assignment.Id,
                    AssignmentNumber = r.Assignment == null ? null : r.Assignment.AssignmentNumber,
                    AssignmentStatus = r.Assignment == null ? null : (int)r.Assignment.Status,
                    ResponseId = r.Assignment == null || r.Assignment.Response == null ? null : r.Assignment.Response.Id,
                    ResponseStatus = r.Assignment == null || r.Assignment.Response == null ? null : (int)r.Assignment.Response.Status,
                    StartOrderId = r.Assignment == null || r.Assignment.Response == null || r.Assignment.Response.StartOrder == null
                        ? null : r.Assignment.Response.StartOrder.Id,
                    StartOrderNumber = r.Assignment == null || r.Assignment.Response == null || r.Assignment.Response.StartOrder == null
                        ? null : r.Assignment.Response.StartOrder.OrderNumber,
                    StartOrderStatus = r.Assignment == null || r.Assignment.Response == null || r.Assignment.Response.StartOrder == null
                        ? null : (int)r.Assignment.Response.StartOrder.Status,
                    ProjectId = r.Assignment == null || r.Assignment.Response == null || r.Assignment.Response.StartOrder == null
                                || r.Assignment.Response.StartOrder.Project == null
                        ? null : r.Assignment.Response.StartOrder.Project.Id,
                    ProjectCode = r.Assignment == null || r.Assignment.Response == null || r.Assignment.Response.StartOrder == null
                                  || r.Assignment.Response.StartOrder.Project == null
                        ? null : r.Assignment.Response.StartOrder.Project.Code,
                    ProjectStatus = r.Assignment == null || r.Assignment.Response == null || r.Assignment.Response.StartOrder == null
                                    || r.Assignment.Response.StartOrder.Project == null
                        ? null : (int)r.Assignment.Response.StartOrder.Project.Status,
                })
                .AsNoTracking()
                .FirstOrDefaultAsync() ?? throw new KeyNotFoundException("الطلب غير موجود");
    }

    public class StudyAssignmentService : StudyRepositoryBase, IStudyAssignmentService
    {
        public StudyAssignmentService(DataContext context) : base(context) { }

        private IQueryable<StudyAssignment> WithDetails() => _context.StudyAssignments
            .Include(a => a.Request).ThenInclude(r => r.Branch)
            .Include(a => a.FollowingDepartment)
            .Include(a => a.CreatedBy)
            .Include(a => a.Response);

        public async Task<StudyAssignment?> GetByIdAsync(int id) =>
            await WithDetails().FirstOrDefaultAsync(a => a.Id == id);

        public async Task<(List<StudyAssignment> Items, int Total)> SearchAsync(
            StudyDocumentFilterDto filter, int? branchLimit, int page, int pageSize)
        {
            var query = _context.StudyAssignments.AsQueryable();
            if (branchLimit != null) query = query.Where(a => a.Request.BranchId == branchLimit);
            if (filter.BranchId is int branchId) query = query.Where(a => a.Request.BranchId == branchId);
            if (filter.Status is int status) query = query.Where(a => (int)a.Status == status);
            if (filter.From is DateTime from) query = query.Where(a => a.AssignmentDate >= from.Date);
            if (filter.To is DateTime to) query = query.Where(a => a.AssignmentDate <= to.Date);
            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var text = filter.Search.Trim();
                query = query.Where(a => a.AssignmentNumber.Contains(text) || (a.ProjectCode != null && a.ProjectCode.Contains(text))
                                         || a.Request.RequestNumber.Contains(text) || a.Request.MainBeneficiary.Contains(text));
            }

            var total = await query.CountAsync();
            var items = await WithDetails().Where(a => query.Select(q => q.Id).Contains(a.Id))
                .OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .AsNoTracking().ToListAsync();
            return (items, total);
        }

        public async Task<bool> ExistsForRequestAsync(int requestId) =>
            await _context.StudyAssignments.AnyAsync(a => a.RequestId == requestId);

        public async Task<bool> ProjectCodeExistsAsync(string code, int? excludeAssignmentId = null) =>
            await _context.StudyAssignments.AnyAsync(a => a.ProjectCode == code && a.Id != excludeAssignmentId)
            || await _context.StudyProjects.AnyAsync(p => p.Code == code
                && (excludeAssignmentId == null || p.StartOrder.Response.AssignmentId != excludeAssignmentId));

        public async Task<StudyAssignment> AddAsync(StudyAssignment assignment)
        {
            _context.StudyAssignments.Add(assignment);
            await SaveChangesAsync();
            return assignment;
        }

        public async Task UpdateAsync(StudyAssignment assignment, byte[]? expectedRowVersion = null)
        {
            ExpectVersion(assignment, expectedRowVersion);
            await SaveChangesAsync();
        }

        public async Task DeleteAsync(StudyAssignment assignment)
        {
            _context.StudyAssignments.Remove(assignment);
            await SaveChangesAsync();
        }
    }

    public class StudyResponseService : StudyRepositoryBase, IStudyResponseService
    {
        public StudyResponseService(DataContext context) : base(context) { }

        private IQueryable<StudyAssignmentResponse> WithDetails() => _context.StudyAssignmentResponses
            .Include(r => r.Assignment).ThenInclude(a => a.Request).ThenInclude(q => q.Branch)
            .Include(r => r.Assignment).ThenInclude(a => a.FollowingDepartment)
            .Include(r => r.ProposedManager)
            .Include(r => r.CreatedBy)
            .Include(r => r.Buildings)
            .Include(r => r.CostItems)
            .Include(r => r.StaffEstimates).ThenInclude(s => s.Discipline)
            .Include(r => r.LogisticItems)
            .Include(r => r.StartOrder)
            .AsSplitQuery();

        public async Task<StudyAssignmentResponse?> GetByIdAsync(int id) =>
            await WithDetails().FirstOrDefaultAsync(r => r.Id == id);

        public async Task<(List<StudyAssignmentResponse> Items, int Total)> SearchAsync(
            StudyDocumentFilterDto filter, int? branchLimit, int page, int pageSize)
        {
            var query = _context.StudyAssignmentResponses.AsQueryable();
            if (branchLimit != null) query = query.Where(r => r.Assignment.Request.BranchId == branchLimit);
            if (filter.BranchId is int branchId) query = query.Where(r => r.Assignment.Request.BranchId == branchId);
            if (filter.Status is int status) query = query.Where(r => (int)r.Status == status);
            if (filter.From is DateTime from) query = query.Where(r => r.ResponseDate >= from.Date);
            if (filter.To is DateTime to) query = query.Where(r => r.ResponseDate <= to.Date);
            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var text = filter.Search.Trim();
                query = query.Where(r => r.ResponseNumber.Contains(text) || r.Assignment.AssignmentNumber.Contains(text)
                                         || (r.Assignment.ProjectCode != null && r.Assignment.ProjectCode.Contains(text))
                                         || r.Assignment.Request.MainBeneficiary.Contains(text));
            }

            var total = await query.CountAsync();
            var ids = await query.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id)
                .Skip((page - 1) * pageSize).Take(pageSize).Select(r => r.Id).ToListAsync();
            var items = await WithDetails().Where(r => ids.Contains(r.Id)).AsNoTracking().ToListAsync();
            return (items.OrderBy(r => ids.IndexOf(r.Id)).ToList(), total);
        }

        public async Task<bool> ExistsForAssignmentAsync(int assignmentId) =>
            await _context.StudyAssignmentResponses.AnyAsync(r => r.AssignmentId == assignmentId);

        public async Task<StudyAssignmentResponse> AddAsync(StudyAssignmentResponse response)
        {
            _context.StudyAssignmentResponses.Add(response);
            await SaveChangesAsync();
            return response;
        }

        public async Task UpdateAsync(StudyAssignmentResponse response, byte[]? expectedRowVersion = null)
        {
            ExpectVersion(response, expectedRowVersion);
            await SaveChangesAsync();
        }

        public async Task DeleteAsync(StudyAssignmentResponse response)
        {
            _context.StudyAssignmentResponses.Remove(response);
            await SaveChangesAsync();
        }
    }

    public class StudyStartOrderService : StudyRepositoryBase, IStudyStartOrderService
    {
        public StudyStartOrderService(DataContext context) : base(context) { }

        private IQueryable<StudyStartOrder> WithDetails() => _context.StudyStartOrders
            .Include(o => o.Response).ThenInclude(r => r.Assignment).ThenInclude(a => a.Request).ThenInclude(q => q.Branch)
            .Include(o => o.CompetentDepartment)
            .Include(o => o.ProjectManager)
            .Include(o => o.CreatedBy)
            .Include(o => o.Engineers).ThenInclude(e => e.Engineer)
            .Include(o => o.Engineers).ThenInclude(e => e.Discipline)
            .Include(o => o.Resources)
            .Include(o => o.Project)
            .AsSplitQuery();

        public async Task<StudyStartOrder?> GetByIdAsync(int id) =>
            await WithDetails().FirstOrDefaultAsync(o => o.Id == id);

        public async Task<(List<StudyStartOrder> Items, int Total)> SearchAsync(
            StudyDocumentFilterDto filter, int? branchLimit, int page, int pageSize)
        {
            var query = _context.StudyStartOrders.AsQueryable();
            if (branchLimit != null) query = query.Where(o => o.Response.Assignment.Request.BranchId == branchLimit);
            if (filter.BranchId is int branchId) query = query.Where(o => o.Response.Assignment.Request.BranchId == branchId);
            if (filter.Status is int status) query = query.Where(o => (int)o.Status == status);
            if (filter.From is DateTime from) query = query.Where(o => o.OrderDate >= from.Date);
            if (filter.To is DateTime to) query = query.Where(o => o.OrderDate <= to.Date);
            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var text = filter.Search.Trim();
                query = query.Where(o => o.OrderNumber.Contains(text)
                                         || (o.Response.Assignment.ProjectCode != null && o.Response.Assignment.ProjectCode.Contains(text))
                                         || o.Response.Assignment.Request.MainBeneficiary.Contains(text));
            }

            var total = await query.CountAsync();
            var ids = await query.OrderByDescending(o => o.CreatedAt).ThenByDescending(o => o.Id)
                .Skip((page - 1) * pageSize).Take(pageSize).Select(o => o.Id).ToListAsync();
            var items = await WithDetails().Where(o => ids.Contains(o.Id)).AsNoTracking().ToListAsync();
            return (items.OrderBy(o => ids.IndexOf(o.Id)).ToList(), total);
        }

        public async Task<bool> ExistsForResponseAsync(int responseId) =>
            await _context.StudyStartOrders.AnyAsync(o => o.ResponseId == responseId);

        public async Task<StudyStartOrder> AddAsync(StudyStartOrder order)
        {
            _context.StudyStartOrders.Add(order);
            await SaveChangesAsync();
            return order;
        }

        public async Task UpdateAsync(StudyStartOrder order, byte[]? expectedRowVersion = null)
        {
            ExpectVersion(order, expectedRowVersion);
            await SaveChangesAsync();
        }

        public async Task DeleteAsync(StudyStartOrder order)
        {
            _context.StudyStartOrders.Remove(order);
            await SaveChangesAsync();
        }
    }
}
