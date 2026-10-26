using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Attendance_Shifts_Calculation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AttendanceCorrections",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    FromDate = table.Column<DateTime>(type: "date", nullable: false),
                    ToDate = table.Column<DateTime>(type: "date", nullable: false),
                    PunchTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PunchId = table.Column<long>(type: "bigint", nullable: true),
                    ExcuseKind = table.Column<int>(type: "int", nullable: true),
                    WholeDays = table.Column<bool>(type: "bit", nullable: false),
                    FromAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ToAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CancelledByUserId = table.Column<int>(type: "int", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceCorrections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendanceCorrections_AttendancePunches_PunchId",
                        column: x => x.PunchId,
                        principalTable: "AttendancePunches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceCorrections_Users_CancelledByUserId",
                        column: x => x.CancelledByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceCorrections_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceCorrections_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AttendanceRecalcRequests",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    FromDate = table.Column<DateTime>(type: "date", nullable: false),
                    ToDate = table.Column<DateTime>(type: "date", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceRecalcRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AttendanceShifts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CycleUnit = table.Column<int>(type: "int", nullable: false),
                    CycleLength = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateTime>(type: "date", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceShifts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AttendanceTimetables",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Color = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false),
                    StartMinute = table.Column<int>(type: "int", nullable: false),
                    EndMinute = table.Column<int>(type: "int", nullable: false),
                    CheckInBeforeMinutes = table.Column<int>(type: "int", nullable: false),
                    CheckInAfterMinutes = table.Column<int>(type: "int", nullable: false),
                    CheckOutBeforeMinutes = table.Column<int>(type: "int", nullable: false),
                    CheckOutAfterMinutes = table.Column<int>(type: "int", nullable: false),
                    LateGraceMinutes = table.Column<int>(type: "int", nullable: false),
                    EarlyLeaveGraceMinutes = table.Column<int>(type: "int", nullable: false),
                    LateCounting = table.Column<int>(type: "int", nullable: false),
                    RequireCheckIn = table.Column<bool>(type: "bit", nullable: false),
                    RequireCheckOut = table.Column<bool>(type: "bit", nullable: false),
                    BreakMinutes = table.Column<int>(type: "int", nullable: false),
                    RequiredMinutes = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceTimetables", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AttendanceDays",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateTime>(type: "date", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ShiftId = table.Column<int>(type: "int", nullable: true),
                    FirstIn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastOut = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LateMinutes = table.Column<int>(type: "int", nullable: false),
                    EarlyMinutes = table.Column<int>(type: "int", nullable: false),
                    ShortMinutes = table.Column<int>(type: "int", nullable: false),
                    WorkMinutes = table.Column<int>(type: "int", nullable: false),
                    RequiredMinutes = table.Column<int>(type: "int", nullable: false),
                    PunchCount = table.Column<int>(type: "int", nullable: false),
                    HasExcuse = table.Column<bool>(type: "bit", nullable: false),
                    Hash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CalculatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceDays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendanceDays_AttendanceShifts_ShiftId",
                        column: x => x.ShiftId,
                        principalTable: "AttendanceShifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AttendanceDays_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AttendanceSchedules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TargetType = table.Column<int>(type: "int", nullable: false),
                    TargetId = table.Column<int>(type: "int", nullable: false),
                    ShiftId = table.Column<int>(type: "int", nullable: false),
                    FromDate = table.Column<DateTime>(type: "date", nullable: false),
                    ToDate = table.Column<DateTime>(type: "date", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendanceSchedules_AttendanceShifts_ShiftId",
                        column: x => x.ShiftId,
                        principalTable: "AttendanceShifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceSchedules_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceSchedules_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AttendanceShiftDays",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ShiftId = table.Column<int>(type: "int", nullable: false),
                    DayIndex = table.Column<int>(type: "int", nullable: false),
                    TimetableId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceShiftDays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendanceShiftDays_AttendanceShifts_ShiftId",
                        column: x => x.ShiftId,
                        principalTable: "AttendanceShifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AttendanceShiftDays_AttendanceTimetables_TimetableId",
                        column: x => x.TimetableId,
                        principalTable: "AttendanceTimetables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AttendanceTemporarySchedules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TargetType = table.Column<int>(type: "int", nullable: false),
                    TargetId = table.Column<int>(type: "int", nullable: false),
                    FromDate = table.Column<DateTime>(type: "date", nullable: false),
                    ToDate = table.Column<DateTime>(type: "date", nullable: false),
                    TimetableId = table.Column<int>(type: "int", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceTemporarySchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendanceTemporarySchedules_AttendanceTimetables_TimetableId",
                        column: x => x.TimetableId,
                        principalTable: "AttendanceTimetables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceTemporarySchedules_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AttendanceDayParts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DayId = table.Column<long>(type: "bigint", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    TimetableId = table.Column<int>(type: "int", nullable: false),
                    TimetableName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    PlannedStart = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PlannedEnd = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CheckIn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CheckOut = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    LateMinutes = table.Column<int>(type: "int", nullable: false),
                    EarlyMinutes = table.Column<int>(type: "int", nullable: false),
                    ShortMinutes = table.Column<int>(type: "int", nullable: false),
                    WorkMinutes = table.Column<int>(type: "int", nullable: false),
                    RequiredMinutes = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceDayParts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendanceDayParts_AttendanceDays_DayId",
                        column: x => x.DayId,
                        principalTable: "AttendanceDays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceCorrections_CancelledByUserId",
                table: "AttendanceCorrections",
                column: "CancelledByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceCorrections_CreatedByUserId",
                table: "AttendanceCorrections",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceCorrections_PunchId",
                table: "AttendanceCorrections",
                column: "PunchId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceCorrections_UserId_FromDate",
                table: "AttendanceCorrections",
                columns: new[] { "UserId", "FromDate" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceDayParts_DayId",
                table: "AttendanceDayParts",
                column: "DayId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceDays_Date_Status",
                table: "AttendanceDays",
                columns: new[] { "Date", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceDays_ShiftId",
                table: "AttendanceDays",
                column: "ShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceDays_UserId_Date",
                table: "AttendanceDays",
                columns: new[] { "UserId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceSchedules_CreatedByUserId",
                table: "AttendanceSchedules",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceSchedules_ShiftId",
                table: "AttendanceSchedules",
                column: "ShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceSchedules_TargetType_TargetId_FromDate",
                table: "AttendanceSchedules",
                columns: new[] { "TargetType", "TargetId", "FromDate" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceSchedules_UpdatedByUserId",
                table: "AttendanceSchedules",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceShiftDays_ShiftId_DayIndex_TimetableId",
                table: "AttendanceShiftDays",
                columns: new[] { "ShiftId", "DayIndex", "TimetableId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceShiftDays_TimetableId",
                table: "AttendanceShiftDays",
                column: "TimetableId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceShifts_Name",
                table: "AttendanceShifts",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceTemporarySchedules_CreatedByUserId",
                table: "AttendanceTemporarySchedules",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceTemporarySchedules_TargetType_TargetId_FromDate",
                table: "AttendanceTemporarySchedules",
                columns: new[] { "TargetType", "TargetId", "FromDate" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceTemporarySchedules_TimetableId",
                table: "AttendanceTemporarySchedules",
                column: "TimetableId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceTimetables_Name",
                table: "AttendanceTimetables",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AttendanceCorrections");

            migrationBuilder.DropTable(
                name: "AttendanceDayParts");

            migrationBuilder.DropTable(
                name: "AttendanceRecalcRequests");

            migrationBuilder.DropTable(
                name: "AttendanceSchedules");

            migrationBuilder.DropTable(
                name: "AttendanceShiftDays");

            migrationBuilder.DropTable(
                name: "AttendanceTemporarySchedules");

            migrationBuilder.DropTable(
                name: "AttendanceDays");

            migrationBuilder.DropTable(
                name: "AttendanceTimetables");

            migrationBuilder.DropTable(
                name: "AttendanceShifts");
        }
    }
}
