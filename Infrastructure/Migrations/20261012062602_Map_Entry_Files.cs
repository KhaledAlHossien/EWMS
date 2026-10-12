using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Map_Entry_Files : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FileKinds",
                table: "MapLayerFields",
                type: "nvarchar(400)",
                maxLength: 400,
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<int>(
                name: "MaxFileMegabytes",
                table: "MapLayerFields",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MaxFiles",
                table: "MapLayerFields",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "MapEntryFiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntryId = table.Column<int>(type: "int", nullable: true),
                    FieldId = table.Column<int>(type: "int", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    UploadedByUserId = table.Column<int>(type: "int", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MapEntryFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MapEntryFiles_MapEntries_EntryId",
                        column: x => x.EntryId,
                        principalTable: "MapEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MapEntryFiles_MapLayerFields_FieldId",
                        column: x => x.FieldId,
                        principalTable: "MapLayerFields",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MapEntryFiles_Users_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MapEntryFileContents",
                columns: table => new
                {
                    FileId = table.Column<int>(type: "int", nullable: false),
                    Data = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MapEntryFileContents", x => x.FileId);
                    table.ForeignKey(
                        name: "FK_MapEntryFileContents_MapEntryFiles_FileId",
                        column: x => x.FileId,
                        principalTable: "MapEntryFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MapEntryFiles_EntryId_FieldId",
                table: "MapEntryFiles",
                columns: new[] { "EntryId", "FieldId" });

            migrationBuilder.CreateIndex(
                name: "IX_MapEntryFiles_FieldId",
                table: "MapEntryFiles",
                column: "FieldId");

            migrationBuilder.CreateIndex(
                name: "IX_MapEntryFiles_UploadedAt",
                table: "MapEntryFiles",
                column: "UploadedAt");

            migrationBuilder.CreateIndex(
                name: "IX_MapEntryFiles_UploadedByUserId",
                table: "MapEntryFiles",
                column: "UploadedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MapEntryFileContents");

            migrationBuilder.DropTable(
                name: "MapEntryFiles");

            migrationBuilder.DropColumn(
                name: "FileKinds",
                table: "MapLayerFields");

            migrationBuilder.DropColumn(
                name: "MaxFileMegabytes",
                table: "MapLayerFields");

            migrationBuilder.DropColumn(
                name: "MaxFiles",
                table: "MapLayerFields");
        }
    }
}
