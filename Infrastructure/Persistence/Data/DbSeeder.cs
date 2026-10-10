using Application.Common;
using Domain.Entities;
using Domain.Entities.Maintenance;
using Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using BCrypt.Net;


namespace Infrastructure.Persistence.Data
{
    public static class DbSeeder
    {
        public static async Task SeedAsync(DataContext context, IConfiguration configuration, ILogger logger)
        {
            // ==================== 1. الأدوار ====================
            // الدور العام الوحيد هو SuperAdmin. كل منصب آخر ينشأ من صفحة الأدوار
            // ويرتبط صراحة بفرع أو قسم أو مكتب.
            var roleNames = new[] { "SuperAdmin" };
            foreach (var roleName in roleNames)
            {
                if (!await context.Roles.AnyAsync(r => r.Name == roleName))
                {
                    await context.Roles.AddAsync(new Role { Name = roleName });
                }
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
                    new MaintenanceRequestStatus { Name = "جديد", Color = "#3B82F6", Stage = MaintenanceStage.New },
                    new MaintenanceRequestStatus { Name = "قيد الصيانة", Color = "#F59E0B", Stage = MaintenanceStage.InProgress },
                    new MaintenanceRequestStatus { Name = "تم الإصلاح", Color = "#10B981", Stage = MaintenanceStage.Ready },
                    new MaintenanceRequestStatus { Name = "تم التسليم", Color = "#6B7280", Stage = MaintenanceStage.Delivered },
                    new MaintenanceRequestStatus { Name = "غير قابل للإصلاح", Color = "#EF4444", Stage = MaintenanceStage.NotRepairable }
                );
                await context.SaveChangesAsync();
            }

            // ==================== 5. الصلاحيات ====================
            // القائمة كلها في Application/Common/AppPermissions (المصدر الوحيد) — الـ seeder يضيف الناقص فقط
            var permissions = AppPermissions.All
                .Select(p => new Permission { Name = p.Name, Description = p.Description });

            foreach (var permission in permissions)
            {
                if (!await context.Permissions.AnyAsync(p => p.Name == permission.Name))
                    await context.Permissions.AddAsync(permission);
            }

            await context.SaveChangesAsync();

            // ==================== 6. أول مدير نظام ====================
            // لا كلمة مرور في الكود: يُنشأ فقط إن لم يوجد أي مستخدم بدور SuperAdmin، من الإعدادات Seed:AdminEmail / Seed:AdminPassword
            // (في الإنتاج متغيرا البيئة Seed__AdminEmail و Seed__AdminPassword لمرة واحدة، ثم يُحذفان — docs/DEPLOYMENT.md)
            var superAdminRole = await context.Roles.FirstAsync(r => r.Name == "SuperAdmin");
            if (!await context.Users.AnyAsync(u => u.RoleId == superAdminRole.Id))
            {
                var email = configuration["Seed:AdminEmail"]?.Trim();
                var password = configuration["Seed:AdminPassword"];
                if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                {
                    logger.LogWarning("لا يوجد مدير نظام. اضبط Seed:AdminEmail و Seed:AdminPassword (متغيرا البيئة Seed__AdminEmail و Seed__AdminPassword) ثم أعد التشغيل لإنشائه.");
                }
                else if (password.Length < 8)
                {
                    logger.LogWarning("لم يُنشأ مدير النظام: كلمة المرور في Seed:AdminPassword أقصر من 8 أحرف.");
                }
                else if (await context.Users.AnyAsync(u => u.Email == email))
                {
                    logger.LogWarning("لم يُنشأ مدير النظام: البريد {Email} مستخدم لحساب آخر.", email);
                }
                else
                {
                    await context.Users.AddAsync(new User
                    {
                        FullName = "Super Admin",
                        Email = email,
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                        RoleId = superAdminRole.Id,
                        // SuperAdmin لا يتبع لفرع أو قسم أو مكتب
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    });
                    await context.SaveChangesAsync();
                    logger.LogWarning("أُنشئ مدير النظام الأول ({Email}). احذف Seed:AdminPassword من الإعدادات وغيّر كلمة المرور بعد أول دخول.", email);
                }
            }

            // ==================== 7. ربط الصلاحيات بالأدوار ====================
            // السوبر ادمن يملك كل الصلاحيات دائماً (أي صلاحية جديدة في AppPermissions تصله تلقائياً)
            await EnsureRolePermissionsAsync(context, "SuperAdmin", AppPermissions.All.Select(p => p.Name));

            await context.SaveChangesAsync();
        }

        /// <summary>
        /// مواقع قديمة بلا محافظة (كانت تتبع "منطقة" قبل حذفها): تُحسب محافظتها من إحداثياتها مرة واحدة.
        /// من لا إحداثيات لها أو تقع خارج المحافظات تبقى فارغة وتُصحَّح عند تعديل الموقع.
        /// </summary>
        public static async Task BackfillSiteGovernoratesAsync(DataContext context)
        {
            var sites = await context.Sites
                .Where(s => s.GovernorateCode == "" && s.Latitude != null && s.Longitude != null)
                .ToListAsync();
            if (sites.Count == 0) return;

            foreach (var site in sites)
                site.GovernorateCode = Governorates.Locate(site.Latitude!.Value, site.Longitude!.Value)?.Code ?? string.Empty;

            await context.SaveChangesAsync();
        }

        /// <summary>
        /// كلمات سر الأجهزة المحفوظة نصاً قبل التشفير (2026-10-05): تُشفَّر مرة واحدة عند التشغيل.
        /// المشفّرة (enc:v1:) تُتخطّى، فالتشغيل المتكرر لا يغيّر شيئاً.
        /// </summary>
        public static async Task EncryptDevicePasswordsAsync(DataContext context, Application.Interfaces.IDevicePasswordProtector protector)
        {
            var plain = await context.DeviceSites.Where(ds => ds.Pass != "" && !ds.Pass.StartsWith("enc:v1:")).ToListAsync();
            if (plain.Count == 0) return;

            foreach (var installation in plain)
                installation.Pass = protector.Protect(installation.Pass);

            await context.SaveChangesAsync();
        }

        /// <summary>
        /// إجازات معتمدة قبل 2026-10-04 (قبل الأجزاء): يُنشأ لكل منها جزء لكل شهر بحالة دفعها القديمة
        /// وأيامها التقويمية كما حُسبت وقتها، كي يبقى الحد الشهري صحيحاً. مرة واحدة (من لا أجزاء له فقط).
        /// </summary>
        public static async Task BackfillVacationSegmentsAsync(DataContext context)
        {
            var legacy = await context.Vacation
                .Where(v => v.Status == VacationStatus.Approved && v.FinalApprovedAt == null && !v.Segments.Any())
                .ToListAsync();
            if (legacy.Count == 0) return;

            foreach (var v in legacy)
            {
                for (var from = v.StartVac.Date; from <= v.EndVac.Date; )
                {
                    var monthEnd = new DateTime(from.Year, from.Month, 1).AddMonths(1).AddDays(-1);
                    var to = monthEnd < v.EndVac.Date ? monthEnd : v.EndVac.Date;
                    v.Segments.Add(new VacationSegment
                    {
                        StartDate = from,
                        EndDate = to,
                        IsPaid = v.IsPaid,
                        Days = (to - from).Days + 1
                    });
                    from = to.AddDays(1);
                }

                var days = v.Segments.Sum(x => x.Days);
                v.PaidDays = v.IsPaid ? days : 0;
                v.UnpaidDays = v.IsPaid ? 0 : days;
            }

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
