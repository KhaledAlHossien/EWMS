using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Versioned_Signatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_UserSignatures",
                table: "UserSignatures");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "UserSignatures",
                newName: "CreatedAt");

            migrationBuilder.AddColumn<int>(
                name: "FinalApprovedSignatureId",
                table: "Vacation",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RejectedSignatureId",
                table: "Vacation",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Id",
                table: "UserSignatures",
                type: "int",
                nullable: false)
                .Annotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AddColumn<bool>(
                name: "IsCurrent",
                table: "UserSignatures",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // كان لكل مستخدم توقيع واحد: يصبح نسخته الحالية
            migrationBuilder.Sql("UPDATE UserSignatures SET IsCurrent = 1;");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserSignatures",
                table: "UserSignatures",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_Vacation_FinalApprovedSignatureId",
                table: "Vacation",
                column: "FinalApprovedSignatureId");

            migrationBuilder.CreateIndex(
                name: "IX_Vacation_RejectedSignatureId",
                table: "Vacation",
                column: "RejectedSignatureId");

            migrationBuilder.CreateIndex(
                name: "IX_UserSignatures_UserId",
                table: "UserSignatures",
                column: "UserId",
                unique: true,
                filter: "[IsCurrent] = 1");

            migrationBuilder.AddForeignKey(
                name: "FK_Vacation_UserSignatures_FinalApprovedSignatureId",
                table: "Vacation",
                column: "FinalApprovedSignatureId",
                principalTable: "UserSignatures",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Vacation_UserSignatures_RejectedSignatureId",
                table: "Vacation",
                column: "RejectedSignatureId",
                principalTable: "UserSignatures",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // صلاحية رفع التوقيع وتغييره (كانت مرتبطة بـ SignMaintenanceReceipt): تُمنح لمن يوقّع أوراق الصيانة
            // ولمن يملك قراراً على الإجازات (الاعتماد النهائي يُوقَّع، والرفض في أي مرحلة يُوقَّع)
            migrationBuilder.Sql(@"
INSERT INTO Permissions (Name, Description)
SELECT N'ManageMySignature', N'رفع توقيعه الإلكتروني وتغييره (يُطلب تأكيد كلمة المرور)'
WHERE NOT EXISTS (SELECT 1 FROM Permissions WHERE Name = N'ManageMySignature');

INSERT INTO RolePermissions (RoleId, PermissionId)
SELECT DISTINCT rp.RoleId, np.Id
FROM RolePermissions rp
INNER JOIN Permissions p ON p.Id = rp.PermissionId
CROSS JOIN (SELECT Id FROM Permissions WHERE Name = N'ManageMySignature') np
WHERE p.Name IN (N'SignMaintenanceReceipt', N'ApproveVacationFirst', N'ApproveVacationFinal')
  AND NOT EXISTS (SELECT 1 FROM RolePermissions x WHERE x.RoleId = rp.RoleId AND x.PermissionId = np.Id);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DELETE FROM Permissions WHERE Name = N'ManageMySignature';");

            migrationBuilder.DropForeignKey(
                name: "FK_Vacation_UserSignatures_FinalApprovedSignatureId",
                table: "Vacation");

            migrationBuilder.DropForeignKey(
                name: "FK_Vacation_UserSignatures_RejectedSignatureId",
                table: "Vacation");

            migrationBuilder.DropIndex(
                name: "IX_Vacation_FinalApprovedSignatureId",
                table: "Vacation");

            migrationBuilder.DropIndex(
                name: "IX_Vacation_RejectedSignatureId",
                table: "Vacation");

            // الرجوع إلى توقيع واحد لكل مستخدم: تبقى النسخة الحالية فقط
            migrationBuilder.Sql("DELETE FROM UserSignatures WHERE IsCurrent = 0;");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UserSignatures",
                table: "UserSignatures");

            migrationBuilder.DropIndex(
                name: "IX_UserSignatures_UserId",
                table: "UserSignatures");

            migrationBuilder.DropColumn(
                name: "FinalApprovedSignatureId",
                table: "Vacation");

            migrationBuilder.DropColumn(
                name: "RejectedSignatureId",
                table: "Vacation");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "UserSignatures");

            migrationBuilder.DropColumn(
                name: "IsCurrent",
                table: "UserSignatures");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "UserSignatures",
                newName: "UpdatedAt");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserSignatures",
                table: "UserSignatures",
                column: "UserId");
        }
    }
}
