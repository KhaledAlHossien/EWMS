using Domain.Entities.Attendance;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    /// <summary>تعليم الأجهزة «تحتاج مزامنة» حين يتغير ما يخص من يجب أن يكون عليها — تعيد الخدمة الخلفية حسابها</summary>
    internal static class AttendanceSyncMarks
    {
        /// <summary>مناطق هؤلاء الأشخاص: منطقة العامل المحددة أو منطقة الفرع، والمناطق الإضافية</summary>
        public static async Task<List<int>> AreasOfAsync(DataContext context, IReadOnlyCollection<int> userIds, CancellationToken ct = default)
        {
            if (userIds.Count == 0) return [];
            var home = await context.Users.AsNoTracking()
                .Where(u => userIds.Contains(u.Id))
                .Select(u => context.AttendanceProfiles.Where(p => p.UserId == u.Id).Select(p => p.AreaId).FirstOrDefault()
                    ?? u.Branch!.AttendanceAreaId)
                .ToListAsync(ct);
            var extra = await context.AttendanceProfileAreas.AsNoTracking()
                .Where(a => userIds.Contains(a.UserId)).Select(a => (int?)a.AreaId).ToListAsync(ct);
            return home.Concat(extra).Where(a => a != null).Select(a => a!.Value).Distinct().ToList();
        }

        public static async Task MarkAreasAsync(DataContext context, IReadOnlyCollection<int> areaIds, CancellationToken ct = default)
        {
            if (areaIds.Count == 0) return;
            await context.AttendanceDevices
                .Where(d => d.State == AttendanceDeviceState.Approved && d.AreaId != null && areaIds.Contains(d.AreaId.Value))
                .ExecuteUpdateAsync(s => s.SetProperty(d => d.NeedsSync, true), ct);
        }

        public static async Task MarkUsersAsync(DataContext context, IReadOnlyCollection<int> userIds, CancellationToken ct = default) =>
            await MarkAreasAsync(context, await AreasOfAsync(context, userIds, ct), ct);
    }
}
