using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Study_Project_View_Scopes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // حدّ رؤية مشاريع الدراسات بالقسم والمكتب (بيانات فقط، بلا تغيير في الجداول): ثلاث صلاحيات جديدة (الأسماء والأوصاف تطابق AppPermissions).
            // ViewFullStudyProject تُمنح للأدوار التي كانت ترى المشروع كاملاً بحكم عملها حتى لا يفقد أحد رؤيته:
            // من يملك إنشاء المراحل أو تعديل المشروع أو إنشاءه، أو التوقيع بصفة رئيس الفرع أو إدارة المشاريع.
            // الصلاحيتان الأخريان تُمنحان من صفحة الأدوار؛ ومدير النظام يأخذ الثلاث من DbSeeder.
            migrationBuilder.Sql(@"
DECLARE @p TABLE (Name nvarchar(100), Description nvarchar(300));
INSERT INTO @p VALUES
 (N'ViewFullStudyProject', N'رؤية مشاريع فرعه كاملة: كل المراحل والمهام والمخطط الزمني والفريق والمرفقات والتواقيع وسجل النشاط (بلاها يرى ما يخص قسمه ومكتبه فقط؛ ومدير المشروع يرى مشروعه كاملاً)'),
 (N'ViewDepartmentStudyTasks', N'رؤية كل مهام قسمه في المشاريع: مراحل قسمه والمهام المسندة لقسمه أو مكاتبه أو موظفيه، وأعضاء فريق قسمه'),
 (N'ViewOfficeStudyTasks', N'رؤية مهام مكتبه وموظفيه في المشاريع، وأعضاء فريق مكتبه');

INSERT INTO Permissions (Name, Description)
SELECT p.Name, p.Description FROM @p p
WHERE NOT EXISTS (SELECT 1 FROM Permissions x WHERE x.Name = p.Name);

DECLARE @map TABLE (Source nvarchar(100), Target nvarchar(100));
INSERT INTO @map VALUES
 (N'CreateStudyStage', N'ViewFullStudyProject'), (N'EditStudyProject', N'ViewFullStudyProject'),
 (N'CreateStudyProject', N'ViewFullStudyProject'), (N'SignStudyAsBranchHead', N'ViewFullStudyProject'),
 (N'SignStudyAsProjectManagement', N'ViewFullStudyProject');

INSERT INTO RolePermissions (RoleId, PermissionId)
SELECT DISTINCT rp.RoleId, t.Id
FROM RolePermissions rp
INNER JOIN Permissions s ON s.Id = rp.PermissionId
INNER JOIN @map m ON m.Source = s.Name
INNER JOIN Permissions t ON t.Name = m.Target
WHERE NOT EXISTS (SELECT 1 FROM RolePermissions x WHERE x.RoleId = rp.RoleId AND x.PermissionId = t.Id);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // منح الأدوار يُحذف معها (Cascade)؛ ويعود المشروع كاملاً لكل من يملك ViewStudyProjects بعد إرجاع الكود
            migrationBuilder.Sql(@"DELETE FROM Permissions WHERE Name IN (N'ViewFullStudyProject', N'ViewDepartmentStudyTasks', N'ViewOfficeStudyTasks');");
        }
    }
}
