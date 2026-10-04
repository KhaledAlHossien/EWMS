using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    public class SiteService : ISiteService
    {
        private readonly DataContext _context;

        public SiteService(DataContext context)
        {
            _context = context;
        }

        public async Task<Site?> GetByIdAsync(int id)
        {
            return await _context.Sites
                .FirstOrDefaultAsync(s => s.Id == id);
        }

        public async Task<List<Site>> GetAllAsync()
        {
            return await _context.Sites
                .ToListAsync();
        }


        public async Task<Site> AddAsync(Site site)
        {
            var result = await _context.Sites.AddAsync(site);
            await _context.SaveChangesAsync();
            return result.Entity;
        }

        public async Task<bool> UpdateAsync(Site site)
        {
            _context.Sites.Update(site);
            return (await _context.SaveChangesAsync()) > 0;
        }

        public async Task<bool> DeleteAsync(Site site)
        {
            _context.Sites.Remove(site);
            return (await _context.SaveChangesAsync()) > 0;
        }

        public async Task<bool> ExistsAsync(int id)
        {
            return await _context.Sites.AnyAsync(s => s.Id == id);
        }

        public async Task<bool> ExistsByNameAsync(string name, int? excludeId = null)
        {
            return await _context.Sites.AnyAsync(s => s.Name == name && s.Id != excludeId);
        }

        public async Task<bool> HasDeviceLinksAsync(int siteId)
        {
            return await _context.DeviceSites.AnyAsync(ds => ds.SiteId == siteId);
        }
    }
}
