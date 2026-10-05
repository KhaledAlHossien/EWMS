using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Add_Vacation_Attachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // مرفقات الإجازة (قرار المستخدم 2026-10-05). أُزيل منها إنشاء فهرسي DeliverySignatureId/DeliverySignerId:
            // موجودان في القاعدة من Maintenance_Delivery_Signature، لكن Designer الخاص بـ Add_DeviceMaintenance بُني بدونهما.
            migrationBuilder.CreateTable(
                name: "VacationAttachments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VacationId = table.Column<int>(type: "int", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Size = table.Column<int>(type: "int", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VacationAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VacationAttachments_Vacation_VacationId",
                        column: x => x.VacationId,
                        principalTable: "Vacation",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VacationAttachmentContents",
                columns: table => new
                {
                    AttachmentId = table.Column<int>(type: "int", nullable: false),
                    Data = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VacationAttachmentContents", x => x.AttachmentId);
                    table.ForeignKey(
                        name: "FK_VacationAttachmentContents_VacationAttachments_AttachmentId",
                        column: x => x.AttachmentId,
                        principalTable: "VacationAttachments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VacationAttachments_VacationId",
                table: "VacationAttachments",
                column: "VacationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VacationAttachmentContents");

            migrationBuilder.DropTable(
                name: "VacationAttachments");
        }
    }
}
