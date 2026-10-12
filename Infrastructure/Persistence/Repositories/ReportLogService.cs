using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.Reports;
using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    public class ReportLogService : IReportLogService
    {
        private readonly DataContext _context;

        public ReportLogService(DataContext context) => _context = context;

        public async Task AddAsync(ReportExportLog log)
        {
            _context.ReportExportLogs.Add(log);
            await _context.SaveChangesAsync();
        }

        public async Task<(List<ReportLogDto> Items, int Total)> SearchAsync(ReportLogFilterDto filter, int? branchId, int page, int pageSize)
        {
            var query = _context.ReportExportLogs.AsNoTracking();
            if (branchId != null) query = query.Where(l => l.BranchId == branchId);
            if (!string.IsNullOrWhiteSpace(filter.ReportKey)) query = query.Where(l => l.ReportKey == filter.ReportKey);
            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var search = filter.Search.Trim();
                query = query.Where(l => l.User.FullName.Contains(search));
            }
            // الفترة بأيام التوقيت المحلي، والسجل محفوظ UTC
            if (filter.From is DateTime from)
            {
                var start = DateTime.SpecifyKind(from.Date, DateTimeKind.Local).ToUniversalTime();
                query = query.Where(l => l.CreatedAt >= start);
            }
            if (filter.To is DateTime to)
            {
                var end = DateTime.SpecifyKind(to.Date.AddDays(1), DateTimeKind.Local).ToUniversalTime();
                query = query.Where(l => l.CreatedAt < end);
            }

            var total = await query.CountAsync();
            var rows = await query
                .OrderByDescending(l => l.CreatedAt).ThenByDescending(l => l.Id)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(l => new
                {
                    Log = l,
                    l.User.FullName,
                    BranchName = _context.Branches.Where(b => b.Id == l.BranchId).Select(b => b.Name).FirstOrDefault()
                })
                .ToListAsync();

            return (rows.Select(x => new ReportLogDto
            {
                Id = x.Log.Id,
                UserId = x.Log.UserId,
                UserName = x.FullName,
                BranchName = x.BranchName ?? string.Empty,
                ReportKey = x.Log.ReportKey,
                ReportTitle = x.Log.ReportTitle,
                Format = ReportRules.FormatAr(x.Log.Format),
                Filters = x.Log.Filters,
                RowCount = x.Log.RowCount,
                CreatedAt = x.Log.CreatedAt
            }).ToList(), total);
        }

        public Task<bool> ExistsForUserAsync(int userId) => _context.ReportExportLogs.AnyAsync(l => l.UserId == userId);
    }
}
