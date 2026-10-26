using Domain.Entities.Attendance;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Data
{
    /// <summary>
    /// جداول المرحلة الثانية من الحضور: الجداول الزمنية والورديات والجدولة، نتائج الأيام، التصحيحات، وطابور إعادة الحساب.
    /// </summary>
    public partial class DataContext
    {
        public DbSet<AttendanceTimetable> AttendanceTimetables { get; set; }
        public DbSet<AttendanceShift> AttendanceShifts { get; set; }
        public DbSet<AttendanceShiftDay> AttendanceShiftDays { get; set; }
        public DbSet<AttendanceSchedule> AttendanceSchedules { get; set; }
        public DbSet<AttendanceTemporarySchedule> AttendanceTemporarySchedules { get; set; }
        public DbSet<AttendanceDay> AttendanceDays { get; set; }
        public DbSet<AttendanceDayPart> AttendanceDayParts { get; set; }
        public DbSet<AttendanceCorrection> AttendanceCorrections { get; set; }
        public DbSet<AttendanceRecalcRequest> AttendanceRecalcRequests { get; set; }

        private static void ConfigureAttendanceShifts(ModelBuilder builder)
        {
            builder.Entity<AttendanceTimetable>(entity =>
            {
                entity.Property(t => t.Name).HasMaxLength(100).IsRequired();
                entity.Property(t => t.Color).HasMaxLength(7).IsRequired();
                entity.Property(t => t.Kind).HasConversion<int>();
                entity.Property(t => t.LateCounting).HasConversion<int>();
                entity.Property(t => t.RowVersion).IsRowVersion();
                entity.HasIndex(t => t.Name).IsUnique();
            });

            builder.Entity<AttendanceShift>(entity =>
            {
                entity.Property(s => s.Name).HasMaxLength(100).IsRequired();
                entity.Property(s => s.Description).HasMaxLength(500);
                entity.Property(s => s.CycleUnit).HasConversion<int>();
                entity.Property(s => s.StartDate).HasColumnType("date");
                entity.Property(s => s.RowVersion).IsRowVersion();
                entity.HasIndex(s => s.Name).IsUnique();
                entity.HasMany(s => s.Days).WithOne(d => d.Shift).HasForeignKey(d => d.ShiftId).OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<AttendanceShiftDay>(entity =>
            {
                // الجدول المستعمل في وردية لا يُحذف (حراسة في الـ handler)
                entity.HasOne(d => d.Timetable).WithMany().HasForeignKey(d => d.TimetableId).OnDelete(DeleteBehavior.Restrict);
                entity.HasIndex(d => new { d.ShiftId, d.DayIndex, d.TimetableId }).IsUnique();
            });

            builder.Entity<AttendanceSchedule>(entity =>
            {
                entity.Property(s => s.TargetType).HasConversion<int>();
                entity.Property(s => s.FromDate).HasColumnType("date");
                entity.Property(s => s.ToDate).HasColumnType("date");
                entity.Property(s => s.Note).HasMaxLength(300);
                entity.Property(s => s.RowVersion).IsRowVersion();
                entity.HasOne(s => s.Shift).WithMany().HasForeignKey(s => s.ShiftId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(s => s.CreatedByUser).WithMany().HasForeignKey(s => s.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(s => s.UpdatedByUser).WithMany().HasForeignKey(s => s.UpdatedByUserId).OnDelete(DeleteBehavior.Restrict);
                entity.HasIndex(s => new { s.TargetType, s.TargetId, s.FromDate });
            });

            builder.Entity<AttendanceTemporarySchedule>(entity =>
            {
                entity.Property(s => s.TargetType).HasConversion<int>();
                entity.Property(s => s.FromDate).HasColumnType("date");
                entity.Property(s => s.ToDate).HasColumnType("date");
                entity.Property(s => s.Note).HasMaxLength(300);
                entity.HasOne(s => s.Timetable).WithMany().HasForeignKey(s => s.TimetableId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(s => s.CreatedByUser).WithMany().HasForeignKey(s => s.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
                entity.HasIndex(s => new { s.TargetType, s.TargetId, s.FromDate });
            });

            builder.Entity<AttendanceDay>(entity =>
            {
                entity.Property(d => d.Date).HasColumnType("date");
                entity.Property(d => d.Type).HasConversion<int>();
                entity.Property(d => d.Status).HasConversion<int>();
                entity.Property(d => d.Hash).HasMaxLength(64).IsRequired();
                // نتائج الأيام تُحذف مع صاحبها (حذفه نفسه مرفوض ما دامت له بصمات)
                entity.HasOne(d => d.User).WithMany().HasForeignKey(d => d.UserId).OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(d => d.Shift).WithMany().HasForeignKey(d => d.ShiftId).OnDelete(DeleteBehavior.SetNull);
                entity.HasIndex(d => new { d.UserId, d.Date }).IsUnique();
                entity.HasIndex(d => new { d.Date, d.Status });
                entity.HasMany(d => d.Parts).WithOne(p => p.Day).HasForeignKey(p => p.DayId).OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<AttendanceDayPart>(entity =>
            {
                entity.Property(p => p.TimetableName).HasMaxLength(100);
                entity.Property(p => p.Kind).HasConversion<int>();
                entity.Property(p => p.Status).HasConversion<int>();
            });

            builder.Entity<AttendanceCorrection>(entity =>
            {
                entity.Property(c => c.Kind).HasConversion<int>();
                entity.Property(c => c.ExcuseKind).HasConversion<int>();
                entity.Property(c => c.FromDate).HasColumnType("date");
                entity.Property(c => c.ToDate).HasColumnType("date");
                entity.Property(c => c.Reason).HasMaxLength(500).IsRequired();
                entity.Property(c => c.CancelReason).HasMaxLength(500);
                entity.HasOne(c => c.User).WithMany().HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(c => c.Punch).WithMany().HasForeignKey(c => c.PunchId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(c => c.CreatedByUser).WithMany().HasForeignKey(c => c.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(c => c.CancelledByUser).WithMany().HasForeignKey(c => c.CancelledByUserId).OnDelete(DeleteBehavior.Restrict);
                entity.HasIndex(c => new { c.UserId, c.FromDate });
            });

            builder.Entity<AttendanceRecalcRequest>(entity =>
            {
                entity.Property(r => r.FromDate).HasColumnType("date");
                entity.Property(r => r.ToDate).HasColumnType("date");
                entity.Property(r => r.Reason).HasMaxLength(100);
            });
        }
    }
}
