using Domain.Entities;
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
        public DbSet<UserToken> UserTokens { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<Region> Regions { get; set; }
        public DbSet<Site> Sites { get; set; }
        public DbSet<Device> Devices { get; set; }
        public DbSet<DeviceSite> DeviceSites { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // ==================== الفهارس الفريدة (Unique Indexes) ====================
            builder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

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

            builder.Entity<Region>()
                .HasIndex(r => r.Name)
                .IsUnique();

            builder.Entity<Site>()
                .HasIndex(s => s.Name)
                .IsUnique();

            builder.Entity<DeviceSite>()
                .HasIndex(ds => new { ds.DeviceId, ds.SiteId })
                .IsUnique();

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

            // ---------- Region / Site / Device ----------
            // 7.1 Site -> Region
            builder.Entity<Site>()
                .HasOne(s => s.Region)
                .WithMany()
                .HasForeignKey(s => s.RegionId)
                .OnDelete(DeleteBehavior.Restrict);

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

                

                entity.Property(v => v.BranchManagerAccept)
                    .HasDefaultValue(true);

                entity.Property(v => v.Status)
                    .HasConversion<int>()   // خزّنه كـ int
                    .HasDefaultValue(VacationStatus.PendingManager);

                entity.Property(v => v.RejectionReason)
                    .HasMaxLength(500);

                entity.Property(v => v.IsPaid)
                    .HasDefaultValue(true);


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
        }
    }
}