using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Study_Project_Site : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SiteId",
                table: "StudyProjects",
                type: "int",
                nullable: true);

            // المشاريع الموجودة تأخذ موقع طلبها عبر السلسلة أمر البدء ← الرد ← التكليف ← الطلب
            migrationBuilder.Sql(@"
                UPDATE p SET p.[SiteId] = r.[SiteId]
                FROM [StudyProjects] p
                JOIN [StudyStartOrders] o ON o.[Id] = p.[StartOrderId]
                JOIN [StudyAssignmentResponses] a ON a.[Id] = o.[ResponseId]
                JOIN [StudyAssignments] i ON i.[Id] = a.[AssignmentId]
                JOIN [StudyRequests] r ON r.[Id] = i.[RequestId]
                WHERE r.[SiteId] IS NOT NULL;");

            migrationBuilder.CreateIndex(
                name: "IX_StudyProjects_BranchId_SiteId",
                table: "StudyProjects",
                columns: new[] { "BranchId", "SiteId" });

            migrationBuilder.CreateIndex(
                name: "IX_StudyProjects_SiteId",
                table: "StudyProjects",
                column: "SiteId");

            migrationBuilder.AddForeignKey(
                name: "FK_StudyProjects_Sites_SiteId",
                table: "StudyProjects",
                column: "SiteId",
                principalTable: "Sites",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StudyProjects_Sites_SiteId",
                table: "StudyProjects");

            migrationBuilder.DropIndex(
                name: "IX_StudyProjects_BranchId_SiteId",
                table: "StudyProjects");

            migrationBuilder.DropIndex(
                name: "IX_StudyProjects_SiteId",
                table: "StudyProjects");

            migrationBuilder.DropColumn(
                name: "SiteId",
                table: "StudyProjects");
        }
    }
}
