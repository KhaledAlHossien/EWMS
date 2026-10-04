using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    public class PublicHolidayService : IPublicHolidayService
    {
        private readonly DataContext _context;

        public PublicHolidayService(DataContext context)
        {
            _context = context;
        }

        public async Task<List<PublicHoliday>> GetAllAsync(int? year = null) =>
            await _context.PublicHolidays
                .Where(h => year == null || h.Date.Year == year)
                .OrderBy(h => h.Date)
                .ToListAsync();

        public async Task<List<PublicHoliday>> GetBetweenAsync(DateTime start, DateTime end)
        {
            var from = start.Date;
            var to = end.Date;
            return await _context.PublicHolidays
                .Where(h => h.Date >= from && h.Date <= to)
                .OrderBy(h => h.Date)
                .ToListAsync();
        }

        public async Task<PublicHoliday?> GetByIdAsync(int id) =>
            await _context.PublicHolidays.FirstOrDefaultAsync(h => h.Id == id);

        public async Task AddRangeAsync(IEnumerable<PublicHoliday> holidays)
        {
            await _context.PublicHolidays.AddRangeAsync(holidays);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(PublicHoliday holiday)
        {
            _context.PublicHolidays.Update(holiday);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(PublicHoliday holiday)
        {
            _context.PublicHolidays.Remove(holiday);
            await _context.SaveChangesAsync();
        }

        public async Task<List<DateTime>> ExistingDatesAsync(IEnumerable<DateTime> dates, int? excludeId = null)
        {
            var list = dates.Select(d => d.Date).ToList();
            return await _context.PublicHolidays
                .Where(h => list.Contains(h.Date) && h.Id != excludeId)
                .Select(h => h.Date)
                .ToListAsync();
        }

        public async Task<IReadOnlySet<DateTime>> GetDatesAsync(DateTime start, DateTime end)
        {
            var from = start.Date;
            var to = end.Date;
            var dates = await _context.PublicHolidays
                .Where(h => h.Date >= from && h.Date <= to)
                .Select(h => h.Date)
                .ToListAsync();
            return dates.ToHashSet();
        }
    }
}
