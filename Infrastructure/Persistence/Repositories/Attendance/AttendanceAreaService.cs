using Application.Interfaces;
using Domain.Entities;
using Domain.Entities.Attendance;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    public class AttendanceAreaService : IAttendanceAreaService
    {
        private readonly DataContext _context;

        public AttendanceAreaService(DataContext context) => _context = context;

        public async Task<List<AreaSummary>> GetAllAsync(DateTime onlineSinceUtc)
        {
            var areas = await _context.AttendanceAreas.AsNoTracking().OrderBy(a => a.Name).ToListAsync();
            return await SummariesAsync(areas, onlineSinceUtc);
        }

        public async Task<AreaSummary?> GetSummaryAsync(int id, DateTime onlineSinceUtc)
        {
            var area = await _context.AttendanceAreas.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id);
            return area == null ? null : (await SummariesAsync([area], onlineSinceUtc))[0];
        }

        private async Task<List<AreaSummary>> SummariesAsync(List<AttendanceArea> areas, DateTime onlineSinceUtc)
        {
            var ids = areas.Select(a => a.Id).ToList();
            var branches = await _context.Branches.AsNoTracking()
                .Where(b => b.AttendanceAreaId != null && ids.Contains(b.AttendanceAreaId.Value)).ToListAsync();
            var devices = await _context.AttendanceDevices.AsNoTracking()
                .Where(d => d.State == AttendanceDeviceState.Approved && d.AreaId != null && ids.Contains(d.AreaId.Value))
                .GroupBy(d => d.AreaId!.Value)
                .Select(g => new { AreaId = g.Key, Total = g.Count(), Online = g.Count(d => d.LastSeenAt >= onlineSinceUtc) })
                .ToListAsync();
            // الأشخاص على الأجهزة: لهم رقم بصمة وفعّالون — منطقة العامل المحددة، وإلا منطقة فرعه
            var people = await _context.AttendanceProfiles.AsNoTracking()
                .Where(p => p.AttendanceNumber != null && p.User.IsActive)
                .Select(p => new { AreaId = p.AreaId ?? p.User.Branch!.AttendanceAreaId })
                .Where(p => p.AreaId != null && ids.Contains(p.AreaId.Value))
                .GroupBy(p => p.AreaId!.Value)
                .Select(g => new { AreaId = g.Key, Count = g.Count() })
                .ToListAsync();

            return areas.Select(a =>
            {
                var d = devices.FirstOrDefault(x => x.AreaId == a.Id);
                return new AreaSummary(a,
                    branches.Where(b => b.AttendanceAreaId == a.Id).ToList(),
                    d?.Total ?? 0, d?.Online ?? 0,
                    people.FirstOrDefault(x => x.AreaId == a.Id)?.Count ?? 0);
            }).ToList();
        }

        public Task<AttendanceArea?> GetByIdAsync(int id) =>
            _context.AttendanceAreas.FirstOrDefaultAsync(a => a.Id == id);

        public Task<bool> ExistsByNameAsync(string name, int? excludeId = null) =>
            _context.AttendanceAreas.AnyAsync(a => a.Name == name && a.Id != excludeId);

        public Task<List<Branch>> GetBranchesAsync() =>
            _context.Branches.AsNoTracking().Include(b => b.AttendanceArea).ToListAsync();

        public async Task SaveAsync(AttendanceArea area, byte[]? rowVersion, IReadOnlyCollection<int> branchIds)
        {
            if (area.Id == 0) _context.AttendanceAreas.Add(area);
            else if (rowVersion is { Length: > 0 })
                _context.Entry(area).Property(a => a.RowVersion).OriginalValue = rowVersion;

            await using var transaction = await _context.Database.BeginTransactionAsync();
            await DeviceInventorySave.SaveAsync(_context, "توجد منطقة بنفس الاسم");

            // الفروع: الخارج منها يصبح بلا منطقة، والمختار يُنقل إليها — والمناطق التي تأثرت تُعاد مزامنة أجهزتها
            var changed = await _context.Branches
                .Where(b => (b.AttendanceAreaId == area.Id && !branchIds.Contains(b.Id))
                         || (branchIds.Contains(b.Id) && b.AttendanceAreaId != area.Id))
                .ToListAsync();
            var touchedAreas = changed.Where(b => b.AttendanceAreaId != null).Select(b => b.AttendanceAreaId!.Value).Append(area.Id).ToHashSet();
            foreach (var branch in changed)
                branch.AttendanceAreaId = branchIds.Contains(branch.Id) ? area.Id : null;

            if (changed.Count > 0)
            {
                await _context.SaveChangesAsync();
                await _context.AttendanceDevices
                    .Where(d => d.AreaId != null && touchedAreas.Contains(d.AreaId.Value))
                    .ExecuteUpdateAsync(s => s.SetProperty(d => d.NeedsSync, true));
            }
            await transaction.CommitAsync();
        }

        public async Task<string?> DeleteBlockerAsync(int id)
        {
            if (await _context.Branches.AnyAsync(b => b.AttendanceAreaId == id))
                return "لا يمكن حذف منطقة فيها فروع — أخرج فروعها منها أولاً";
            if (await _context.AttendanceDevices.AnyAsync(d => d.AreaId == id))
                return "لا يمكن حذف منطقة لها أجهزة بصمة (ولو مُزالة) — انقل أجهزتها إلى منطقة أخرى أولاً";
            if (await _context.AttendanceProfiles.AnyAsync(p => p.AreaId == id))
                return "لا يمكن حذف منطقة لها عمال مياومة — انقلهم إلى منطقة أخرى أولاً";
            if (await _context.AttendanceProfileAreas.AnyAsync(a => a.AreaId == id))
                return "لا يمكن حذف منطقة مسجّلة منطقةً إضافية لبعض الأشخاص — أزلها من بياناتهم أولاً";
            return null;
        }

        public async Task DeleteAsync(AttendanceArea area)
        {
            _context.AttendanceAreas.Remove(area);
            await DeviceInventorySave.SaveAsync(_context, "توجد منطقة بنفس الاسم");
        }
    }
}
