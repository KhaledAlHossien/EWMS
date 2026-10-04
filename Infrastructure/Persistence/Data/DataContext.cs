using Domain.Entities;
using Domain.Entities.Maintenance;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace Infrastructure.Persistence.Data
{
    public class DataContext : DbContext
    {
        public DataContext(DbContextOptions<DataContext> options) : base(options) { }

        // ==================== DbSets ====================
        public DbSet<Branch> Branches { get; set; }
        public DbSet<Department> Departments { get; set; }
        public DbSet<Office> Offices { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<Permission> Permissions { get; set; }
        public DbSet<RolePermission> RolePermissions { get; set; }
        public DbSet<Vacation> Vacation { get; set; }
        public DbSet<VacationType> VacationType { get; set; }
        public DbSet<VacationSegment> VacationSegments { get; set; }
        public DbSet<PublicHoliday> PublicHolidays { get; set; }
        public DbSet<UserToken> UserTokens { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<Site> Sites { get; set; }
        public DbSet<Device> Devices { get; set; }
        public DbSet<DeviceSite> DeviceSites { get; set; }
        public DbSet<WorkTask> WorkTasks { get; set; }
        public DbSet<UserWorkTask> UserWorkTasks { get; set; }
        public DbSet<AssignedTask> AssignedTasks { get; set; }
        public DbSet<AssignedTaskActivity> AssignedTaskActivities { get; set; }
        public DbSet<DeviceType> DeviceTypes { get; set; }
        public DbSet<DeviceCompany> DeviceCompanies { get; set; }
        public DbSet<DamageType> DamageTypes { get; set; }
        public DbSet<MaintenanceRequestStatus> MaintenanceRequestStatuses { get; set; }
        public DbSet<DeviceMaintenance> DeviceMaintenances { get; set; }
        public DbSet<MaintenanceRequest> MaintenanceRequests { get; set; }
        public DbSet<MaintenanceTask> MaintenanceTasks { get; set; }
        public DbSet<MaintenanceRequestActivity> MaintenanceRequestActivities { get; set; }
        public DbSet<UserSignature> UserSignatures { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // ==================== الفهارس الفريدة (Unique Indexes) ====================
            builder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            // الرقم الذاتي: فريد لمن سُجّل له، والحسابات بلا رقم (NULL) لا تتعارض فيما بينها
            builder.Entity<User>()
                .Property(u => u.PersonalIdNumber)
                .HasMaxLength(20);
            builder.Entity<User>()
                .Property(u => u.PhoneNumber)
                .HasMaxLength(20);
            builder.Entity<User>()
                .HasIndex(u => u.PersonalIdNumber)
                .IsUnique()
                .HasFilter("[PersonalIdNumber] IS NOT NULL");

            builder.Entity<Role>()
                .HasIndex(r => r.Name)
                .IsUnique();

            builder.Entity<VacationType>()
               .HasIndex(u => u.Name)
               .IsUnique();

            builder.Entity<Branch>()
                .HasIndex(b => b.Name)
                .IsUnique();

            builder.Entity<Department>()
                .HasIndex(d => d.Name)
                .IsUnique();

            builder.Entity<Office>()
                .HasIndex(o => o.Name)
                .IsUnique();

            builder.Entity<RolePermission>()
                .HasIndex(rp => new { rp.RoleId, rp.PermissionId })
                .IsUnique();

            builder.Entity<Site>()
                .Property(s => s.GovernorateCode)
                .HasMaxLength(10);

            builder.Entity<Site>()
                .HasIndex(s => s.GovernorateCode);

            builder.Entity<Site>()
                .HasIndex(s => s.Name)
                .IsUnique();

            // لا فهرس فريد على (DeviceId, SiteId): نفس الجهاز قد يُركَّب أكثر من مرة في نفس الموقع
            builder.Entity<DeviceSite>()
                .Property(ds => ds.InstallLocation)
                .HasMaxLength(300);

            builder.Entity<DeviceSite>()
                .Property(ds => ds.SN)
                .HasMaxLength(100);

            // ==================== تكوين العلاقات ====================

            // ---------- Branch ----------
            // 1. Department -> Branch
            builder.Entity<Department>()
                .HasOne(d => d.Branch)
                .WithMany()
                .HasForeignKey(d => d.BranchId)
                .OnDelete(DeleteBehavior.Restrict);

            // 2. User -> Branch
            builder.Entity<User>()
                .HasOne(u => u.Branch)
                .WithMany()
                .HasForeignKey(u => u.BranchId)
                .OnDelete(DeleteBehavior.Restrict);

            // ---------- Office ----------
            // 2.1 Office -> Department
            builder.Entity<Office>()
                .HasOne(o => o.Department)
                .WithMany()
                .HasForeignKey(o => o.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);

            // ---------- User ----------

            // 3. User -> Role
            builder.Entity<User>()
                .HasOne(u => u.Role)
                .WithMany()
                .HasForeignKey(u => u.RoleId)
                .OnDelete(DeleteBehavior.Restrict);

            // 4. User -> Department
            builder.Entity<User>()
                .HasOne(u => u.Department)
                .WithMany()
                .HasForeignKey(u => u.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);

            // 4.1 User -> Office
            builder.Entity<User>()
                .HasOne(u => u.Office)
                .WithMany()
                .HasForeignKey(u => u.OfficeId)
                .OnDelete(DeleteBehavior.Restrict);

            // ---------- RolePermission ----------
            // 5. RolePermission -> Role
            builder.Entity<RolePermission>()
                .HasOne(rp => rp.Role)
                .WithMany()
                .HasForeignKey(rp => rp.RoleId)
                .OnDelete(DeleteBehavior.Cascade);

            // 6. RolePermission -> Permission
            builder.Entity<RolePermission>()
                .HasOne(rp => rp.Permission)
                .WithMany()
                .HasForeignKey(rp => rp.PermissionId)
                .OnDelete(DeleteBehavior.Cascade);

            // 7. User -> UserToken
            builder.Entity<UserToken>()
                .HasOne(ut => ut.User)
                .WithMany()
                .HasForeignKey(ut => ut.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // ---------- Site / Device ----------

            // 7.2 DeviceSite -> Device
            builder.Entity<DeviceSite>()
                .HasOne(ds => ds.Device)
                .WithMany()
                .HasForeignKey(ds => ds.DeviceId)
                .OnDelete(DeleteBehavior.Restrict);

            // 7.3 DeviceSite -> Site
            builder.Entity<DeviceSite>()
                .HasOne(ds => ds.Site)
                .WithMany()
                .HasForeignKey(ds => ds.SiteId)
                .OnDelete(DeleteBehavior.Restrict);

            // ==================== تكوين Notification ====================
            builder.Entity<Notification>(entity =>
            {
                entity.HasKey(n => n.Id);

                entity.Property(n => n.Title).HasMaxLength(200).IsRequired();
                entity.Property(n => n.Message).HasMaxLength(1000).IsRequired();
                entity.Property(n => n.RelatedEntityType).HasMaxLength(100);
                entity.Property(n => n.Type).HasConversion<int>();
                entity.Property(n => n.IsRead).HasDefaultValue(false);

                // 8. Notification -> User (تُحذف إشعارات المستخدم إن حُذف حسابه)
                entity.HasOne(n => n.User)
                    .WithMany()
                    .HasForeignKey(n => n.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(n => new { n.UserId, n.IsRead });
                entity.HasIndex(n => new { n.UserId, n.CreatedAt });
            });

            // ==================== تكوين WorkTask (مهام العمل) ====================
            builder.Entity<WorkTask>(entity =>
            {
                entity.Property(t => t.Name).HasMaxLength(100).IsRequired();
                entity.Property(t => t.Description).HasMaxLength(500);
                entity.Property(t => t.Icon).HasMaxLength(16);

                // اسم المهمة فريد داخل الفرع (فروع مختلفة قد يكون لها مهام بنفس الاسم)
                entity.HasIndex(t => new { t.BranchId, t.Name }).IsUnique();

                // لا يُحذف فرع له مهام (DeleteBranchCommandHandler يتحقق برسالة واضحة)
                entity.HasOne(t => t.Branch)
                    .WithMany()
                    .HasForeignKey(t => t.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<UserWorkTask>(entity =>
            {
                entity.HasKey(a => new { a.UserId, a.WorkTaskId });

                // الإسناد جدول ربط: يُحذف مع المهمة أو مع الموظف
                entity.HasOne(a => a.WorkTask)
                    .WithMany(t => t.Assignments)
                    .HasForeignKey(a => a.WorkTaskId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(a => a.User)
                    .WithMany()
                    .HasForeignKey(a => a.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ==================== تكوين AssignedTask (لوحة المهام) ====================
            // كل العلاقات Restrict: سجل المهام يُحفظ، وحذف قسم/مكتب/موظف له مهام يُمنع برسالة واضحة
            builder.Entity<AssignedTask>(entity =>
            {
                entity.Property(t => t.Title).HasMaxLength(200).IsRequired();
                entity.Property(t => t.Description).HasMaxLength(4000);
                entity.Property(t => t.Priority).HasConversion<int>();
                entity.Property(t => t.Status).HasConversion<int>();
                entity.Property(t => t.TargetType).HasConversion<int>();

                entity.HasOne(t => t.Branch).WithMany().HasForeignKey(t => t.BranchId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(t => t.Department).WithMany().HasForeignKey(t => t.DepartmentId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(t => t.Office).WithMany().HasForeignKey(t => t.OfficeId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(t => t.AssigneeUser).WithMany().HasForeignKey(t => t.AssigneeUserId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(t => t.CreatedByUser).WithMany().HasForeignKey(t => t.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(t => t.ParentTask).WithMany(t => t.SubTasks).HasForeignKey(t => t.ParentTaskId).OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(t => new { t.DepartmentId, t.Status });
                entity.HasIndex(t => new { t.OfficeId, t.Status });
                entity.HasIndex(t => new { t.AssigneeUserId, t.Status });
                entity.HasIndex(t => new { t.CreatedByUserId, t.Status });
            });

            builder.Entity<AssignedTaskActivity>(entity =>
            {
                entity.Property(a => a.Text).HasMaxLength(2000);
                entity.Property(a => a.Type).HasConversion<int>();
                entity.Property(a => a.FromStatus).HasConversion<int?>();
                entity.Property(a => a.ToStatus).HasConversion<int?>();

                // السجل جزء من المهمة: يُحذف معها
                entity.HasOne(a => a.AssignedTask).WithMany(t => t.Activities)
                    .HasForeignKey(a => a.AssignedTaskId).OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(a => a.User).WithMany()
                    .HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(a => new { a.AssignedTaskId, a.CreatedAt });
            });

            // ==================== الصيانة: الجداول المساعدة ====================
            builder.Entity<DeviceType>(entity =>
            {
                entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
                entity.HasIndex(x => x.Name).IsUnique();
            });

            builder.Entity<DeviceCompany>(entity =>
            {
                entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
                entity.Property(x => x.Description).HasMaxLength(500);
                entity.HasIndex(x => x.Name).IsUnique();
            });

            builder.Entity<DamageType>(entity =>
            {
                entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
                entity.Property(x => x.Description).HasMaxLength(500);
                entity.HasIndex(x => x.Name).IsUnique();
            });

            builder.Entity<MaintenanceRequestStatus>(entity =>
            {
                entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
                entity.Property(x => x.Color).HasMaxLength(7).IsRequired();
                entity.HasIndex(x => x.Name).IsUnique();
            });

            // ==================== الصيانة: أجهزة الصيانة ====================
            // الجهاز قطعة فعلية: رقمه التسلسلي فريد (فهرس فريد يخدم البحث بـ"يبدأ بـ" أيضاً — Index Seek)
            builder.Entity<DeviceMaintenance>(entity =>
            {
                entity.Property(d => d.Name).HasMaxLength(200);
                entity.Property(d => d.SerialNumber).HasMaxLength(100).IsRequired();
                entity.Property(d => d.Model).HasMaxLength(100);
                entity.Property(d => d.Description).HasMaxLength(1000);

                entity.HasOne(d => d.DeviceType).WithMany().HasForeignKey(d => d.DeviceTypeId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(d => d.DeviceCompany).WithMany().HasForeignKey(d => d.DeviceCompanyId).OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(d => d.SerialNumber).IsUnique();
                entity.HasIndex(d => d.Model);
            });

            // ==================== الصيانة: طلبات الصيانة ====================
            // كل العلاقات Restrict: لا يُحذف جهاز/عطل/حالة/موظف/قسم مستخدم في طلب (الحذف يُمنع برسالة واضحة)
            builder.Entity<MaintenanceRequest>(entity =>
            {
                entity.Property(r => r.ClientName).HasMaxLength(200).IsRequired();
                entity.Property(r => r.ClientPhone).HasMaxLength(30);
                entity.Property(r => r.Accessories).HasMaxLength(500);
                entity.Property(r => r.Description).HasMaxLength(2000);

                entity.HasOne(r => r.User).WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(r => r.Department).WithMany().HasForeignKey(r => r.DepartmentId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(r => r.DeviceMaintenance).WithMany().HasForeignKey(r => r.DeviceMaintenanceId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(r => r.DamageType).WithMany().HasForeignKey(r => r.DamageTypeId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(r => r.MaintenanceRequestStatus).WithMany().HasForeignKey(r => r.MaintenanceRequestStatusId).OnDelete(DeleteBehavior.Restrict);

                // ----- فهارس البحث -----
                // الرقم التسلسلي والموديل والشركة والنوع صارت في DeviceMaintenance (يُبحث عنها بالربط معه)
                entity.HasIndex(r => r.ClientName);

                // فلترة بالمعرّف + ترتيب بالأحدث (تغني عن فهرس الـ FK المنفرد)
                entity.HasIndex(r => new { r.UserId, r.CreatedAt });
                entity.HasIndex(r => new { r.DeviceMaintenanceId, r.CreatedAt }); // سجل إصلاحات الجهاز
                entity.HasIndex(r => new { r.DepartmentId, r.CreatedAt });
                entity.HasIndex(r => new { r.MaintenanceRequestStatusId, r.CreatedAt });
            });

            // ==================== الصيانة: مهام العمل ====================
            builder.Entity<MaintenanceTask>(entity =>
            {
                entity.Property(t => t.TaskLocation).HasMaxLength(200).IsRequired();
                entity.Property(t => t.RequestingParty).HasMaxLength(200).IsRequired();
                entity.Property(t => t.RequiredWork).HasMaxLength(2000).IsRequired();
                entity.Property(t => t.CompletedWorks).HasMaxLength(2000);

                entity.HasOne(t => t.User).WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(t => t.Department).WithMany().HasForeignKey(t => t.DepartmentId).OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(t => new { t.UserId, t.CreatedAt });
                entity.HasIndex(t => new { t.DepartmentId, t.CreatedAt });
            });

            // ==================== تكوين VacationType ====================
            builder.Entity<VacationType>(entity =>
            {
                entity.HasKey(vt => vt.Id);

                entity.Property(vt => vt.Name)
                    .HasMaxLength(100)
                    .IsRequired();

                entity.Property(vt => vt.Description)
                    .HasMaxLength(500);
            });

            // ==================== تكوين Vacation ====================
            builder.Entity<Vacation>(entity =>
            {
                entity.HasKey(v => v.Id);

                // ----- الخصائص -----
                entity.Property(v => v.VacReason)
                    .HasMaxLength(500);

                entity.Property(v => v.ManagerAccept)
                    .HasDefaultValue(false);

                

                // ⚠ لا HasDefaultValue على خصائص bool: EF يعتبر false "غير محددة" فيحذفها من INSERT
                // فتكتب قاعدة البيانات الافتراضي بدلها (كانت الإجازة غير المدفوعة تُحفظ مدفوعة —
                // أصلحته migration Fix_Vacation_Bool_Defaults). القيمة الافتراضية من مُهيّئ الـ Entity.

                entity.Property(v => v.Status)
                    .HasConversion<int>()   // خزّنه كـ int
                    .HasDefaultValue(VacationStatus.PendingManager);

                entity.Property(v => v.RejectionReason)
                    .HasMaxLength(500);


                // ----- العلاقات -----

                // 8. Vacation -> VacationType
                entity.HasOne(v => v.VacationType)
                    .WithMany()
                    .HasForeignKey(v => v.VacationTypeId)
                    .OnDelete(DeleteBehavior.Restrict);

                // 9. Vacation -> User
                entity.HasOne(v => v.User)
                    .WithMany()
                    .HasForeignKey(v => v.UserId)
                    .OnDelete(DeleteBehavior.Restrict);

                // 10. Vacation -> Department
                entity.HasOne(v => v.Department)
                    .WithMany()
                    .HasForeignKey(v => v.DepartmentId)
                    .OnDelete(DeleteBehavior.Restrict);

                // 11. Vacation -> Branch
                entity.HasOne(v => v.Branch)
                    .WithMany()
                    .HasForeignKey(v => v.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);


                

                // علاقة RejectedByUser
                entity.HasOne(v => v.RejectedByUser)
                    .WithMany()
                    .HasForeignKey(v => v.RejectedByUserId)
                    .OnDelete(DeleteBehavior.Restrict);

                // من وافق في المرحلة الأولى (لا يعتمد نفس الشخص المرحلتين، ويُبلَّغ بالقرار النهائي)
                entity.HasOne(v => v.FirstApprovedByUser)
                    .WithMany()
                    .HasForeignKey(v => v.FirstApprovedByUserId)
                    .OnDelete(DeleteBehavior.Restrict);

                // من اعتمد نهائياً
                entity.HasOne(v => v.FinalApprovedByUser)
                    .WithMany()
                    .HasForeignKey(v => v.FinalApprovedByUserId)
                    .OnDelete(DeleteBehavior.Restrict);

                // قراران متزامنان على نفس الطلب → الثاني يفشل (DbUpdateConcurrencyException)
                entity.Property(v => v.RowVersion).IsRowVersion();

                // فهرس على Status لتسريع الاستعلامات
                entity.HasIndex(v => v.Status);

                // ----- الفهارس (Indexes) -----
                entity.HasIndex(v => v.UserId);
                entity.HasIndex(v => v.DepartmentId);
                entity.HasIndex(v => v.BranchId);
                entity.HasIndex(v => v.StartVac);
                entity.HasIndex(v => new { v.UserId, v.StartVac, v.EndVac });
                entity.HasIndex(v => new { v.UserId, v.IsPaid });
            });

            // ==================== أجزاء الإجازة المعتمدة (الدفع الشهري) ====================
            builder.Entity<VacationSegment>(entity =>
            {
                entity.Property(s => s.StartDate).HasColumnType("date");
                entity.Property(s => s.EndDate).HasColumnType("date");

                entity.HasOne(s => s.Vacation)
                    .WithMany(v => v.Segments)
                    .HasForeignKey(s => s.VacationId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(s => new { s.VacationId, s.StartDate });
            });

            // ==================== العطل الرسمية ====================
            builder.Entity<PublicHoliday>(entity =>
            {
                entity.Property(h => h.Date).HasColumnType("date");
                entity.Property(h => h.Name).HasMaxLength(100).IsRequired();
                entity.HasIndex(h => h.Date).IsUnique();
            });

            // ==================== الصيانة: سجل الطلب ====================
            builder.Entity<MaintenanceRequestActivity>(entity =>
            {
                entity.Property(a => a.Text).HasMaxLength(500);
                entity.Property(a => a.Type).HasConversion<int>();

                // السجل جزء من الطلب: يُحذف معه
                entity.HasOne(a => a.MaintenanceRequest).WithMany()
                    .HasForeignKey(a => a.MaintenanceRequestId).OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(a => a.User).WithMany()
                    .HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(a => new { a.MaintenanceRequestId, a.CreatedAt });
            });

            // ==================== توقيع المستخدم ====================
            builder.Entity<UserSignature>(entity =>
            {
                entity.HasKey(s => s.UserId);
                entity.HasOne(s => s.User).WithOne()
                    .HasForeignKey<UserSignature>(s => s.UserId).OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
