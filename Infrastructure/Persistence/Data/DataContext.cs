using Domain.Entities;
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
        public DbSet<Project> Projects { get; set; }
        public DbSet<ProjectAssignments> ProjectAssignments { get; set; }
        public DbSet<ProjectFile> ProjectFiles { get; set; }
        public DbSet<ProjectTransfers> ProjectTransfers { get; set; }
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

            // ---------- Project ----------
            // 7. Project -> User (المنشئ)
            builder.Entity<Project>()
                .HasOne(p => p.User)
                .WithMany()
                .HasForeignKey(p => p.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);

            // 8. Project -> Department (القسم الحالي)
            builder.Entity<Project>()
                .HasOne(p => p.Department)
                .WithMany()
                .HasForeignKey(p => p.CurrentDepartmentId)
                .OnDelete(DeleteBehavior.Restrict);

            // ---------- ProjectAssignments ----------
            // 9. ProjectAssignments -> Project
            builder.Entity<ProjectAssignments>()
                .HasOne(pa => pa.Project)
                .WithMany()
                .HasForeignKey(pa => pa.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            // 10. ProjectAssignments -> AssignedUser
            builder.Entity<ProjectAssignments>()
                .HasOne(pa => pa.AssignedUser)
                .WithMany()
                .HasForeignKey(pa => pa.AssignedUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // 11. ProjectAssignments -> AssignedByUser
            builder.Entity<ProjectAssignments>()
                .HasOne(pa => pa.AssignedByUser)
                .WithMany()
                .HasForeignKey(pa => pa.AssignedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // ---------- ProjectFile ----------
            // 12. ProjectFile -> Project
            builder.Entity<ProjectFile>()
                .HasOne(pf => pf.Project)
                .WithMany()
                .HasForeignKey(pf => pf.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            // 13. ProjectFile -> User (الذي رفع الملف)
            builder.Entity<ProjectFile>()
                .HasOne(pf => pf.User)
                .WithMany()
                .HasForeignKey(pf => pf.UploadedById)
                .OnDelete(DeleteBehavior.Restrict);

            // ---------- ProjectTransfers ----------
            // 14. ProjectTransfers -> Project
            builder.Entity<ProjectTransfers>()
                .HasOne(pt => pt.Project)
                .WithMany()
                .HasForeignKey(pt => pt.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            // 15. ProjectTransfers -> FromDepartment
            builder.Entity<ProjectTransfers>()
                .HasOne(pt => pt.FromDepartment)
                .WithMany()
                .HasForeignKey(pt => pt.FromDepartmentId)
                .OnDelete(DeleteBehavior.Restrict);

            // 16. ProjectTransfers -> ToDepartment
            builder.Entity<ProjectTransfers>()
                .HasOne(pt => pt.ToDepartment)
                .WithMany()
                .HasForeignKey(pt => pt.ToDepartmentId)
                .OnDelete(DeleteBehavior.Restrict);

            // 17. ProjectTransfers -> TransferredByUser
            builder.Entity<ProjectTransfers>()
                .HasOne(pt => pt.TransferredByUser)
                .WithMany()
                .HasForeignKey(pt => pt.TransferredById)
                .OnDelete(DeleteBehavior.Restrict);

            // 18. User -> UserToken
            builder.Entity<UserToken>()
                .HasOne(ut => ut.User)
                .WithMany()
                .HasForeignKey(ut => ut.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
