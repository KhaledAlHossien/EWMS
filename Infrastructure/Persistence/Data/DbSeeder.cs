using Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.EntityFrameworkCore;
using BCrypt.Net;


namespace Infrastructure.Persistence.Data
{
    public static class DbSeeder
    {
        public static async Task SeedAsync(DataContext context)
        {
            // ==================== 1. الأدوار ====================
            if (!await context.Roles.AnyAsync())
            {
                await context.Roles.AddRangeAsync(
                    new Role { Name = "Admin" },
                    new Role { Name = "Manager" },
                    new Role { Name = "ACC" }
                );
                await context.SaveChangesAsync();
            }

            // ==================== 2. الفروع ====================
            if (!await context.Branches.AnyAsync())
            {
                await context.Branches.AddRangeAsync(
                    new Branch { Name = "الفرع الرئيسي", Description = "المركز الرئيسي للشركة" },
                    new Branch { Name = "فرع دمشق", Description = "فرع العاصمة" }
                );
                await context.SaveChangesAsync();
            }

            // ==================== 3. الأقسام ====================
            if (!await context.Departments.AnyAsync())
            {
                var mainBranch = await context.Branches.FirstAsync(b => b.Name == "الفرع الرئيسي");

                await context.Departments.AddRangeAsync(
                    new Department { Name = "الإدارة العامة", Description = "القسم الرئيسي", BranchId = mainBranch.Id },
                    new Department { Name = "قسم المشاريع", Description = "إدارة المشاريع", BranchId = mainBranch.Id },
                    new Department { Name = "قسم المالية", Description = "الشؤون المالية", BranchId = mainBranch.Id }
                );
                await context.SaveChangesAsync();
            }

            // ==================== 4. الصلاحيات ====================
            if (!await context.Permissions.AnyAsync())
            {
                await context.Permissions.AddRangeAsync(
                    new Permission { Name = "ViewProjects", Description = "عرض المشاريع" },
                    new Permission { Name = "CreateProject", Description = "إنشاء مشروع" },
                    new Permission { Name = "EditProject", Description = "تعديل مشروع" },
                    new Permission { Name = "DeleteProject", Description = "حذف مشروع" },
                    new Permission { Name = "AssignUser", Description = "تعيين مستخدم" },
                    new Permission { Name = "TransferProject", Description = "نقل مشروع" }
                );
                await context.SaveChangesAsync();
            }

            // ==================== 5. المستخدم Admin ====================
            if (!await context.Users.AnyAsync(u => u.Email == "admin@system.com"))
            {
                var adminRole = await context.Roles.FirstAsync(r => r.Name == "Admin");
                var adminDept = await context.Departments.FirstAsync(d => d.Name == "الإدارة العامة");
                var mainBranch = await context.Branches.FirstAsync(b => b.Name == "الفرع الرئيسي");

                var admin = new User
                {
                    FullName = "Admin",
                    Email = "admin@system.com",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("it@123456"),
                    RoleId = adminRole.Id,
                    DepartmentId = adminDept.Id,
                    BranchId = mainBranch.Id,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                await context.Users.AddAsync(admin);
                await context.SaveChangesAsync();
            }

            // ==================== 6. ربط الصلاحيات بدور Admin ====================
            var adminRoleEntity = await context.Roles.FirstAsync(r => r.Name == "Admin");

            if (!await context.RolePermissions.AnyAsync(rp => rp.RoleId == adminRoleEntity.Id))
            {
                var allPermissions = await context.Permissions.ToListAsync();

                var rolePermissions = allPermissions.Select(p => new RolePermission
                {
                    RoleId = adminRoleEntity.Id,
                    PermissionId = p.Id
                });

                await context.RolePermissions.AddRangeAsync(rolePermissions);
                await context.SaveChangesAsync();
            }
        }
    }
}
