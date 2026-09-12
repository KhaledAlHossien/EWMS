using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    public class ProjectTransferService : IProjectTransferService
    {
        private readonly DataContext _context;

        public ProjectTransferService(DataContext context)
        {
            _context = context;
        }

        public async Task<ProjectTransfers?> GetByIdAsync(int id)
        {
            return await _context.ProjectTransfers
                .Include(pt => pt.Project)
                .Include(pt => pt.FromDepartment)
                .Include(pt => pt.ToDepartment)
                .Include(pt => pt.TransferredByUser)
                .FirstOrDefaultAsync(pt => pt.Id == id);
        }

        public async Task AddAsync(ProjectTransfers transfer)
        {
            transfer.TransferredAt = DateTime.UtcNow;
            await _context.ProjectTransfers.AddAsync(transfer);
            await SaveChangesAsync();
        }

        public async Task<bool> UpdateAsync(ProjectTransfers transfer)
        {
            _context.ProjectTransfers.Update(transfer);
            return await SaveChangesAsync();
        }

        public async Task<bool> SaveChangesAsync()
        {
            return (await _context.SaveChangesAsync()) > 0;
        }

        public async Task<List<ProjectTransfers>> GetByProjectAsync(int projectId)
        {
            return await _context.ProjectTransfers
                .Include(pt => pt.FromDepartment)
                .Include(pt => pt.ToDepartment)
                .Include(pt => pt.TransferredByUser)
                .Where(pt => pt.ProjectId == projectId)
                .OrderByDescending(pt => pt.TransferredAt)
                .ToListAsync();
        }

        public async Task<ProjectTransfers?> GetActiveTransferAsync(int projectId)
        {
            return await _context.ProjectTransfers
                .FirstOrDefaultAsync(pt => pt.ProjectId == projectId && pt.IsActive);
        }

        public async Task<bool> DeactivateAllAsync(int projectId)
        {
            var activeTransfers = await _context.ProjectTransfers
                .Where(pt => pt.ProjectId == projectId && pt.IsActive)
                .ToListAsync();

            foreach (var t in activeTransfers)
                t.IsActive = false;

            return await SaveChangesAsync();
        }
    }
}