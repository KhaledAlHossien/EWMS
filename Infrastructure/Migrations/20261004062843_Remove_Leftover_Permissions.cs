using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Remove_Leftover_Permissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // صلاحيات لم تعد في AppPermissions ولا يفحصها أي كود، بقيت في قاعدة البيانات (طلب المستخدم 2026-10-04):
            // ApproveVacation (استُبدلت بـ ApproveVacationFirst/Final) وصلاحيات تجربة "إسناد الخدمات" من فرعي test/yaman.
            // تُعاد إن شُغّل الباك من أحد تلك الفروع لأن الـ seeder هناك يضيفها. RolePermissions تُحذف معها (Cascade).
            migrationBuilder.Sql(@"
DELETE FROM Permissions
WHERE Name IN (N'ApproveVacation', N'ViewModuleAssignments', N'EditModuleAssignments',
               N'ViewAssignedTasks', N'CreateAssignedTask')
   OR Name LIKE N'Module.%';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // لا استرجاع: هذه الصلاحيات لا تعني شيئاً في الكود الحالي
        }
    }
}
