using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <summary>
    /// أفرع الدراسات ومسارها (قرار المستخدم 2026-11-28): جدول StudyBranches تحت فرع الدراسات في النظام، وفرع دراسة إلزامي للطلب،
    /// ومشروع يحمل طلبه مباشرة (RequestId) وأمر بدئه اختياري (مشروع الفرع الجغرافي بلا أمر بدء)، ومسار لأنواع المراحل.
    /// كُتبت يدوياً بالترتيب الذي يحفظ البيانات القائمة:
    /// 1) لكل فرع في النظام له طلبات دراسة تُنشأ الأفرع الستة (المركزية 0 بالدورة الكاملة، والجغرافية 1–5 بالتنسيق)،
    /// 2) كل الطلبات القائمة تصبح «المركزية»، 3) RequestId للمشاريع القائمة من سلسلة أمر البدء ← الرد ← التكليف،
    /// 4) أنواع المراحل القائمة مسارها الدورة الكاملة، 5) الأكواد المولّدة بـ «رقم الفرع في النظام-» تصبح «0-» (رمز المركزية).
    /// على قاعدة بلا طلبات لا تُنشأ أفرع دراسات: تُضاف من قوائم الدراسات.
    /// </summary>
    public partial class Study_Branches_Regional_Track : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1) جدول أفرع الدراسات وبياناته الأولى ──
            migrationBuilder.CreateTable(
                name: "StudyBranches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Track = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudyBranches", x => x.Id);
                    table.CheckConstraint("CK_StudyBranches_Code", "[Code] >= 0 AND [Code] <= 99");
                    table.ForeignKey(
                        name: "FK_StudyBranches_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StudyBranches_BranchId_Code",
                table: "StudyBranches",
                columns: new[] { "BranchId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudyBranches_BranchId_Name",
                table: "StudyBranches",
                columns: new[] { "BranchId", "Name" },
                unique: true);

            migrationBuilder.Sql("""
                INSERT INTO [StudyBranches] ([BranchId], [Code], [Name], [Track], [IsActive])
                SELECT b.[BranchId], v.[Code], v.[Name], v.[Track], 1
                FROM (SELECT DISTINCT [BranchId] FROM [StudyRequests]) AS b
                CROSS JOIN (VALUES
                    (0, N'المركزية', 1),
                    (1, N'الشمالية', 2),
                    (2, N'الجنوبية', 2),
                    (3, N'الوسطى', 2),
                    (4, N'الغربية', 2),
                    (5, N'الشرقية', 2)) AS v([Code], [Name], [Track]);
                """);

            // ── 2) فرع الدراسة للطلب: القائمة كلها للمركزية ──
            migrationBuilder.AddColumn<int>(
                name: "StudyBranchId",
                table: "StudyRequests",
                type: "int",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE r SET r.[StudyBranchId] = s.[Id]
                FROM [StudyRequests] r
                JOIN [StudyBranches] s ON s.[BranchId] = r.[BranchId] AND s.[Code] = 0;
                """);

            migrationBuilder.AlterColumn<int>(
                name: "StudyBranchId",
                table: "StudyRequests",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudyRequests_StudyBranchId",
                table: "StudyRequests",
                column: "StudyBranchId");

            migrationBuilder.AddForeignKey(
                name: "FK_StudyRequests_StudyBranches_StudyBranchId",
                table: "StudyRequests",
                column: "StudyBranchId",
                principalTable: "StudyBranches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // ── 3) المشروع يحمل طلبه: من سلسلة أمر البدء للمشاريع القائمة ──
            migrationBuilder.AddColumn<int>(
                name: "RequestId",
                table: "StudyProjects",
                type: "int",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE p SET p.[RequestId] = a.[RequestId]
                FROM [StudyProjects] p
                JOIN [StudyStartOrders] o ON o.[Id] = p.[StartOrderId]
                JOIN [StudyAssignmentResponses] r ON r.[Id] = o.[ResponseId]
                JOIN [StudyAssignments] a ON a.[Id] = r.[AssignmentId];
                """);

            migrationBuilder.AlterColumn<int>(
                name: "RequestId",
                table: "StudyProjects",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudyProjects_RequestId",
                table: "StudyProjects",
                column: "RequestId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_StudyProjects_StudyRequests_RequestId",
                table: "StudyProjects",
                column: "RequestId",
                principalTable: "StudyRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // أمر البدء اختياري (مشروع الفرع الجغرافي بلا أمر بدء) وفريد حين يوجد
            migrationBuilder.DropForeignKey(
                name: "FK_StudyProjects_StudyStartOrders_StartOrderId",
                table: "StudyProjects");

            migrationBuilder.DropIndex(
                name: "IX_StudyProjects_StartOrderId",
                table: "StudyProjects");

            migrationBuilder.AlterColumn<int>(
                name: "StartOrderId",
                table: "StudyProjects",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.CreateIndex(
                name: "IX_StudyProjects_StartOrderId",
                table: "StudyProjects",
                column: "StartOrderId",
                unique: true,
                filter: "[StartOrderId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_StudyProjects_StudyStartOrders_StartOrderId",
                table: "StudyProjects",
                column: "StartOrderId",
                principalTable: "StudyStartOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // ── 4) مسار أنواع المراحل: القائمة للدورة الكاملة (1) ──
            migrationBuilder.AddColumn<int>(
                name: "Track",
                table: "StudyStageTypes",
                type: "int",
                nullable: false,
                defaultValue: 1);

            // ── 5) كود المشروع يبدأ برمز فرع الدراسة: «رقم الفرع في النظام-yy-…» ← «0-yy-…» (كل الطلبات القائمة للمركزية) ──
            migrationBuilder.Sql("""
                UPDATE a SET a.[ProjectCode] = N'0' + SUBSTRING(a.[ProjectCode], LEN(CAST(r.[BranchId] AS nvarchar(10))) + 1, 200)
                FROM [StudyAssignments] a
                JOIN [StudyRequests] r ON r.[Id] = a.[RequestId]
                WHERE a.[ProjectCode] LIKE CAST(r.[BranchId] AS nvarchar(10)) + N'-[0-9][0-9]-%';

                UPDATE p SET p.[Code] = N'0' + SUBSTRING(p.[Code], LEN(CAST(p.[BranchId] AS nvarchar(10))) + 1, 200)
                FROM [StudyProjects] p
                WHERE p.[Code] LIKE CAST(p.[BranchId] AS nvarchar(10)) + N'-[0-9][0-9]-%';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // مشروع فرع جغرافي بلا أمر بدء لا مكان له في البنية القديمة: يُلغى التراجع بدل حذف بيانات
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [StudyProjects] WHERE [StartOrderId] IS NULL)
                    THROW 50000, N'توجد مشاريع أفرع جغرافية بلا أمر بدء — احذفها يدوياً قبل التراجع عن هذا الترحيل', 1;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_StudyProjects_StudyRequests_RequestId",
                table: "StudyProjects");

            migrationBuilder.DropForeignKey(
                name: "FK_StudyRequests_StudyBranches_StudyBranchId",
                table: "StudyRequests");

            migrationBuilder.DropIndex(
                name: "IX_StudyRequests_StudyBranchId",
                table: "StudyRequests");

            migrationBuilder.DropIndex(
                name: "IX_StudyProjects_RequestId",
                table: "StudyProjects");

            migrationBuilder.DropColumn(
                name: "StudyBranchId",
                table: "StudyRequests");

            migrationBuilder.DropColumn(
                name: "RequestId",
                table: "StudyProjects");

            migrationBuilder.DropColumn(
                name: "Track",
                table: "StudyStageTypes");

            migrationBuilder.DropTable(
                name: "StudyBranches");

            migrationBuilder.DropForeignKey(
                name: "FK_StudyProjects_StudyStartOrders_StartOrderId",
                table: "StudyProjects");

            migrationBuilder.DropIndex(
                name: "IX_StudyProjects_StartOrderId",
                table: "StudyProjects");

            migrationBuilder.AlterColumn<int>(
                name: "StartOrderId",
                table: "StudyProjects",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudyProjects_StartOrderId",
                table: "StudyProjects",
                column: "StartOrderId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_StudyProjects_StudyStartOrders_StartOrderId",
                table: "StudyProjects",
                column: "StartOrderId",
                principalTable: "StudyStartOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // الأكواد لا تُعاد إلى «رقم الفرع في النظام-»: الرمز «0-» هو الصحيح لطلبات المركزية
        }
    }
}
