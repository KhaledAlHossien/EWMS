using Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Interfaces
{
    public interface IProjectTransferService
    {
        Task<ProjectTransfers?> GetByIdAsync(int id);
        Task AddAsync(ProjectTransfers transfer);
        Task<bool> UpdateAsync(ProjectTransfers transfer);
        Task<bool> SaveChangesAsync();

        Task<List<ProjectTransfers>> GetByProjectAsync(int projectId);
        Task<ProjectTransfers?> GetActiveTransferAsync(int projectId);
        Task<bool> DeactivateAllAsync(int projectId); // لإلغاء تفعيل كل التحويلات السابقة
    }
}
