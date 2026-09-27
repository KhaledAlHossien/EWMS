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
        public DbSet<User> Users { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<Permission> Permissions { get; set; }
        public DbSet<RolePermission> RolePermissions { get; set; }
        public DbSet<Vacation> Vacation { get; set; }
        public DbSet<VacationType> VacationType { get; set; }
        public DbSet<UserToken> UserTokens { get; set; }

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

            builder.Entity<RolePermission>()
                .HasIndex(rp => new { rp.RoleId, rp.PermissionId })
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

                entity.Property(v => v.AdministrativeAccept)
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