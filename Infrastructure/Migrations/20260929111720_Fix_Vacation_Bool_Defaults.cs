using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <summary>
    /// حذف HasDefaultValue(true) عن Vacation.IsPaid و BranchManagerAccept: EF كان يحذف القيمة false من INSERT
    /// فتكتب قاعدة البيانات true — الإجازة غير المدفوعة (أو الجزء غير المدفوع من طلب مقسوم) كانت تُحفظ مدفوعة.
    /// </summary>
    public partial class Fix_Vacation_Bool_Defaults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<bool>(
                name: "IsPaid",
                table: "Vacation",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: true);

            migrationBuilder.AlterColumn<bool>(
                name: "BranchManagerAccept",
                table: "Vacation",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: true);

            // تصحيح البيانات المحفوظة خطأً (ما يمكن استنتاجه بثقة فقط):
            // 1) إجازة من نوع غير مدفوع هي غير مدفوعة دائماً (القاعدة في CreateVacationCommandHandler)
            migrationBuilder.Sql(@"
                UPDATE v SET v.[IsPaid] = 0
                FROM [Vacation] v INNER JOIN [VacationType] t ON t.[Id] = v.[VacationTypeId]
                WHERE t.[IsPaid] = 0 AND v.[IsPaid] = 1;");

            // 2) موافقة رئيس الفرع تُسجَّل فقط عند الاعتماد النهائي (Approved = 4)
            migrationBuilder.Sql("UPDATE [Vacation] SET [BranchManagerAccept] = 0 WHERE [Status] <> 4 AND [BranchManagerAccept] = 1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<bool>(
                name: "IsPaid",
                table: "Vacation",
                type: "bit",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<bool>(
                name: "BranchManagerAccept",
                table: "Vacation",
                type: "bit",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "bit");
        }
    }
}
