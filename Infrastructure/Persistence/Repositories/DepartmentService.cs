using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    public class DepartmentService : IDepartmentService
    {
        private readonly DataContext _context;

        public DepartmentService(DataContext context)
        {
            _context = context;
        }

        public async Task<Department?> GetByIdAsync(int id)
        {
            return await _context.Departments
                .Include(d => d.Branch)
                .FirstOrDefaultAsync(d => d.Id == id);
        }

        public async Task<List<Department>> GetAllAsync()
        {
            return await _context.Departments
                .Include(d => d.Branch)
                .ToListAsync();
        }

        public async Task<Department> AddAsync(Department department)
        {
            var result = await _context.Departments.AddAsync(department);
            await SaveChangesAsync();
            return result.Entity;
        }

        public async Task<bool> UpdateAsync(Department department)
        {
            _context.Departments.Update(department);
            return await SaveChangesAsync();
        }

        public async Task<bool> DeleteAsync(Department department)
        {
            _context.Departments.Remove(department);
            return await SaveChangesAsync();
        }

        public async Task<bool> SaveChangesAsync()
        {
            return (await _context.SaveChangesAsync()) > 0;
        }

        public async Task<bool> ExistsAsync(int id)
        {
            return await _context.Departments.AnyAsync(d => d.Id == id);
        }

        public async Task<bool> ExistsByNameAsync(string name, int? excludeId = null)
        {
            return await _context.Departments
                .AnyAsync(d => d.Name == name && d.Id != excludeId);
        }

        public async Task<bool> HasUsersAsync(int departmentId)
        {
            return await _context.Users.AnyAsync(u => u.DepartmentId == departmentId);
        }

        public async Task<bool> HasProjectsAsync(int departmentId)
        {
            return await _context.Projects.AnyAsync(p => p.CurrentDepartmentId == departmentId);
        }

        public async Task<List<Department>> GetByBranchAsync(int branchId)
        {
            return await _context.Departments
                .Where(d => d.BranchId == branchId)
                .ToListAsync();
        }
    }
}