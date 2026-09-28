using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    public class RegionService : IRegionService
    {
        private readonly DataContext _context;

        public RegionService(DataContext context)
        {
            _context = context;
        }

        public async Task<Region?> GetByIdAsync(int id)
        {
            return await _context.Regions.FirstOrDefaultAsync(r => r.Id == id);
        }

        public async Task<List<Region>> GetAllAsync()
        {
            return await _context.Regions.ToListAsync();
        }

        public async Task<Region> AddAsync(Region region)
        {
            var result = await _context.Regions.AddAsync(region);
            await _context.SaveChangesAsync();
            return result.Entity;
        }

        public async Task<bool> UpdateAsync(Region region)
        {
            _context.Regions.Update(region);
            return (await _context.SaveChangesAsync()) > 0;
        }

        public async Task<bool> DeleteAsync(Region region)
        {
            _context.Regions.Remove(region);
            return (await _context.SaveChangesAsync()) > 0;
        }

        public async Task<bool> ExistsAsync(int id)
        {
            return await _context.Regions.AnyAsync(r => r.Id == id);
        }

        public async Task<bool> ExistsByNameAsync(string name, int? excludeId = null)
        {
            return await _context.Regions.AnyAsync(r => r.Name == name && r.Id != excludeId);
        }

        public async Task<bool> HasSitesAsync(int regionId)
        {
            return await _context.Sites.AnyAsync(s => s.RegionId == regionId);
        }
    }
}
