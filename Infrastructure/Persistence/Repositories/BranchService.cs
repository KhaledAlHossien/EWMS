using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    public class BranchService : IBranchService
    {
        private readonly DataContext _context;

        public BranchService(DataContext context)
        {
            _context = context;
        }

        public async Task<Branch?> GetByIdAsync(int id)
        {
            return await _context.Branches.FindAsync(id);
        }

        public async Task<List<Branch>> GetAllAsync()
        {
            return await _context.Branches.ToListAsync();
        }

        public async Task<Branch> AddAsync(Branch branch)
        {
            var result = await _context.Branches.AddAsync(branch);
            await SaveChangesAsync();
            return result.Entity;
        }

        public async Task<bool> UpdateAsync(Branch branch)
        {
            _context.Branches.Update(branch);
            return await SaveChangesAsync();
        }

        public async Task<bool> DeleteAsync(Branch branch)
        {
            _context.Branches.Remove(branch);
            return await SaveChangesAsync();
        }

        public async Task<bool> SaveChangesAsync()
        {
            return (await _context.SaveChangesAsync()) > 0;
        }

        public async Task<bool> ExistsAsync(int id)
        {
            return await _context.Branches.AnyAsync(b => b.Id == id);
        }

        public async Task<bool> ExistsByNameAsync(string name, int? excludeId = null)
        {
            return await _context.Branches
                .AnyAsync(b => b.Name == name && b.Id != excludeId);
        }

        public async Task<bool> HasDepartmentsAsync(int branchId)
        {
            return await _context.Departments.AnyAsync(d => d.BranchId == branchId);
        }

        public async Task<bool> HasUsersAsync(int branchId)
        {
            return await _context.Users.AnyAsync(u => u.BranchId == branchId);
        }
    }
}