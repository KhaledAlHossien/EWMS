using Application.Interfaces;
using Domain.Entities.Attendance;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    public class AttendanceCorrectionService : IAttendanceCorrectionService
    {
        private readonly DataContext _context;

        public AttendanceCorrectionService(DataContext context) => _context = context;

        public Task<List<AttendanceCorrection>> ListAsync(int userId, DateTime from, DateTime to) =>
            _context.AttendanceCorrections.AsNoTracking()
                .Include(c => c.CreatedByUser).Include(c => c.CancelledByUser)
                .Where(c => c.UserId == userId && c.FromDate <= to.Date && c.ToDate >= from.Date)
                .OrderByDescending(c => c.CreatedAt).ToListAsync();

        public Task<AttendanceCorrection?> GetAsync(long id) =>
            _context.AttendanceCorrections.Include(c => c.CreatedByUser).FirstOrDefaultAsync(c => c.Id == id);

        public async Task<(int? UserId, DateTime Time)?> PunchAsync(long punchId)
        {
            var punch = await _context.AttendancePunches.AsNoTracking().Where(p => p.Id == punchId)
                .Select(p => new { p.UserId, p.PunchTime }).FirstOrDefaultAsync();
            return punch == null ? null : (punch.UserId, punch.PunchTime);
        }

        public Task<bool> IgnoredAsync(long punchId) =>
            _context.AttendanceCorrections.AnyAsync(c => c.PunchId == punchId && c.Kind == AttendanceCorrectionKind.IgnorePunch && c.CancelledAt == null);

        public async Task AddAsync(AttendanceCorrection correction)
        {
            _context.AttendanceCorrections.Add(correction);
            await _context.SaveChangesAsync();
        }

        public Task SaveAsync() => _context.SaveChangesAsync();
    }
}
