using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace Infrastructure.Persistence.Repositories
{
    public class VacationService : IVacationService
    {
        private readonly DataContext _context;

        public VacationService(DataContext context)
        {
            _context = context;
        }

        // ==================== الاستعلامات الأساسية ====================

        public async Task<Vacation?> GetByIdAsync(int id)
        {
            return await _context.Vacation
                .FirstOrDefaultAsync(v => v.Id == id);
        }

        public async Task<Vacation?> GetWithDetailsAsync(int id)
        {
            return await _context.Vacation
                .Include(v => v.VacationType)
                .Include(v => v.User)
                .Include(v => v.Department)
                .Include(v => v.Branch)
                .Include(v => v.RejectedByUser)
                .Include(v => v.FirstApprovedByUser)
                .Include(v => v.FinalApprovedByUser)
                .Include(v => v.Segments.OrderBy(s => s.StartDate))
                .FirstOrDefaultAsync(v => v.Id == id);
        }

        public async Task<List<Vacation>> GetAllAsync()
        {
            return await _context.Vacation
                .Include(v => v.VacationType)
                .Include(v => v.User)
                .Include(v => v.Department)
                .Include(v => v.Branch)
                .OrderByDescending(v => v.CreatedAt)
                .ToListAsync();
        }

        // ==================== استعلامات المستخدم ====================

        public async Task<List<Vacation>> GetByUserIdAsync(int userId)
        {
            return await _context.Vacation
                .Include(v => v.VacationType)
                .Include(v => v.Department)
                .Include(v => v.Branch)
                .Where(v => v.UserId == userId)
                .OrderByDescending(v => v.StartVac)
                .ToListAsync();
        }

        // ==================== استعلامات النطاق (كل الحالات) ====================

        public async Task<List<Vacation>> GetByDepartmentIdAsync(int departmentId)
        {
            return await _context.Vacation
                .Include(v => v.VacationType)
                .Include(v => v.User)
                .Include(v => v.Department)
                .Include(v => v.Branch)
                .Where(v => v.DepartmentId == departmentId)
                .OrderByDescending(v => v.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<Vacation>> GetByBranchIdAsync(int branchId)
        {
            return await _context.Vacation
                .Include(v => v.VacationType)
                .Include(v => v.User)
                .Include(v => v.Department)
                .Include(v => v.Branch)
                .Where(v => v.BranchId == branchId)
                .OrderByDescending(v => v.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<Vacation>> GetByOfficeIdAsync(int officeId)
        {
            return await _context.Vacation
                .Include(v => v.VacationType)
                .Include(v => v.User)
                .Include(v => v.Department)
                .Include(v => v.Branch)
                .Where(v => v.User.OfficeId == officeId)
                .OrderByDescending(v => v.CreatedAt)
                .ToListAsync();
        }

        // ==================== استعلامات سير العمل ====================

        public async Task<List<Vacation>> GetByStatusAsync(
            VacationStatus status, int? departmentId, int? branchId)
        {
            var query = _context.Vacation
                .Include(v => v.VacationType)
                .Include(v => v.User)
                .Include(v => v.Department)
                .Include(v => v.Branch)
                .Where(v => v.Status == status);

            if (status == VacationStatus.PendingManager && departmentId.HasValue)
                query = query.Where(v => v.DepartmentId == departmentId.Value);

            if (status == VacationStatus.PendingBranchManager && branchId.HasValue)  // ⬅️
                query = query.Where(v => v.BranchId == branchId.Value);

            return await query.OrderBy(v => v.StartVac).ToListAsync();
        }

        public async Task<bool> ExistsForUserAsync(int userId)
        {
            return await _context.Vacation.AnyAsync(v =>
                v.UserId == userId || v.RejectedByUserId == userId || v.FirstApprovedByUserId == userId || v.FinalApprovedByUserId == userId);
        }

        // إجازات فرع بانتظار مرحلة معيّنة (الموافقة الأولى أو الاعتماد النهائي)
        public async Task<List<Vacation>> GetPendingInBranchAsync(VacationStatus status, int branchId)
        {
            return await _context.Vacation
                .Include(v => v.VacationType)
                .Include(v => v.User)
                .Include(v => v.Department)
                .Include(v => v.Branch)
                .Where(v => v.BranchId == branchId && v.Status == status)
                .OrderBy(v => v.StartVac)
                .ToListAsync();
        }

        // ✅ لـ SuperAdmin
        public async Task<List<Vacation>> GetAllPendingAsync()
        {
            return await _context.Vacation
                .Include(v => v.VacationType)
                .Include(v => v.User)
                .Include(v => v.Department)
                .Include(v => v.Branch)
                .Where(v => v.Status == VacationStatus.PendingManager
                         || v.Status == VacationStatus.PendingBranchManager)  // ⬅️
                .OrderBy(v => v.StartVac)
                .ToListAsync();
        }

        // ==================== عمليات CRUD ====================

        public async Task AddAsync(Vacation vacation)
        {
            await _context.Vacation.AddAsync(vacation);
            await SaveChangesAsync();
        }

        public async Task UpdateAsync(Vacation vacation)
        {
            _context.Vacation.Update(vacation);
            try
            {
                await SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                // قرار آخر سُجّل على نفس الطلب بين التحميل والحفظ (RowVersion)
                throw new InvalidOperationException("تم تعديل هذا الطلب من مستخدم آخر للتو، حدّث الصفحة وحاول مجدداً");
            }
        }

        public async Task<T> RunExclusiveForUserAsync<T>(int userId, Func<Task<T>> action)
        {
            // قفل تطبيقي على مستوى الموظف داخل معاملة: اعتمادان متزامنان لإجازتين لنفس الموظف
            // لا يحسبان الحد الشهري من نفس الرصيد. يُحرَّر القفل مع نهاية المعاملة.
            await using var transaction = await _context.Database.BeginTransactionAsync();
            await _context.Database.ExecuteSqlRawAsync(@"
DECLARE @r int;
EXEC @r = sp_getapplock @Resource = {0}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 15000;
IF @r < 0 THROW 50001, N'vacation lock timeout', 1;", $"vacation-user-{userId}");

            var result = await action();
            await transaction.CommitAsync();
            return result;
        }

        public async Task<bool> DeleteAsync(Vacation vacation)
        {
            _context.Vacation.Remove(vacation);
            return await SaveChangesAsync();
        }

        public async Task<bool> SaveChangesAsync()
        {
            return (await _context.SaveChangesAsync()) > 0;
        }

        // ==================== فحوصات التحقق ====================

        public async Task<bool> VacationTypeExistsAsync(int vacationTypeId)
        {
            return await _context.VacationType
                .AnyAsync(vt => vt.Id == vacationTypeId);
        }

        public async Task<bool> HasOverlappingVacationAsync(
            int userId, DateTime start, DateTime end, int? excludeId = null)
        {
            return await _context.Vacation
                .AnyAsync(v =>
                    v.UserId == userId &&
                    v.Id != excludeId &&
                    v.StartVac <= end &&
                    v.EndVac >= start &&
                    (v.Status == VacationStatus.Approved ||
                     v.Status == VacationStatus.PendingManager ||
                     v.Status == VacationStatus.PendingBranchManager));  // ⬅️
        }

        // ==================== حساب الأيام المدفوعة ====================
        // من أجزاء الإجازات المعتمدة فقط (قرار المستخدم 2026-10-04) — الجزء لا يتجاوز شهراً واحداً،
        // فيُنسب كله لشهر تاريخ بدايته

        public async Task<int> GetPaidVacationDaysInMonthAsync(int userId, int year, int month)
        {
            var monthStart = new DateTime(year, month, 1);
            var nextMonth = monthStart.AddMonths(1);

            return await _context.VacationSegments
                .Where(s => s.IsPaid
                         && s.Vacation.UserId == userId
                         && s.Vacation.Status == VacationStatus.Approved
                         && s.StartDate >= monthStart && s.StartDate < nextMonth)
                .SumAsync(s => s.Days);
        }

        public async Task<Dictionary<(int Year, int Month), int>> GetApprovedPaidDaysByMonthAsync(
            int userId, DateTime start, DateTime end, int? excludeVacationId = null)
        {
            var from = new DateTime(start.Year, start.Month, 1);
            var to = new DateTime(end.Year, end.Month, 1).AddMonths(1);

            var rows = await _context.VacationSegments
                .Where(s => s.IsPaid
                         && s.Vacation.UserId == userId
                         && s.Vacation.Status == VacationStatus.Approved
                         && s.VacationId != excludeVacationId
                         && s.StartDate >= from && s.StartDate < to)
                .GroupBy(s => new { s.StartDate.Year, s.StartDate.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Days = g.Sum(s => s.Days) })
                .ToListAsync();

            return rows.ToDictionary(r => (r.Year, r.Month), r => r.Days);
        }
    }
}