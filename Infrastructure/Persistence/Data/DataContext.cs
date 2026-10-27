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
        public DbSet<VacationAttachment> VacationAttachments { get; set; }
        public DbSet<VacationAttachmentContent> VacationAttachmentContents { get; set; }
        public DbSet<UserToken> UserTokens { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<Site> Sites { get; set; }
        public DbSet<Device> Devices { get; set; }
        public DbSet<DeviceSite> DeviceSites { get; set; }
        // طبقات خرائط الفروع (1.2.0)
        public DbSet<MapLayer> MapLayers { get; set; }
        public DbSet<MapLayerField> MapLayerFields { get; set; }
        public DbSet<MapEntry> MapEntries { get; set; }
        public DbSet<MapEntryFile> MapEntryFiles { get; set; }
        public DbSet<MapEntryFileContent> MapEntryFileContents { get; set; }
        public DbSet<DeviceInventoryLog> DeviceInventoryLogs { get; set; }
        public DbSet<ReportExportLog> ReportExportLogs { get; set; }
        public DbSet<Project> Projects { get; set; }
        public DbSet<ProjectMember> ProjectMembers { get; set; }
        public DbSet<ProjectDocument> ProjectDocuments { get; set; }
        public DbSet<ProjectDocumentVersion> ProjectDocumentVersions { get; set; }
        public DbSet<ProjectDocumentVersionContent> ProjectDocumentVersionContents { get; set; }
        public DbSet<ProjectUpload> ProjectUploads { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<ProjectStartOrder> ProjectStartOrders { get; set; }
        public DbSet<ProjectSchedule> ProjectSchedules { get; set; }
        public DbSet<ScheduleTask> ScheduleTasks { get; set; }
        public DbSet<ScheduleDependency> ScheduleDependencies { get; set; }
        public DbSet<ScheduleProgressEntry> ScheduleProgressEntries { get; set; }
        public DbSet<ScheduleDelay> ScheduleDelays { get; set; }
        public DbSet<ScheduleDelayAffectedTask> ScheduleDelayAffectedTasks { get; set; }
        public DbSet<ScheduleExtension> ScheduleExtensions { get; set; }
        public DbSet<ScheduleExtensionItem> ScheduleExtensionItems { get; set; }
        public DbSet<ScheduleExtensionDocument> ScheduleExtensionDocuments { get; set; }
        public DbSet<WorkTask> WorkTasks { get; set; }
        public DbSet<UserWorkTask> UserWorkTasks { get; set; }
        public DbSet<AssignedTask> AssignedTasks { get; set; }
        public DbSet<AssignedTaskActivity> AssignedTaskActivities { get; set; }
        public DbSet<AssignedTaskAttachment> AssignedTaskAttachments { get; set; }
        public DbSet<AssignedTaskAttachmentContent> AssignedTaskAttachmentContents { get; set; }
        public DbSet<AssignedTaskChecklistItem> AssignedTaskChecklistItems { get; set; }
        public DbSet<AssignedTaskTemplate> AssignedTaskTemplates { get; set; }
        public DbSet<AssignedTaskTemplateItem> AssignedTaskTemplateItems { get; set; }
        public DbSet<AssignedTaskRecurrence> AssignedTaskRecurrences { get; set; }
        public DbSet<AssignedTaskLink> AssignedTaskLinks { get; set; }
        public DbSet<DeviceType> DeviceTypes { get; set; }
        public DbSet<DeviceCompany> DeviceCompanies { get; set; }
        public DbSet<DamageType> DamageTypes { get; set; }
        public DbSet<MaintenanceRequestStatus> MaintenanceRequestStatuses { get; set; }
        public DbSet<DeviceMaintenance> DeviceMaintenances { get; set; }
        public DbSet<MaintenanceRequest> MaintenanceRequests { get; set; }
        public DbSet<MaintenanceTask> MaintenanceTasks { get; set; }
        public DbSet<MaintenanceRequestActivity> MaintenanceRequestActivities { get; set; }
        public DbSet<MaintenanceTransferRequest> MaintenanceTransferRequests { get; set; }
        public DbSet<SparePart> SpareParts { get; set; }
        public DbSet<SparePartDeviceType> SparePartDeviceTypes { get; set; }
        public DbSet<SparePartDeviceCompany> SparePartDeviceCompanies { get; set; }
        public DbSet<SparePartMovement> SparePartMovements { get; set; }
        public DbSet<MaintenanceRequestPart> MaintenanceRequestParts { get; set; }
        public DbSet<UserSignature> UserSignatures { get; set; }
        public DbSet<ToDoList> ToDoLists { get; set; }
        public DbSet<ToDoItem> ToDoItems { get; set; }

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

            // ==================== توثيق الأجهزة ====================
            builder.Entity<Site>(entity =>
            {
                entity.Property(s => s.Name).HasMaxLength(100).IsRequired();
                entity.Property(s => s.Description).HasMaxLength(500);
                entity.Property(s => s.GovernorateCode).HasMaxLength(10);
                entity.Property(s => s.ContactName).HasMaxLength(100);
                entity.Property(s => s.ContactPhone).HasMaxLength(20);
                entity.Property(s => s.ResponsibleParty).HasMaxLength(150);
                entity.Property(s => s.RowVersion).IsRowVersion();
                entity.HasIndex(s => s.GovernorateCode);
                entity.HasIndex(s => s.Name).IsUnique();
            });

            // الجهاز في الكتالوج: الاسم + الموديل فريدان معاً
            builder.Entity<Device>(entity =>
            {
                entity.Property(d => d.Name).HasMaxLength(100).IsRequired();
                entity.Property(d => d.Model).HasMaxLength(100);
                entity.Property(d => d.Description).HasMaxLength(500);
                entity.Property(d => d.Category).HasMaxLength(50);
                entity.Property(d => d.Manufacturer).HasMaxLength(100);
                entity.Property(d => d.RowVersion).IsRowVersion();
                entity.HasIndex(d => new { d.Name, d.Model }).IsUnique();
            });

            // لا فهرس فريد على (DeviceId, SiteId): نفس الجهاز قد يُركَّب أكثر من مرة في نفس الموقع
            builder.Entity<DeviceSite>(entity =>
            {
                entity.Property(ds => ds.Ip).HasMaxLength(15);
                entity.Property(ds => ds.SubnetMask).HasMaxLength(15);
                entity.Property(ds => ds.Gateway).HasMaxLength(15);
                entity.Property(ds => ds.UserName).HasMaxLength(100);
                entity.Property(ds => ds.Pass).HasMaxLength(2000);   // مشفّرة (أطول من الأصل)
                entity.Property(ds => ds.Note).HasMaxLength(1000);
                entity.Property(ds => ds.SN).HasMaxLength(100);
                entity.Property(ds => ds.InstallLocation).HasMaxLength(300);
                entity.Property(ds => ds.MacAddress).HasMaxLength(17);
                entity.Property(ds => ds.Port).HasMaxLength(50);
                entity.Property(ds => ds.Firmware).HasMaxLength(100);
                entity.Property(ds => ds.Status).HasConversion<int>();
                entity.Property(ds => ds.RowVersion).IsRowVersion();
                // البحث بالـ IP والرقم التسلسلي، وتنبيه تكرار الـ IP داخل الموقع
                entity.HasIndex(ds => new { ds.SiteId, ds.Ip });
                entity.HasIndex(ds => ds.Ip);
                entity.HasIndex(ds => ds.SN);
                entity.HasIndex(ds => ds.Status);
                // الوحدة المالكة: حدّ العرض وخريطة الفرع. Restrict + حراسة حذف القسم/الفرع
                entity.HasOne(ds => ds.Branch).WithMany().HasForeignKey(ds => ds.BranchId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(ds => ds.Department).WithMany().HasForeignKey(ds => ds.DepartmentId).OnDelete(DeleteBehavior.Restrict);
            });

            // ==================== طبقات خرائط الفروع (1.2.0) ====================
            builder.Entity<MapLayer>(entity =>
            {
                entity.Property(l => l.Name).HasMaxLength(100).IsRequired();
                entity.Property(l => l.Description).HasMaxLength(500);
                entity.Property(l => l.Color).HasMaxLength(7);
                entity.Property(l => l.Source).HasMaxLength(50);
                entity.Property(l => l.SourceFilter).HasMaxLength(2000);
                entity.Property(l => l.RowVersion).IsRowVersion();
                entity.HasIndex(l => new { l.BranchId, l.Name }).IsUnique();
                entity.HasOne(l => l.Branch).WithMany().HasForeignKey(l => l.BranchId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(l => l.CreatedByUser).WithMany().HasForeignKey(l => l.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
                entity.HasMany(l => l.Fields).WithOne(f => f.Layer).HasForeignKey(f => f.LayerId).OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<MapLayerField>(entity =>
            {
                entity.Property(f => f.Label).HasMaxLength(100).IsRequired();
                entity.Property(f => f.Type).HasConversion<int>();
                entity.Property(f => f.Options).HasMaxLength(4000);
                entity.Property(f => f.FileKinds).HasMaxLength(400);
            });

            builder.Entity<MapEntry>(entity =>
            {
                entity.Property(e => e.Values).IsRequired();
                entity.Property(e => e.RowVersion).IsRowVersion();
                // حذف الطبقة أو الموقع أو القسم أو الفرع أو المستخدم مرفوض ما دامت سجلات تشير إليه (حراسة برسالة في الـ handlers)
                entity.HasOne(e => e.Layer).WithMany().HasForeignKey(e => e.LayerId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.Site).WithMany().HasForeignKey(e => e.SiteId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.Branch).WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.Department).WithMany().HasForeignKey(e => e.DepartmentId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.CreatedByUser).WithMany().HasForeignKey(e => e.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.UpdatedByUser).WithMany().HasForeignKey(e => e.UpdatedByUserId).OnDelete(DeleteBehavior.Restrict);
                entity.HasIndex(e => new { e.LayerId, e.SiteId });
            });

            // ملفات حقول «ملفات»: تُحذف مع سجلها أو حقلها (الحذف النهائي)، والمحتوى مع بياناته
            builder.Entity<MapEntryFile>(entity =>
            {
                entity.Property(f => f.FileName).HasMaxLength(200).IsRequired();
                entity.Property(f => f.ContentType).HasMaxLength(150).IsRequired();
                entity.Property(f => f.Kind).HasMaxLength(20).IsRequired();
                entity.Property(f => f.StorageKey).HasMaxLength(80);
                entity.HasIndex(f => f.StorageKey).IsUnique().HasFilter("[StorageKey] IS NOT NULL");
                entity.HasOne(f => f.Entry).WithMany(e => e.Files).HasForeignKey(f => f.EntryId).OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(f => f.Field).WithMany().HasForeignKey(f => f.FieldId).OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(f => f.UploadedByUser).WithMany().HasForeignKey(f => f.UploadedByUserId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(f => f.Content).WithOne(c => c.File).HasForeignKey<MapEntryFileContent>(c => c.FileId).OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(f => new { f.EntryId, f.FieldId });
                entity.HasIndex(f => f.UploadedAt);
            });

            builder.Entity<MapEntryFileContent>(entity => entity.HasKey(c => c.FileId));

            builder.Entity<DeviceInventoryLog>(entity =>
            {
                entity.Property(l => l.EntityType).HasConversion<int>();
                entity.Property(l => l.Action).HasConversion<int>();
                entity.Property(l => l.Title).HasMaxLength(300);
                entity.Property(l => l.Details).HasMaxLength(4000);
                entity.HasOne(l => l.User).WithMany().HasForeignKey(l => l.UserId).OnDelete(DeleteBehavior.Restrict);
                entity.HasIndex(l => new { l.EntityType, l.EntityId, l.CreatedAt });
                entity.HasIndex(l => new { l.UserId, l.CreatedAt });
            });

            // سجل سحب التقارير: المستخدم Restrict (يُعطَّل لا يُحذف)، والفرع لقطة بلا مفتاح أجنبي
            builder.Entity<ReportExportLog>(entity =>
            {
                entity.Property(l => l.ReportKey).HasMaxLength(50).IsRequired();
                entity.Property(l => l.ReportTitle).HasMaxLength(150).IsRequired();
                entity.Property(l => l.Filters).HasMaxLength(1000);
                entity.Property(l => l.Format).HasConversion<int>();
                entity.HasOne(l => l.User).WithMany().HasForeignKey(l => l.UserId).OnDelete(DeleteBehavior.Restrict);
                entity.HasIndex(l => l.CreatedAt);
                entity.HasIndex(l => new { l.UserId, l.CreatedAt });
                entity.HasIndex(l => new { l.BranchId, l.CreatedAt });
            });

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
                // تعديلان متزامنان على المهمة → الثاني يفشل (DbUpdateConcurrencyException) ويُعرض برسالة
                entity.Property(t => t.RowVersion).IsRowVersion();
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
                entity.HasOne(t => t.ClaimedByUser).WithMany().HasForeignKey(t => t.ClaimedByUserId).OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(t => new { t.DepartmentId, t.Status });
                entity.HasIndex(t => new { t.OfficeId, t.Status });
                entity.HasIndex(t => new { t.AssigneeUserId, t.Status });
                entity.HasIndex(t => new { t.CreatedByUserId, t.Status });
                // مسح التذكيرات: المهام المفتوحة ذات الموعد فقط
                entity.HasIndex(t => new { t.Status, t.DueDate });
            });

            // مرفقات المهمة ومحتواها وقائمة التحقق: جزء من المهمة (تُحذف معها)، والرافع Restrict (حراسة حذف المستخدم)
            builder.Entity<AssignedTaskAttachment>(entity =>
            {
                entity.Property(a => a.FileName).HasMaxLength(200).IsRequired();
                entity.Property(a => a.ContentType).HasMaxLength(150).IsRequired();
                entity.Property(a => a.StorageKey).HasMaxLength(80);
                entity.HasIndex(a => a.StorageKey).IsUnique().HasFilter("[StorageKey] IS NOT NULL");
                entity.HasOne(a => a.AssignedTask).WithMany(t => t.Attachments).HasForeignKey(a => a.AssignedTaskId).OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(a => a.UploadedByUser).WithMany().HasForeignKey(a => a.UploadedByUserId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(a => a.Content).WithOne().HasForeignKey<AssignedTaskAttachmentContent>(c => c.AttachmentId).OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(a => a.AssignedTaskId);
                entity.HasIndex(a => a.UploadedByUserId);
            });

            builder.Entity<AssignedTaskAttachmentContent>(entity =>
            {
                entity.HasKey(c => c.AttachmentId);
                entity.Property(c => c.Data).IsRequired();
            });

            builder.Entity<AssignedTaskChecklistItem>(entity =>
            {
                entity.Property(i => i.Text).HasMaxLength(200).IsRequired();
                entity.HasOne(i => i.AssignedTask).WithMany(t => t.ChecklistItems).HasForeignKey(i => i.AssignedTaskId).OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(i => new { i.AssignedTaskId, i.SortOrder });
            });

            // قوالب المهام والمهام الدورية والروابط: القالب والرابط جزء من مالكهما، والمستخدم Restrict (حراسة حذف المستخدم)
            builder.Entity<AssignedTaskTemplate>(entity =>
            {
                entity.Property(t => t.Name).HasMaxLength(100).IsRequired();
                entity.Property(t => t.Title).HasMaxLength(200).IsRequired();
                entity.Property(t => t.Description).HasMaxLength(4000);
                entity.Property(t => t.Priority).HasConversion<int>();
                entity.HasOne(t => t.OwnerUser).WithMany().HasForeignKey(t => t.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
                entity.HasIndex(t => new { t.OwnerUserId, t.Name }).IsUnique();
            });

            builder.Entity<AssignedTaskTemplateItem>(entity =>
            {
                entity.Property(i => i.Text).HasMaxLength(200).IsRequired();
                entity.HasOne(i => i.Template).WithMany(t => t.Items).HasForeignKey(i => i.TemplateId).OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(i => new { i.TemplateId, i.SortOrder });
            });

            builder.Entity<AssignedTaskRecurrence>(entity =>
            {
                entity.Property(r => r.TargetType).HasConversion<int>();
                entity.Property(r => r.Frequency).HasConversion<int>();
                entity.Property(r => r.LastError).HasMaxLength(500);
                entity.HasOne(r => r.OwnerUser).WithMany().HasForeignKey(r => r.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(r => r.Template).WithMany().HasForeignKey(r => r.TemplateId).OnDelete(DeleteBehavior.Restrict);
                entity.HasIndex(r => new { r.IsActive, r.NextRunDate });
                entity.HasIndex(r => r.OwnerUserId);
            });

            builder.Entity<AssignedTaskLink>(entity =>
            {
                entity.Property(l => l.EntityType).HasConversion<int>();
                entity.HasOne(l => l.AssignedTask).WithMany(t => t.Links).HasForeignKey(l => l.AssignedTaskId).OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(l => l.CreatedByUser).WithMany().HasForeignKey(l => l.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
                entity.HasIndex(l => new { l.AssignedTaskId, l.EntityType, l.EntityId }).IsUnique();
                entity.HasIndex(l => new { l.EntityType, l.EntityId });
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
                entity.Property(x => x.Stage).HasConversion<int>();
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
                // موقّع ورقة التسليم ونسخة توقيعه وقت التسليم
                entity.HasOne(r => r.DeliverySigner).WithMany().HasForeignKey(r => r.DeliverySignerId).OnDelete(DeleteBehavior.Restrict);
                // العميل الموظف — طلباته في «أجهزتي في الصيانة»
                entity.HasOne(r => r.ClientUser).WithMany().HasForeignKey(r => r.ClientUserId).OnDelete(DeleteBehavior.Restrict);
                entity.HasIndex(r => new { r.ClientUserId, r.CreatedAt });
                entity.HasOne(r => r.DeliverySignature).WithMany().HasForeignKey(r => r.DeliverySignatureId).OnDelete(DeleteBehavior.Restrict);

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

                // لا HasDefaultValue على الـ enum أيضاً (نفس فخ الـ bool): 0 هو الافتراضي في C# فيعتبره EF "غير محددة"
                // ويحذفه من INSERT فتكتب القاعدة حالة افتراضية بصمت. الحالة تُضبط دائماً من مُهيّئ الـ Entity
                // ومن CreateVacationCommandHandler (أزالته migration Vacation_Status_Remove_Default).
                entity.Property(v => v.Status)
                    .HasConversion<int>();   // خزّنه كـ int

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

                // التوقيع المحفوظ مع الاعتماد النهائي ومع الرفض (نسخة التوقيع وقتها — لا يتغير بعد ذلك)
                entity.HasOne(v => v.FinalApprovedSignature)
                    .WithMany()
                    .HasForeignKey(v => v.FinalApprovedSignatureId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(v => v.RejectedSignature)
                    .WithMany()
                    .HasForeignKey(v => v.RejectedSignatureId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(v => v.RequestSignature)
                    .WithMany()
                    .HasForeignKey(v => v.RequestSignatureId)
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

            // ==================== مرفقات الإجازة: البيانات الوصفية + المحتوى في جدول مستقل ====================
            builder.Entity<VacationAttachment>(entity =>
            {
                entity.Property(a => a.FileName).HasMaxLength(200).IsRequired();
                entity.Property(a => a.ContentType).HasMaxLength(100).IsRequired();
                entity.Property(a => a.StorageKey).HasMaxLength(80);
                entity.HasIndex(a => a.StorageKey).IsUnique().HasFilter("[StorageKey] IS NOT NULL");
                entity.HasOne(a => a.Vacation)
                    .WithMany(v => v.Attachments)
                    .HasForeignKey(a => a.VacationId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(a => a.Content)
                    .WithOne()
                    .HasForeignKey<VacationAttachmentContent>(c => c.AttachmentId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
            builder.Entity<VacationAttachmentContent>(entity =>
            {
                entity.HasKey(c => c.AttachmentId);
                entity.Property(c => c.Data).IsRequired();
            });

            // ==================== العطل الرسمية ====================
            // عطلة رسمية بفترة (سجل لكل عطلة) — العطل لا تتداخل (يفحصه المعالج)، فبداية العطلة فريدة
            builder.Entity<PublicHoliday>(entity =>
            {
                entity.Property(h => h.StartDate).HasColumnType("date");
                entity.Property(h => h.EndDate).HasColumnType("date");
                entity.Property(h => h.Name).HasMaxLength(100).IsRequired();
                entity.HasIndex(h => h.StartDate).IsUnique();
                entity.HasIndex(h => new { h.EndDate, h.StartDate });
            });

            // ==================== الصيانة: سجل الطلب ====================
            // طلبات التحويل: جزء من الطلب (تُحذف معه)، والمستخدمون Restrict (حماية حذف المستخدم في ExistsForUserAsync)
            builder.Entity<MaintenanceTransferRequest>(entity =>
            {
                entity.Property(t => t.Reason).HasMaxLength(500).IsRequired();
                entity.Property(t => t.DecisionNote).HasMaxLength(500);
                entity.Property(t => t.Status).HasConversion<int>();
                entity.HasOne(t => t.MaintenanceRequest).WithMany()
                    .HasForeignKey(t => t.MaintenanceRequestId).OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(t => t.RequestedBy).WithMany().HasForeignKey(t => t.RequestedById).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(t => t.SuggestedUser).WithMany().HasForeignKey(t => t.SuggestedUserId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(t => t.DecidedBy).WithMany().HasForeignKey(t => t.DecidedById).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(t => t.NewUser).WithMany().HasForeignKey(t => t.NewUserId).OnDelete(DeleteBehavior.Restrict);
                // طلب تحويل معلّق واحد على الأكثر لكل طلب صيانة
                entity.HasIndex(t => t.MaintenanceRequestId).IsUnique().HasFilter("[Status] = 1");
                entity.HasIndex(t => new { t.Status, t.CreatedAt });
            });

            // ==================== الصيانة: مخزون قطع الغيار ====================
            // مخزون لكل قسم؛ الكمية والمتوسط يتغيّران بالحركات فقط. الحركات والقطع المصروفة Restrict
            // (لا تُحذف قطعة لها حركات، ولا طلب صُرفت عليه قطع، ولا مستخدم أو قسم له سجل مخزون)
            builder.Entity<SparePart>(entity =>
            {
                entity.Property(p => p.Name).HasMaxLength(200).IsRequired();
                entity.Property(p => p.PartNumber).HasMaxLength(100);
                entity.Property(p => p.Unit).HasMaxLength(30).IsRequired();
                entity.Property(p => p.Description).HasMaxLength(1000);
                entity.Property(p => p.Quantity).HasPrecision(18, 2);
                entity.Property(p => p.MinQuantity).HasPrecision(18, 2);
                entity.Property(p => p.AverageCost).HasPrecision(18, 2);

                entity.HasOne(p => p.Department).WithMany().HasForeignKey(p => p.DepartmentId).OnDelete(DeleteBehavior.Restrict);

                // اسم القطعة فريد داخل مخزون القسم
                entity.HasIndex(p => new { p.DepartmentId, p.Name }).IsUnique();
                entity.HasIndex(p => p.PartNumber);
                entity.ToTable(t => t.HasCheckConstraint("CK_SpareParts_Quantity", "[Quantity] >= 0"));
            });

            // التوافق جزء من القطعة، وحذف نوع/شركة يزيل التوافق فقط
            builder.Entity<SparePartDeviceType>(entity =>
            {
                entity.HasKey(x => new { x.SparePartId, x.DeviceTypeId });
                entity.HasOne(x => x.SparePart).WithMany(p => p.DeviceTypes).HasForeignKey(x => x.SparePartId).OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(x => x.DeviceType).WithMany().HasForeignKey(x => x.DeviceTypeId).OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<SparePartDeviceCompany>(entity =>
            {
                entity.HasKey(x => new { x.SparePartId, x.DeviceCompanyId });
                entity.HasOne(x => x.SparePart).WithMany(p => p.DeviceCompanies).HasForeignKey(x => x.SparePartId).OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(x => x.DeviceCompany).WithMany().HasForeignKey(x => x.DeviceCompanyId).OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<SparePartMovement>(entity =>
            {
                entity.Property(m => m.Type).HasConversion<int>();
                entity.Property(m => m.Quantity).HasPrecision(18, 2);
                entity.Property(m => m.UnitCost).HasPrecision(18, 2);
                entity.Property(m => m.BalanceAfter).HasPrecision(18, 2);
                entity.Property(m => m.Note).HasMaxLength(500);

                entity.HasOne(m => m.SparePart).WithMany().HasForeignKey(m => m.SparePartId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(m => m.MaintenanceRequest).WithMany().HasForeignKey(m => m.MaintenanceRequestId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(m => m.User).WithMany().HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(m => new { m.SparePartId, m.CreatedAt });
                entity.HasIndex(m => new { m.Type, m.Date });
            });

            builder.Entity<MaintenanceRequestPart>(entity =>
            {
                entity.Property(p => p.Quantity).HasPrecision(18, 2);
                entity.Property(p => p.UnitCost).HasPrecision(18, 2);

                entity.HasOne(p => p.MaintenanceRequest).WithMany().HasForeignKey(p => p.MaintenanceRequestId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(p => p.SparePart).WithMany().HasForeignKey(p => p.SparePartId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(p => p.IssuedBy).WithMany().HasForeignKey(p => p.IssuedById).OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(p => p.MaintenanceRequestId);
                entity.HasIndex(p => new { p.SparePartId, p.IssuedAt });
            });

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

            // ==================== قوائم المهام الشخصية ====================
            // قائمة شخصية: تُحذف مع صاحبها (مثل الإشعارات)، واسمها فريد داخل قوائمه
            builder.Entity<ToDoList>(entity =>
            {
                entity.Property(l => l.Name).HasMaxLength(200).IsRequired();
                entity.Property(l => l.Description).HasMaxLength(2000);
                entity.Property(l => l.Color).HasMaxLength(20);
                entity.Property(l => l.Icon).HasMaxLength(16);

                entity.HasOne(l => l.User).WithMany()
                    .HasForeignKey(l => l.UserId).OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(l => new { l.UserId, l.Name }).IsUnique();
            });

            // بند القائمة: جزء منها (يُحذف معها)
            builder.Entity<ToDoItem>(entity =>
            {
                entity.Property(i => i.Title).HasMaxLength(200).IsRequired();
                entity.Property(i => i.Note).HasMaxLength(1000);
                entity.Property(i => i.Color).HasMaxLength(10);
                entity.Property(i => i.Repeat).HasConversion<int?>();
                entity.HasOne(i => i.ToDoList).WithMany(l => l.Items)
                    .HasForeignKey(i => i.ToDoListId).OnDelete(DeleteBehavior.Cascade);
                // حذف المهمة من لوحة المهام لا يمنع ولا يحذف بند المفكرة: يُفكّ الربط فقط
                entity.HasOne(i => i.LinkedTask).WithMany()
                    .HasForeignKey(i => i.LinkedTaskId).OnDelete(DeleteBehavior.SetNull);
                entity.HasIndex(i => new { i.ToDoListId, i.SortOrder });
                // مسح «اليوم» والتذكيرات: غير المنجزة ذات الموعد
                entity.HasIndex(i => new { i.IsDone, i.DueDate });
            });

            // ==================== توقيع المستخدم ====================
            // نسخ التوقيع: لكل مستخدم عدة نسخ، واحدة حالية على الأكثر (فهرس فريد مُرشَّح)
            builder.Entity<UserSignature>(entity =>
            {
                entity.HasKey(s => s.Id);
                entity.HasOne(s => s.User).WithMany()
                    .HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(s => s.UserId).IsUnique().HasFilter("[IsCurrent] = 1");
            });

            ConfigureProjects(builder);
            ConfigureSchedule(builder);
        }

        // ==================== المشاريع والوثائق وسجل التدقيق (2026-10-27) ====================
        // كل المفاتيح Restrict: المشروع لا يُحذف أبداً (يُلغى)، فلا شيء يتسلسل حذفه إلا إصدارات الوثيقة ومحتواها.
        private static void ConfigureProjects(ModelBuilder builder)
        {
            builder.Entity<Project>(entity =>
            {
                entity.Property(p => p.ReferenceNumber).HasMaxLength(50);
                entity.Property(p => p.Name).HasMaxLength(200).IsRequired();
                entity.Property(p => p.Beneficiary).HasMaxLength(200);
                entity.Property(p => p.Description).HasMaxLength(4000);
                entity.Property(p => p.Stage).HasConversion<int>();
                entity.Property(p => p.RowVersion).IsRowVersion();
                entity.HasOne(p => p.Branch).WithMany().HasForeignKey(p => p.BranchId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(p => p.Department).WithMany().HasForeignKey(p => p.DepartmentId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(p => p.Site).WithMany().HasForeignKey(p => p.SiteId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(p => p.CreatedByUser).WithMany().HasForeignKey(p => p.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
                entity.HasIndex(p => new { p.BranchId, p.ReferenceNumber }).IsUnique().HasFilter("[ReferenceNumber] IS NOT NULL");
                entity.HasIndex(p => new { p.BranchId, p.Stage });
                entity.HasIndex(p => p.DepartmentId);
                entity.HasIndex(p => p.SiteId);
                entity.HasIndex(p => p.CreatedByUserId);
                entity.HasIndex(p => p.CreatedAt);
            });

            builder.Entity<ProjectMember>(entity =>
            {
                entity.HasKey(m => new { m.ProjectId, m.UserId });
                entity.Property(m => m.Title).HasMaxLength(100);
                entity.HasOne(m => m.Project).WithMany(p => p.Members).HasForeignKey(m => m.ProjectId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(m => m.User).WithMany().HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Restrict);
                entity.HasIndex(m => m.UserId);
            });

            builder.Entity<ProjectDocument>(entity =>
            {
                entity.Property(d => d.Title).HasMaxLength(200).IsRequired();
                entity.Property(d => d.Kind).HasConversion<int>();
                entity.HasOne(d => d.Project).WithMany(p => p.Documents).HasForeignKey(d => d.ProjectId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(d => d.CreatedByUser).WithMany().HasForeignKey(d => d.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
                entity.HasIndex(d => new { d.ProjectId, d.Kind });
                entity.HasIndex(d => d.CreatedByUserId);
            });

            builder.Entity<ProjectDocumentVersion>(entity =>
            {
                entity.Property(v => v.FileName).HasMaxLength(260).IsRequired();
                entity.Property(v => v.ContentType).HasMaxLength(150).IsRequired();
                entity.Property(v => v.Sha256).HasMaxLength(64).IsRequired();
                entity.Property(v => v.StorageKey).HasMaxLength(80);
                entity.Property(v => v.Source).HasMaxLength(200);
                entity.Property(v => v.Note).HasMaxLength(1000);
                entity.HasOne(v => v.Document).WithMany(d => d.Versions).HasForeignKey(v => v.DocumentId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(v => v.UploadedByUser).WithMany().HasForeignKey(v => v.UploadedByUserId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(v => v.ApprovedByUser).WithMany().HasForeignKey(v => v.ApprovedByUserId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(v => v.Content).WithOne(c => c.Version).HasForeignKey<ProjectDocumentVersionContent>(c => c.VersionId).OnDelete(DeleteBehavior.Cascade);
                // رقم الإصدار فريد داخل الوثيقة: رفعان متزامنان لا يأخذان الرقم نفسه (الثاني يُعاد بحجز الرقم التالي)
                entity.HasIndex(v => new { v.DocumentId, v.VersionNumber }).IsUnique();
                entity.HasIndex(v => v.StorageKey).IsUnique().HasFilter("[StorageKey] IS NOT NULL");
                entity.HasIndex(v => v.UploadedByUserId);
                entity.HasIndex(v => v.ApprovedByUserId);
            });

            builder.Entity<ProjectDocumentVersionContent>(entity => entity.HasKey(c => c.VersionId));

            builder.Entity<ProjectUpload>(entity =>
            {
                entity.Property(u => u.Title).HasMaxLength(200);
                entity.Property(u => u.Kind).HasConversion<int>();
                entity.Property(u => u.FileName).HasMaxLength(260).IsRequired();
                entity.Property(u => u.Source).HasMaxLength(200);
                entity.Property(u => u.Note).HasMaxLength(1000);
                entity.Property(u => u.Status).HasConversion<int>();
                entity.Property(u => u.Error).HasMaxLength(500);
                entity.HasOne(u => u.Project).WithMany().HasForeignKey(u => u.ProjectId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(u => u.Document).WithMany().HasForeignKey(u => u.DocumentId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(u => u.User).WithMany().HasForeignKey(u => u.UserId).OnDelete(DeleteBehavior.Restrict);
                entity.HasIndex(u => new { u.UserId, u.Status });
                entity.HasIndex(u => u.UpdatedAt);
                entity.HasIndex(u => u.ProjectId);
                entity.HasIndex(u => u.DocumentId);
            });

            builder.Entity<AuditLog>(entity =>
            {
                entity.Property(a => a.Action).HasMaxLength(40).IsRequired();
                entity.Property(a => a.EntityType).HasMaxLength(40).IsRequired();
                entity.Property(a => a.Title).HasMaxLength(300);
                entity.Property(a => a.Details).HasMaxLength(4000);
                entity.HasOne(a => a.User).WithMany().HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(a => a.Project).WithMany().HasForeignKey(a => a.ProjectId).OnDelete(DeleteBehavior.Restrict);
                entity.HasIndex(a => new { a.ProjectId, a.CreatedAt });
                entity.HasIndex(a => new { a.EntityType, a.EntityId });
                entity.HasIndex(a => new { a.BranchId, a.CreatedAt });
                entity.HasIndex(a => new { a.UserId, a.CreatedAt });
            });
        }

        // ==================== الجدول الزمني والتأخير والتمديد (المرحلة 1، 2026-10-27) ====================
        // مفتاح الجدول يتسلسل إلى بنوده فقط (حذف مسودة)؛ كل ما سواه Restrict. التبعية لا تتسلسل (مساران إلى البند): تُحذف قبل مسودتها في الخدمة.
        private static void ConfigureSchedule(ModelBuilder builder)
        {
            builder.Entity<ProjectStartOrder>(entity =>
            {
                entity.Property(o => o.OrderNumber).HasMaxLength(50).IsRequired();
                entity.Property(o => o.OrderDate).HasColumnType("date");
                entity.Property(o => o.StartDate).HasColumnType("date");
                entity.Property(o => o.StudyCost).HasColumnType("decimal(18,2)");
                entity.Property(o => o.Notes).HasMaxLength(2000);
                entity.Property(o => o.RowVersion).IsRowVersion();
                entity.HasOne(o => o.Project).WithMany().HasForeignKey(o => o.ProjectId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(o => o.SiteEngineer).WithMany().HasForeignKey(o => o.SiteEngineerUserId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(o => o.SectorSupervisor).WithMany().HasForeignKey(o => o.SectorSupervisorUserId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(o => o.CreatedByUser).WithMany().HasForeignKey(o => o.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
                entity.HasIndex(o => o.ProjectId).IsUnique();
                entity.HasIndex(o => o.SiteEngineerUserId);
                entity.HasIndex(o => o.SectorSupervisorUserId);
                entity.HasIndex(o => o.CreatedByUserId);
            });

            builder.Entity<ProjectSchedule>(entity =>
            {
                entity.Property(s => s.Title).HasMaxLength(200);
                entity.Property(s => s.Note).HasMaxLength(1000);
                entity.Property(s => s.Status).HasConversion<int>();
                entity.Property(s => s.RowVersion).IsRowVersion();
                entity.HasOne(s => s.Project).WithMany().HasForeignKey(s => s.ProjectId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(s => s.CreatedByUser).WithMany().HasForeignKey(s => s.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(s => s.ApprovedByUser).WithMany().HasForeignKey(s => s.ApprovedByUserId).OnDelete(DeleteBehavior.Restrict);
                entity.HasIndex(s => new { s.ProjectId, s.VersionNumber }).IsUnique();
                // مسودة واحدة ومعتمد واحد على الأكثر لكل مشروع (فهرس واحد: EF لا يقبل فهرسين على الأعمدة نفسها)
                entity.HasIndex(s => new { s.ProjectId, s.Status }).IsUnique().HasFilter("[Status] IN (1, 2)").HasDatabaseName("IX_ProjectSchedules_OneDraftOneApproved");
                entity.HasIndex(s => s.CreatedByUserId);
                entity.HasIndex(s => s.ApprovedByUserId);
            });

            builder.Entity<ScheduleTask>(entity =>
            {
                entity.Property(t => t.Code).HasMaxLength(30);
                entity.Property(t => t.Name).HasMaxLength(300).IsRequired();
                entity.Property(t => t.Note).HasMaxLength(2000);
                entity.Property(t => t.Weight).HasColumnType("decimal(9,2)");
                entity.Property(t => t.Status).HasConversion<int>();
                entity.Property(t => t.PlannedStart).HasColumnType("date");
                entity.Property(t => t.PlannedEnd).HasColumnType("date");
                entity.Property(t => t.OriginalStart).HasColumnType("date");
                entity.Property(t => t.OriginalEnd).HasColumnType("date");
                entity.Property(t => t.ActualStart).HasColumnType("date");
                entity.Property(t => t.ActualEnd).HasColumnType("date");
                entity.Property(t => t.OverdueNotifiedFor).HasColumnType("date");
                entity.Property(t => t.RowVersion).IsRowVersion();
                entity.ToTable(t =>
                {
                    t.HasCheckConstraint("CK_ScheduleTasks_Dates", "[PlannedEnd] >= [PlannedStart]");
                    t.HasCheckConstraint("CK_ScheduleTasks_Progress", "[Progress] >= 0 AND [Progress] <= 100");
                    t.HasCheckConstraint("CK_ScheduleTasks_Weight", "[Weight] > 0");
                });
                entity.HasOne(t => t.Schedule).WithMany(s => s.Tasks).HasForeignKey(t => t.ScheduleId).OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(t => t.Assignee).WithMany().HasForeignKey(t => t.AssigneeUserId).OnDelete(DeleteBehavior.Restrict);
                entity.HasIndex(t => new { t.ScheduleId, t.SortOrder });
                entity.HasIndex(t => t.AssigneeUserId);
            });

            builder.Entity<ScheduleDependency>(entity =>
            {
                entity.HasOne(d => d.Schedule).WithMany().HasForeignKey(d => d.ScheduleId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(d => d.Predecessor).WithMany().HasForeignKey(d => d.PredecessorId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(d => d.Successor).WithMany().HasForeignKey(d => d.SuccessorId).OnDelete(DeleteBehavior.Restrict);
                entity.HasIndex(d => new { d.PredecessorId, d.SuccessorId }).IsUnique();
                entity.HasIndex(d => d.SuccessorId);
                entity.HasIndex(d => d.ScheduleId);
                entity.ToTable(t => t.HasCheckConstraint("CK_ScheduleDependencies_NotSelf", "[PredecessorId] <> [SuccessorId]"));
            });

            builder.Entity<ScheduleProgressEntry>(entity =>
            {
                entity.Property(e => e.Status).HasConversion<int>();
                entity.Property(e => e.Note).HasMaxLength(1000);
                entity.HasOne(e => e.Task).WithMany(t => t.ProgressHistory).HasForeignKey(e => e.TaskId).OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Restrict);
                entity.HasIndex(e => new { e.TaskId, e.CreatedAt });
                entity.HasIndex(e => e.UserId);
            });

            builder.Entity<ScheduleDelay>(entity =>
            {
                entity.Property(d => d.Kind).HasConversion<int>();
                entity.Property(d => d.Status).HasConversion<int>();
                entity.Property(d => d.DiscoveredAt).HasColumnType("date");
                entity.Property(d => d.PlannedEndAtDiscovery).HasColumnType("date");
                entity.Property(d => d.ApprovalDate).HasColumnType("date");
                entity.Property(d => d.Reason).HasMaxLength(2000);
                entity.Property(d => d.Actions).HasMaxLength(2000);
                entity.Property(d => d.ApprovalAuthority).HasMaxLength(200);
                entity.Property(d => d.DecisionNote).HasMaxLength(1000);
                entity.Property(d => d.RowVersion).IsRowVersion();
                entity.HasOne(d => d.Project).WithMany().HasForeignKey(d => d.ProjectId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(d => d.Task).WithMany().HasForeignKey(d => d.TaskId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(d => d.ApprovalDocumentVersion).WithMany().HasForeignKey(d => d.ApprovalDocumentVersionId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(d => d.RecordedByUser).WithMany().HasForeignKey(d => d.RecordedByUserId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(d => d.DecidedByUser).WithMany().HasForeignKey(d => d.DecidedByUserId).OnDelete(DeleteBehavior.Restrict);
                entity.HasIndex(d => new { d.ProjectId, d.Status });
                entity.HasIndex(d => d.TaskId);
                entity.HasIndex(d => d.ApprovalDocumentVersionId);
                entity.HasIndex(d => d.RecordedByUserId);
                entity.HasIndex(d => d.DecidedByUserId);
            });

            builder.Entity<ScheduleDelayAffectedTask>(entity =>
            {
                entity.HasKey(a => new { a.DelayId, a.TaskId });
                entity.HasOne(a => a.Delay).WithMany(d => d.AffectedTasks).HasForeignKey(a => a.DelayId).OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(a => a.Task).WithMany().HasForeignKey(a => a.TaskId).OnDelete(DeleteBehavior.Restrict);
                entity.HasIndex(a => a.TaskId);
            });

            builder.Entity<ScheduleExtension>(entity =>
            {
                entity.Property(x => x.Mode).HasConversion<int>();
                entity.Property(x => x.DayCount).HasConversion<int>();
                entity.Property(x => x.Status).HasConversion<int>();
                entity.Property(x => x.Reason).HasMaxLength(2000);
                entity.Property(x => x.DecisionNote).HasMaxLength(1000);
                entity.Property(x => x.RowVersion).IsRowVersion();
                entity.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(x => x.Schedule).WithMany().HasForeignKey(x => x.ScheduleId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(x => x.Delay).WithMany().HasForeignKey(x => x.DelayId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(x => x.RequestedByUser).WithMany().HasForeignKey(x => x.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(x => x.DecidedByUser).WithMany().HasForeignKey(x => x.DecidedByUserId).OnDelete(DeleteBehavior.Restrict);
                entity.HasIndex(x => new { x.ProjectId, x.Status });
                entity.HasIndex(x => x.ScheduleId);
                entity.HasIndex(x => x.DelayId);
                entity.HasIndex(x => x.RequestedByUserId);
                entity.HasIndex(x => x.DecidedByUserId);
            });

            builder.Entity<ScheduleExtensionItem>(entity =>
            {
                entity.Property(i => i.OldStart).HasColumnType("date");
                entity.Property(i => i.OldEnd).HasColumnType("date");
                entity.Property(i => i.NewStart).HasColumnType("date");
                entity.Property(i => i.NewEnd).HasColumnType("date");
                entity.HasOne(i => i.Extension).WithMany(x => x.Items).HasForeignKey(i => i.ExtensionId).OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(i => i.Task).WithMany().HasForeignKey(i => i.TaskId).OnDelete(DeleteBehavior.Restrict);
                entity.HasIndex(i => new { i.ExtensionId, i.TaskId }).IsUnique();
                entity.HasIndex(i => i.TaskId);
            });

            builder.Entity<ScheduleExtensionDocument>(entity =>
            {
                entity.HasKey(d => new { d.ExtensionId, d.DocumentVersionId });
                entity.HasOne(d => d.Extension).WithMany(x => x.Documents).HasForeignKey(d => d.ExtensionId).OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(d => d.DocumentVersion).WithMany().HasForeignKey(d => d.DocumentVersionId).OnDelete(DeleteBehavior.Restrict);
                entity.HasIndex(d => d.DocumentVersionId);
            });
        }
    }
}
