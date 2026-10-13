using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    /// <summary>العطل الرسمية: سجل لكل عطلة بفترة من–إلى؛ أيامها تُفرد لحساب الإجازات</summary>
    public class PublicHolidayService : IPublicHolidayService
    {
        private readonly DataContext _context;

        public PublicHolidayService(DataContext context)
        {
            _context = context;
        }

        public async Task<List<PublicHoliday>> GetAllAsync(int? year = null)
        {
            var query = _context.PublicHolidays.AsQueryable();
            if (year is int y)
            {
                var first = new DateTime(y, 1, 1);
                var last = new DateTime(y, 12, 31);
                query = query.Where(h => h.StartDate <= last && h.EndDate >= first);
            }
            return await query.OrderBy(h => h.StartDate).ToListAsync();
        }

        public async Task<List<PublicHoliday>> GetBetweenAsync(DateTime start, DateTime end)
        {
            var from = start.Date;
            var to = end.Date;
            return await _context.PublicHolidays
                .Where(h => h.StartDate <= to && h.EndDate >= from)
                .OrderBy(h => h.StartDate)
                .ToListAsync();
        }

        public async Task<PublicHoliday?> GetByIdAsync(int id) =>
            await _context.PublicHolidays.FirstOrDefaultAsync(h => h.Id == id);

        public async Task AddAsync(PublicHoliday holiday)
        {
            await _context.PublicHolidays.AddAsync(holiday);
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

        public async Task<PublicHoliday?> FindOverlapAsync(DateTime start, DateTime end, int? excludeId = null)
        {
            var from = start.Date;
            var to = end.Date;
            return await _context.PublicHolidays
                .Where(h => h.Id != excludeId && h.StartDate <= to && h.EndDate >= from)
                .OrderBy(h => h.StartDate)
                .FirstOrDefaultAsync();
        }

        public async Task<IReadOnlySet<DateTime>> GetDatesAsync(DateTime start, DateTime end)
        {
            var from = start.Date;
            var to = end.Date;
            var dates = new HashSet<DateTime>();
            foreach (var h in await GetBetweenAsync(from, to))
                for (var d = h.StartDate.Date < from ? from : h.StartDate.Date; d <= h.EndDate.Date && d <= to; d = d.AddDays(1))
                    dates.Add(d);
            return dates;
        }
    }
}
