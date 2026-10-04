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
                v.UserId == userId || v.RejectedByUserId == userId || v.FirstApprovedByUserId == userId);
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
            await SaveChangesAsync();
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

        // ==================== حساب الأيام ====================

        public async Task<int> GetVacationDaysInMonthAsync(
            int userId, int year, int month)
        {
            var monthStart = new DateTime(year, month, 1);
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);

            var vacations = await _context.Vacation
                .Where(v =>
                    v.UserId == userId &&
                    v.StartVac <= monthEnd &&
                    v.EndVac >= monthStart &&
                    (v.Status == VacationStatus.Approved ||
                     v.Status == VacationStatus.PendingManager ||
                     v.Status == VacationStatus.PendingBranchManager))  // ⬅️
                .Select(v => new { v.StartVac, v.EndVac })
                .ToListAsync();

            return CalculateOverlapDays(vacations, monthStart, monthEnd);
        }

        public async Task<int> GetPaidVacationDaysInMonthAsync(
            int userId, int year, int month)
        {
            var monthStart = new DateTime(year, month, 1);
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);

            var vacations = await _context.Vacation
                .Where(v =>
                    v.UserId == userId &&
                    v.IsPaid == true &&
                    v.StartVac <= monthEnd &&
                    v.EndVac >= monthStart &&
                    (v.Status == VacationStatus.Approved ||
                     v.Status == VacationStatus.PendingManager ||
                     v.Status == VacationStatus.PendingBranchManager))  // ⬅️
                .Select(v => new { v.StartVac, v.EndVac })
                .ToListAsync();

            return CalculateOverlapDays(vacations, monthStart, monthEnd);
        }

        public async Task<int> GetTotalPaidDaysInYearAsync(int userId, int year)
        {
            var yearStart = new DateTime(year, 1, 1);
            var yearEnd = new DateTime(year, 12, 31);

            var vacations = await _context.Vacation
                .Where(v =>
                    v.UserId == userId &&
                    v.IsPaid == true &&
                    v.StartVac <= yearEnd &&
                    v.EndVac >= yearStart &&
                    (v.Status == VacationStatus.Approved ||
                     v.Status == VacationStatus.PendingManager ||
                     v.Status == VacationStatus.PendingBranchManager))  // ⬅️
                .Select(v => new { v.StartVac, v.EndVac })
                .ToListAsync();

            return CalculateOverlapDays(vacations, yearStart, yearEnd);
        }

        public async Task<int> GetTotalUnpaidDaysInYearAsync(int userId, int year)
        {
            var yearStart = new DateTime(year, 1, 1);
            var yearEnd = new DateTime(year, 12, 31);

            var vacations = await _context.Vacation
                .Where(v =>
                    v.UserId == userId &&
                    v.IsPaid == false &&
                    v.StartVac <= yearEnd &&
                    v.EndVac >= yearStart &&
                    (v.Status == VacationStatus.Approved ||
                     v.Status == VacationStatus.PendingManager ||
                     v.Status == VacationStatus.PendingBranchManager))  // ⬅️
                .Select(v => new { v.StartVac, v.EndVac })
                .ToListAsync();

            return CalculateOverlapDays(vacations, yearStart, yearEnd);
        }

        // ==================== دالة مساعدة ====================
        private static int CalculateOverlapDays<T>(
            List<T> vacations, DateTime rangeStart, DateTime rangeEnd)
            where T : class
        {
            int totalDays = 0;

            foreach (var v in vacations)
            {
                var startProp = v.GetType().GetProperty("StartVac")?.GetValue(v);
                var endProp = v.GetType().GetProperty("EndVac")?.GetValue(v);

                if (startProp == null || endProp == null) continue;

                var start = (DateTime)startProp;
                var end = (DateTime)endProp;

                var overlapStart = start > rangeStart ? start : rangeStart;
                var overlapEnd = end < rangeEnd ? end : rangeEnd;

                if (overlapStart > overlapEnd) continue;

                totalDays += (overlapEnd - overlapStart).Days + 1;
            }

            return totalDays;
        }
    }
}