using Domain.Entities;
using Domain.Entities.Attendance;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Data
{
    /// <summary>
    /// جداول وحدة الحضور والانصراف (المرحلة الأولى: المناطق، بيانات الأشخاص، الأجهزة، البصمات الخام، القوالب والصور).
    /// كل المفاتيح الأجنبية Restrict إلا ما يُذكر: الحذف محروس برسائل في الـ handlers.
    /// </summary>
    public partial class DataContext
    {
        public DbSet<AttendanceArea> AttendanceAreas { get; set; }
        public DbSet<AttendanceProfile> AttendanceProfiles { get; set; }
        public DbSet<AttendanceProfileArea> AttendanceProfileAreas { get; set; }
        public DbSet<AttendanceDevice> AttendanceDevices { get; set; }
        public DbSet<AttendanceDeviceCommand> AttendanceDeviceCommands { get; set; }
        public DbSet<AttendanceDeviceUser> AttendanceDeviceUsers { get; set; }
        public DbSet<AttendanceDeviceLog> AttendanceDeviceLogs { get; set; }
        public DbSet<AttendancePunch> AttendancePunches { get; set; }
        public DbSet<AttendancePunchPhoto> AttendancePunchPhotos { get; set; }
        public DbSet<AttendancePunchPhotoContent> AttendancePunchPhotoContents { get; set; }
        public DbSet<AttendanceBiometric> AttendanceBiometrics { get; set; }
        public DbSet<AttendanceBioPhoto> AttendanceBioPhotos { get; set; }

        private static void ConfigureAttendance(ModelBuilder builder)
        {
            builder.Entity<AttendanceArea>(entity =>
            {
                entity.Property(a => a.Name).HasMaxLength(100).IsRequired();
                entity.Property(a => a.Description).HasMaxLength(500);
                entity.Property(a => a.RowVersion).IsRowVersion();
                entity.HasIndex(a => a.Name).IsUnique();
            });

            // الفرع يتبع منطقة واحدة على الأكثر؛ حذف منطقة فيها فروع مرفوض (حراسة في الـ handler)
            builder.Entity<Branch>()
                .HasOne(b => b.AttendanceArea).WithMany()
                .HasForeignKey(b => b.AttendanceAreaId).OnDelete(DeleteBehavior.Restrict);

            builder.Entity<AttendanceProfile>(entity =>
            {
                entity.HasKey(p => p.UserId);
                // بيانات الحضور تُحذف مع صاحبها (حذفه نفسه مرفوض ما دامت له بصمات)
                entity.HasOne(p => p.User).WithOne().HasForeignKey<AttendanceProfile>(p => p.UserId).OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(p => p.Area).WithMany().HasForeignKey(p => p.AreaId).OnDelete(DeleteBehavior.Restrict);
                entity.Property(p => p.AttendanceNumber).HasMaxLength(20);
                entity.Property(p => p.CardNumber).HasMaxLength(20);
                entity.Property(p => p.ActiveFrom).HasColumnType("date");
                entity.Property(p => p.ActiveTo).HasColumnType("date");
                entity.Property(p => p.RowVersion).IsRowVersion();
                entity.HasIndex(p => p.AttendanceNumber).IsUnique().HasFilter("[AttendanceNumber] IS NOT NULL");
                entity.HasIndex(p => p.CardNumber).IsUnique().HasFilter("[CardNumber] IS NOT NULL");
                entity.HasMany(p => p.ExtraAreas).WithOne(a => a.Profile).HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<AttendanceProfileArea>(entity =>
            {
                entity.HasKey(a => new { a.UserId, a.AreaId });
                entity.HasOne(a => a.Area).WithMany().HasForeignKey(a => a.AreaId).OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<AttendanceDevice>(entity =>
            {
                entity.Property(d => d.SerialNumber).HasMaxLength(50).IsRequired();
                entity.Property(d => d.Name).HasMaxLength(100).IsRequired();
                entity.Property(d => d.State).HasConversion<int>();
                entity.Property(d => d.Model).HasMaxLength(100);
                entity.Property(d => d.Firmware).HasMaxLength(100);
                entity.Property(d => d.PushVersion).HasMaxLength(20);
                entity.Property(d => d.FingerAlgorithm).HasMaxLength(20);
                entity.Property(d => d.FaceAlgorithm).HasMaxLength(20);
                entity.Property(d => d.IpAddress).HasMaxLength(45);
                entity.Property(d => d.AttLogStamp).HasMaxLength(30);
                entity.Property(d => d.OperLogStamp).HasMaxLength(30);
                entity.Property(d => d.PhotoStamp).HasMaxLength(30);
                entity.Property(d => d.Notes).HasMaxLength(500);
                entity.Property(d => d.RowVersion).IsRowVersion();
                entity.HasIndex(d => d.SerialNumber).IsUnique();
                entity.HasIndex(d => new { d.State, d.AreaId });
                entity.HasOne(d => d.Area).WithMany().HasForeignKey(d => d.AreaId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(d => d.ApprovedByUser).WithMany().HasForeignKey(d => d.ApprovedByUserId).OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<AttendanceDeviceCommand>(entity =>
            {
                entity.Property(c => c.Kind).HasConversion<int>();
                entity.Property(c => c.State).HasConversion<int>();
                entity.Property(c => c.Text).IsRequired();
                entity.Property(c => c.Summary).HasMaxLength(300);
                entity.HasOne(c => c.Device).WithMany().HasForeignKey(c => c.DeviceId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(c => c.CreatedByUser).WithMany().HasForeignKey(c => c.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
                // الاستطلاع يقرأ أوامر الجهاز المنتظرة بالترتيب
                entity.HasIndex(c => new { c.DeviceId, c.State, c.Id });
                entity.HasIndex(c => c.CreatedAt);
            });

            builder.Entity<AttendanceDeviceUser>(entity =>
            {
                entity.Property(u => u.Pin).HasMaxLength(20).IsRequired();
                entity.Property(u => u.Name).HasMaxLength(100);
                entity.Property(u => u.SyncedHash).HasMaxLength(64);
                entity.Property(u => u.BiometricHash).HasMaxLength(64);
                entity.HasIndex(u => new { u.DeviceId, u.Pin }).IsUnique();
                entity.HasIndex(u => u.UserId);
                entity.HasOne(u => u.Device).WithMany().HasForeignKey(u => u.DeviceId).OnDelete(DeleteBehavior.Cascade);
                // حذف الشخص يترك سطره «غير معروف» فتحذفه المزامنة من الجهاز
                entity.HasOne(u => u.User).WithMany().HasForeignKey(u => u.UserId).OnDelete(DeleteBehavior.SetNull);
            });

            builder.Entity<AttendanceDeviceLog>(entity =>
            {
                entity.Property(l => l.Event).HasConversion<int>();
                entity.Property(l => l.Details).HasMaxLength(1000);
                entity.HasOne(l => l.Device).WithMany().HasForeignKey(l => l.DeviceId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(l => l.User).WithMany().HasForeignKey(l => l.UserId).OnDelete(DeleteBehavior.Restrict);
                entity.HasIndex(l => new { l.DeviceId, l.CreatedAt });
            });

            builder.Entity<AttendancePunch>(entity =>
            {
                entity.Property(p => p.DeviceSerial).HasMaxLength(50).IsRequired();
                entity.Property(p => p.Pin).HasMaxLength(20).IsRequired();
                entity.Property(p => p.WorkCode).HasMaxLength(20);
                entity.Property(p => p.Source).HasConversion<int>();
                entity.HasOne(p => p.Device).WithMany().HasForeignKey(p => p.DeviceId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(p => p.User).WithMany().HasForeignKey(p => p.UserId).OnDelete(DeleteBehavior.Restrict);
                // المكرر لا يُحفظ مرتين (إعادة الإرسال والسحب آمنان)
                entity.HasIndex(p => new { p.DeviceSerial, p.Pin, p.PunchTime }).IsUnique();
                entity.HasIndex(p => new { p.UserId, p.PunchTime });
                entity.HasIndex(p => p.PunchTime);
                entity.HasIndex(p => new { p.DeviceId, p.PunchTime });
                // الأرقام غير المعروفة (بصمات بلا صاحب)
                entity.HasIndex(p => p.Pin).HasFilter("[UserId] IS NULL").HasDatabaseName("IX_AttendancePunches_UnknownPin");
            });

            builder.Entity<AttendancePunchPhoto>(entity =>
            {
                entity.Property(p => p.DeviceSerial).HasMaxLength(50).IsRequired();
                entity.Property(p => p.Pin).HasMaxLength(20).IsRequired();
                entity.Property(p => p.StorageKey).HasMaxLength(80);
                entity.HasOne(p => p.Device).WithMany().HasForeignKey(p => p.DeviceId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(p => p.Content).WithOne(c => c.Photo).HasForeignKey<AttendancePunchPhotoContent>(c => c.PhotoId).OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(p => new { p.DeviceSerial, p.Pin, p.PunchTime }).IsUnique();
                entity.HasIndex(p => p.StorageKey).IsUnique().HasFilter("[StorageKey] IS NOT NULL");
                entity.HasIndex(p => p.PunchTime);
            });

            builder.Entity<AttendancePunchPhotoContent>(entity => entity.HasKey(c => c.PhotoId));

            builder.Entity<AttendanceBiometric>(entity =>
            {
                entity.Property(b => b.Type).HasConversion<int>();
                entity.Property(b => b.MajorVersion).HasMaxLength(20);
                entity.Property(b => b.MinorVersion).HasMaxLength(20);
                entity.Property(b => b.Hash).HasMaxLength(64).IsRequired();
                entity.HasOne(b => b.User).WithMany().HasForeignKey(b => b.UserId).OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(b => b.SourceDevice).WithMany().HasForeignKey(b => b.SourceDeviceId).OnDelete(DeleteBehavior.Restrict);
                entity.HasIndex(b => new { b.UserId, b.Type, b.No, b.Index }).IsUnique();
            });

            builder.Entity<AttendanceBioPhoto>(entity =>
            {
                entity.HasKey(p => p.UserId);
                entity.Property(p => p.Hash).HasMaxLength(64).IsRequired();
                entity.HasOne(p => p.User).WithOne().HasForeignKey<AttendanceBioPhoto>(p => p.UserId).OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(p => p.SourceDevice).WithMany().HasForeignKey(p => p.SourceDeviceId).OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}
