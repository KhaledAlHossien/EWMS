using Domain.Entities;
using Domain.Entities.Maintenance;
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
            var roleNames = new[] { "SuperAdmin", "BranchManager", "Manager", "OfficeManager", "Emp" };
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
                    new Branch { Name = "الفرع التقني", Description = "الفرع التقني الخاص بالادارة" },
                    new Branch { Name = "فرع التصميم", Description = "الفرع التصميمي الخاص بالادارة" },
                    new Branch { Name = "فرع التقييم", Description = "الفرع التقييمي الخاص بالادارة" },
                    new Branch { Name = "فرع الدراسات", Description = "الفرع الدراسي الخاص بالادارة" }
                );
                await context.SaveChangesAsync();
            }

            // ==================== 3. الأقسام ====================
            if (!await context.Departments.AnyAsync())
            {
                var mainBranch = await context.Branches.FirstAsync(b => b.Name == "الفرع التقني");

                await context.Departments.AddRangeAsync(
                    new Department { Name = "العمليات", Description = "القسم الخاص بالعمليات", BranchId = mainBranch.Id },
                    new Department { Name = "التنفيذ", Description = "القسم الخاص بالتنفيذ", BranchId = mainBranch.Id }
                );
                await context.SaveChangesAsync();
            }

            // ==================== 3.1 المكاتب ====================
            {
                var operationsDept = await context.Departments.FirstAsync(d => d.Name == "العمليات");

                var officeNames = new[]
                {
                    new Office { Name = "مكتب البرمجة", Description = "مكتب البرمجة التابع لقسم العمليات", DepartmentId = operationsDept.Id },
                    new Office { Name = "مكتب السيرفر", Description = "مكتب السيرفر التابع لقسم العمليات", DepartmentId = operationsDept.Id }
                };

                foreach (var office in officeNames)
                {
                    if (!await context.Offices.AnyAsync(o => o.Name == office.Name))
                        await context.Offices.AddAsync(office);
                }

                await context.SaveChangesAsync();
            }

            // ==================== 4. أنواع الإجازات ====================
            if (!await context.VacationType.AnyAsync())
            {
                await context.VacationType.AddRangeAsync(
                    new VacationType
                    {
                        Name = "إجازة مدفوعة الأجر",
                        Description = "إجازة مدفوعة الأجر، يحق للموظف إجازتين كل شهر",
                        IsPaid = true   // ✅ يخضع للحد الشهري
                    },
                    new VacationType
                    {
                        Name = "إجازة غير مدفوعة الأجر",
                        Description = "إجازة غير مدفوعة الأجر يتم خصمها على الموظف",
                        IsPaid = false  // ❌ غير مدفوعة دائماً
                    },
                    new VacationType
                    {
                        Name = "إجازة مرضية",
                        Description = "إجازة تمنح للموظف في الحالات المرضية",
                        IsPaid = true   // ✅ مدفوعة (حسب الحد)
                    },
                    new VacationType
                    {
                        Name = "إجازة مكافأة عمل إضافي",
                        Description = "إجازة تمنح للموظف عند العمل الإضافي",
                        IsPaid = true   // ✅ مدفوعة
                    }
                );
                await context.SaveChangesAsync();
            }

            // ==================== 4.1 حالات طلب الصيانة (مرة واحدة فقط — بعدها تُدار من الواجهة) ====================
            if (!await context.MaintenanceRequestStatuses.AnyAsync())
            {
                await context.MaintenanceRequestStatuses.AddRangeAsync(
                    new MaintenanceRequestStatus { Name = "جديد", Color = "#3B82F6" },
                    new MaintenanceRequestStatus { Name = "قيد الصيانة", Color = "#F59E0B" },
                    new MaintenanceRequestStatus { Name = "تم الإصلاح", Color = "#10B981" },
                    new MaintenanceRequestStatus { Name = "تم التسليم", Color = "#6B7280" },
                    new MaintenanceRequestStatus { Name = "غير قابل للإصلاح", Color = "#EF4444" }
                );
                await context.SaveChangesAsync();
            }

            // ==================== 5. الصلاحيات ====================
            var permissions = new[]
            {
                new Permission { Name = "ManageUsers", Description = "إدارة المستخدمين" },
                new Permission { Name = "ManageBranches", Description = "إدارة الفروع" },
                new Permission { Name = "ManageDepartments", Description = "إدارة الأقسام" },
                new Permission { Name = "ManageOffices", Description = "إدارة المكاتب" },
                new Permission { Name = "ManageRoles", Description = "إدارة الأدوار والصلاحيات" },

                new Permission { Name = "ViewVacations",        Description = "عرض الإجازات" },
                new Permission { Name = "CreateVacation",       Description = "تقديم طلب إجازة" },
                new Permission { Name = "ApproveVacation",      Description = "الموافقة على الإجازات" },
                new Permission { Name = "ManageVacations",      Description = "إدارة كل الإجازات" },
                new Permission { Name = "ManageVacationTypes",  Description = "إدارة أنواع الإجازات" },

                new Permission { Name = "ViewDevices",   Description = "عرض المناطق والمواقع والأجهزة" },
                new Permission { Name = "ManageDevices", Description = "إدارة المناطق والمواقع والأجهزة" },
                new Permission { Name = "ManageWorkTasks",      Description = "إدارة مهام العمل وإسنادها" },

                // الصيانة: صلاحية منفصلة لكل عملية — تُمنح من صفحة الأدوار (السوبر ادمن يملكها كلها تلقائياً)
                // أي تعديل هنا يجب أن يطابق migration: Split_Maintenance_Permissions
                new Permission { Name = "ViewMaintenanceTasks",   Description = "عرض مهام الصيانة" },
                new Permission { Name = "CreateMaintenanceTask",  Description = "إضافة مهمة صيانة" },
                new Permission { Name = "EditMaintenanceTask",    Description = "تعديل مهمة صيانة" },
                new Permission { Name = "DeleteMaintenanceTask",  Description = "حذف مهمة صيانة" },

                new Permission { Name = "ViewMaintenanceRequests",  Description = "عرض طلبات الصيانة والبحث فيها" },
                new Permission { Name = "CreateMaintenanceRequest", Description = "تقديم طلب صيانة" },
                new Permission { Name = "EditMaintenanceRequest",   Description = "تعديل طلب صيانة" },
                new Permission { Name = "DeleteMaintenanceRequest", Description = "حذف طلب صيانة" },

                new Permission { Name = "CreateMaintenanceLookup", Description = "إضافة أنواع الأجهزة والشركات والأعطال وحالات الطلب" },
                new Permission { Name = "EditMaintenanceLookup",   Description = "تعديل أنواع الأجهزة والشركات والأعطال وحالات الطلب" },
                new Permission { Name = "DeleteMaintenanceLookup", Description = "حذف أنواع الأجهزة والشركات والأعطال وحالات الطلب" },
            };

            foreach (var permission in permissions)
            {
                if (!await context.Permissions.AnyAsync(p => p.Name == permission.Name))
                    await context.Permissions.AddAsync(permission);
            }

            await context.SaveChangesAsync();

            // ==================== 6. المستخدم SuperAdmin ====================
            if (!await context.Users.AnyAsync(u => u.Email == "admin@system.com"))
            {
                var adminRole = await context.Roles.FirstAsync(r => r.Name == "SuperAdmin");

                var admin = new User
                {
                    FullName = "Super Admin",
                    Email = "admin@system.com",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("it@123456"),
                    RoleId = adminRole.Id,
                    // SuperAdmin لا يتبع لفرع أو قسم أو مكتب
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                await context.Users.AddAsync(admin);
                await context.SaveChangesAsync();
            }

            // ==================== 7. ربط الصلاحيات بالأدوار ====================
            var allPermissions = await context.Permissions.ToListAsync();

            await EnsureRolePermissionsAsync(context, "SuperAdmin", allPermissions.Select(p => p.Name));

            // الرؤساء لا يديرون الهيكل (فروع/أقسام/مكاتب/موظفين/أدوار/أنواع إجازات) — هذه للسوبر ادمن فقط،
            // ولاحقاً لدور "موظف إداري" يُنشئه السوبر ادمن من صفحة الأدوار (قرار المستخدم 2026-09-28).
            // الصلاحيات التي سُحبت من الرؤساء أزالتها migration: Restrict_Management_To_SuperAdmin

            // رئيس الفرع: يعتمد الإجازات (المرحلة الثانية) ولا يقدّم إجازات
            await EnsureRolePermissionsAsync(context, "BranchManager", new[]
            {
                "ViewVacations",
                "ApproveVacation"
            });

            // رئيس القسم: يوافق على إجازات قسمه (المرحلة الأولى) ويقدّم إجازاته
            // ⚠ الـ seeder يضيف فقط مع كل تشغيل: أي صلاحية تبقى هنا تعود لقاعدة البيانات حتى لو حذفتها migration.
            // ManageVacations / ManageVacationTypes للسوبر ادمن فقط (أُعيدتا خطأً عند حل تعارض دمج — أزالتهما Remove_Manager_Vacation_Management)
            await EnsureRolePermissionsAsync(context, "Manager", new[]
            {
                "ViewVacations",
                "CreateVacation",
                "ApproveVacation"
            });

            // رئيس المكتب: يطّلع على إجازات مكتبه فقط، لا يوافق عليها
            await EnsureRolePermissionsAsync(context, "OfficeManager", new[]
            {
                "ViewVacations",
                "CreateVacation"
            });

            // الموظف العادي: يقدّم طلب إجازة ويرى إجازاته فقط (لا موافقة ولا إدارة)
            var empPermissionNames = new[]
            {
                "ViewVacations",
                "CreateVacation"
            };

            await EnsureRolePermissionsAsync(context, "Emp", empPermissionNames);

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