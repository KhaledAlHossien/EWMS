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
            var roleNames = new[] { "SuperAdmin", "BranchManager", "Admin", "Manager", "ACC" };
            foreach (var roleName in roleNames)
            {
                if (!await context.Roles.AnyAsync(r => r.Name == roleName))
                    await context.Roles.AddAsync(new Role { Name = roleName });
            }

            await context.SaveChangesAsync();

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
            var permissions = new[]
            {
                new Permission { Name = "ViewProjects", Description = "عرض المشاريع" },
                new Permission { Name = "CreateProject", Description = "إنشاء مشروع" },
                new Permission { Name = "EditProject", Description = "تعديل مشروع" },
                new Permission { Name = "DeleteProject", Description = "حذف مشروع" },
                new Permission { Name = "AssignUser", Description = "تعيين مستخدم" },
                new Permission { Name = "TransferProject", Description = "نقل مشروع" },
                new Permission { Name = "UploadProjectFile", Description = "رفع ملفات المشروع" },
                new Permission { Name = "ManageUsers", Description = "إدارة المستخدمين" },
                new Permission { Name = "ManageBranches", Description = "إدارة الفروع" },
                new Permission { Name = "ManageDepartments", Description = "إدارة الأقسام" },
                new Permission { Name = "ManageRoles", Description = "إدارة الأدوار والصلاحيات" }
            };

            foreach (var permission in permissions)
            {
                if (!await context.Permissions.AnyAsync(p => p.Name == permission.Name))
                    await context.Permissions.AddAsync(permission);
            }

            await context.SaveChangesAsync();

            // ==================== 5. المستخدم SuperAdmin ====================
            if (!await context.Users.AnyAsync(u => u.Email == "admin@system.com"))
            {
                var adminRole = await context.Roles.FirstAsync(r => r.Name == "SuperAdmin");
                var adminDept = await context.Departments.FirstAsync(d => d.Name == "الإدارة العامة");
                var mainBranch = await context.Branches.FirstAsync(b => b.Name == "الفرع الرئيسي");

                var admin = new User
                {
                    FullName = "Super Admin",
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

            // ==================== 6. ربط الصلاحيات بالأدوار ====================
            var allPermissions = await context.Permissions.ToListAsync();

            await EnsureRolePermissionsAsync(context, "SuperAdmin", allPermissions.Select(p => p.Name));
            await EnsureRolePermissionsAsync(context, "Admin", allPermissions.Select(p => p.Name));

            var managerPermissionNames = new[]
            {
                "ViewProjects",
                "CreateProject",
                "EditProject",
                "AssignUser",
                "TransferProject",
                "UploadProjectFile",
                "ManageUsers",
                "ManageDepartments",
                "ManageRoles"
            };

            await EnsureRolePermissionsAsync(context, "Manager", managerPermissionNames);
            await EnsureRolePermissionsAsync(context, "BranchManager", managerPermissionNames);

            await EnsureRolePermissionsAsync(context, "ACC", new[] { "ViewProjects" });

            await context.SaveChangesAsync();
        }

        private static async Task EnsureRolePermissionsAsync(
            DataContext context,
            string roleName,
            IEnumerable<string> permissionNames)
        {
            var role = await context.Roles.FirstAsync(r => r.Name == roleName);
            var permissions = await context.Permissions
                .Where(p => permissionNames.Contains(p.Name))
                .ToListAsync();

            foreach (var permission in permissions)
            {
                if (!await context.RolePermissions.AnyAsync(rp =>
                        rp.RoleId == role.Id && rp.PermissionId == permission.Id))
                {
                    await context.RolePermissions.AddAsync(new RolePermission
                    {
                        RoleId = role.Id,
                        PermissionId = permission.Id
                    });
                }
            }
        }
    }
}
