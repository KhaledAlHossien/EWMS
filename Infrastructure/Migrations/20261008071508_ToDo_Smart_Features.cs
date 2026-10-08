using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ToDo_Smart_Features : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Color",
                table: "ToDoLists",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Icon",
                table: "ToDoLists",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "ToDoLists",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsPinned",
                table: "ToDoLists",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DueDate",
                table: "ToDoItems",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DueSoonNotifiedAt",
                table: "ToDoItems",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsImportant",
                table: "ToDoItems",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastCompletedAt",
                table: "ToDoItems",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LinkedTaskId",
                table: "ToDoItems",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Note",
                table: "ToDoItems",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "OverdueNotifiedAt",
                table: "ToDoItems",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Repeat",
                table: "ToDoItems",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ToDoItems_IsDone_DueDate",
                table: "ToDoItems",
                columns: new[] { "IsDone", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ToDoItems_LinkedTaskId",
                table: "ToDoItems",
                column: "LinkedTaskId");

            migrationBuilder.AddForeignKey(
                name: "FK_ToDoItems_AssignedTasks_LinkedTaskId",
                table: "ToDoItems",
                column: "LinkedTaskId",
                principalTable: "AssignedTasks",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ToDoItems_AssignedTasks_LinkedTaskId",
                table: "ToDoItems");

            migrationBuilder.DropIndex(
                name: "IX_ToDoItems_IsDone_DueDate",
                table: "ToDoItems");

            migrationBuilder.DropIndex(
                name: "IX_ToDoItems_LinkedTaskId",
                table: "ToDoItems");

            migrationBuilder.DropColumn(
                name: "Color",
                table: "ToDoLists");

            migrationBuilder.DropColumn(
                name: "Icon",
                table: "ToDoLists");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "ToDoLists");

            migrationBuilder.DropColumn(
                name: "IsPinned",
                table: "ToDoLists");

            migrationBuilder.DropColumn(
                name: "DueDate",
                table: "ToDoItems");

            migrationBuilder.DropColumn(
                name: "DueSoonNotifiedAt",
                table: "ToDoItems");

            migrationBuilder.DropColumn(
                name: "IsImportant",
                table: "ToDoItems");

            migrationBuilder.DropColumn(
                name: "LastCompletedAt",
                table: "ToDoItems");

            migrationBuilder.DropColumn(
                name: "LinkedTaskId",
                table: "ToDoItems");

            migrationBuilder.DropColumn(
                name: "Note",
                table: "ToDoItems");

            migrationBuilder.DropColumn(
                name: "OverdueNotifiedAt",
                table: "ToDoItems");

            migrationBuilder.DropColumn(
                name: "Repeat",
                table: "ToDoItems");
        }
    }
}
