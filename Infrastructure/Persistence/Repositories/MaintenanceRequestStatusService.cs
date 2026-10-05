using Application.Interfaces;
using Domain.Entities.Maintenance;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    public class MaintenanceRequestStatusService : IMaintenanceRequestStatusService
    {
        private readonly DataContext _context;

        public MaintenanceRequestStatusService(DataContext context)
        {
            _context = context;
        }

        public async Task<MaintenanceRequestStatus?> GetByIdAsync(int id) =>
            await _context.MaintenanceRequestStatuses.FirstOrDefaultAsync(x => x.Id == id);

        // بترتيب الإضافة (مسار سير الطلب: جديد ← قيد الصيانة ← ...)
        public async Task<List<MaintenanceRequestStatus>> GetAllAsync() =>
            // بترتيب المراحل (جديد ← قيد العمل ← جاهز ← مُسلَّم ← غير قابل للصيانة) — أعمدة اللوحة وأول حالة «جديد» للطلب الجديد
            await _context.MaintenanceRequestStatuses.OrderBy(x => x.Stage).ThenBy(x => x.Id).ToListAsync();

        public async Task<MaintenanceRequestStatus> AddAsync(MaintenanceRequestStatus status)
        {
            var result = await _context.MaintenanceRequestStatuses.AddAsync(status);
            await _context.SaveChangesAsync();
            return result.Entity;
        }

        public async Task<bool> UpdateAsync(MaintenanceRequestStatus status)
        {
            _context.MaintenanceRequestStatuses.Update(status);
            return (await _context.SaveChangesAsync()) > 0;
        }

        public async Task<bool> DeleteAsync(MaintenanceRequestStatus status)
        {
            _context.MaintenanceRequestStatuses.Remove(status);
            return (await _context.SaveChangesAsync()) > 0;
        }

        public async Task<bool> ExistsAsync(int id) =>
            await _context.MaintenanceRequestStatuses.AnyAsync(x => x.Id == id);

        public async Task<bool> ExistsByNameAsync(string name, int? excludeId = null) =>
            await _context.MaintenanceRequestStatuses.AnyAsync(x => x.Name == name && x.Id != excludeId);

        public async Task<bool> IsUsedAsync(int id) =>
            await _context.MaintenanceRequests.AnyAsync(r => r.MaintenanceRequestStatusId == id);
    }
}
