using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Attendance_Areas_Devices_Punches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AttendanceAreaId",
                table: "Branches",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AttendanceAreas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceAreas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AttendanceDevices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SerialNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    State = table.Column<int>(type: "int", nullable: false),
                    AreaId = table.Column<int>(type: "int", nullable: true),
                    Model = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Firmware = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PushVersion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    FingerAlgorithm = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    FaceAlgorithm = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    UserCount = table.Column<int>(type: "int", nullable: true),
                    FingerCount = table.Column<int>(type: "int", nullable: true),
                    FaceCount = table.Column<int>(type: "int", nullable: true),
                    PunchCount = table.Column<int>(type: "int", nullable: true),
                    AttLogStamp = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    OperLogStamp = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    PhotoStamp = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    FirstSeenAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OfflineNotifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NeedsSync = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ApprovedByUserId = table.Column<int>(type: "int", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceDevices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendanceDevices_AttendanceAreas_AreaId",
                        column: x => x.AreaId,
                        principalTable: "AttendanceAreas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceDevices_Users_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AttendanceProfiles",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    AttendanceNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CardNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    IsDeviceAdmin = table.Column<bool>(type: "bit", nullable: false),
                    AreaId = table.Column<int>(type: "int", nullable: true),
                    ActiveFrom = table.Column<DateTime>(type: "date", nullable: true),
                    ActiveTo = table.Column<DateTime>(type: "date", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceProfiles", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_AttendanceProfiles_AttendanceAreas_AreaId",
                        column: x => x.AreaId,
                        principalTable: "AttendanceAreas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceProfiles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AttendanceBiometrics",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    No = table.Column<int>(type: "int", nullable: false),
                    Index = table.Column<int>(type: "int", nullable: false),
                    IsValid = table.Column<bool>(type: "bit", nullable: false),
                    Duress = table.Column<bool>(type: "bit", nullable: false),
                    MajorVersion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MinorVersion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Format = table.Column<int>(type: "int", nullable: false),
                    Data = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    Hash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SourceDeviceId = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceBiometrics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendanceBiometrics_AttendanceDevices_SourceDeviceId",
                        column: x => x.SourceDeviceId,
                        principalTable: "AttendanceDevices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceBiometrics_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AttendanceBioPhotos",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Data = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    Hash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Size = table.Column<int>(type: "int", nullable: false),
                    SourceDeviceId = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceBioPhotos", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_AttendanceBioPhotos_AttendanceDevices_SourceDeviceId",
                        column: x => x.SourceDeviceId,
                        principalTable: "AttendanceDevices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceBioPhotos_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AttendanceDeviceCommands",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DeviceId = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    State = table.Column<int>(type: "int", nullable: false),
                    ReturnCode = table.Column<int>(type: "int", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceDeviceCommands", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendanceDeviceCommands_AttendanceDevices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "AttendanceDevices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceDeviceCommands_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AttendanceDeviceLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DeviceId = table.Column<int>(type: "int", nullable: false),
                    Event = table.Column<int>(type: "int", nullable: false),
                    Details = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceDeviceLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendanceDeviceLogs_AttendanceDevices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "AttendanceDevices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceDeviceLogs_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AttendanceDeviceUsers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DeviceId = table.Column<int>(type: "int", nullable: false),
                    Pin = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Managed = table.Column<bool>(type: "bit", nullable: false),
                    SyncedHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    BiometricHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    SyncedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceDeviceUsers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendanceDeviceUsers_AttendanceDevices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "AttendanceDevices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AttendanceDeviceUsers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "AttendancePunches",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DeviceId = table.Column<int>(type: "int", nullable: false),
                    DeviceSerial = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Pin = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    PunchTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    VerifyType = table.Column<int>(type: "int", nullable: false),
                    WorkCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Source = table.Column<int>(type: "int", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendancePunches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendancePunches_AttendanceDevices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "AttendanceDevices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendancePunches_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AttendancePunchPhotos",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DeviceId = table.Column<int>(type: "int", nullable: false),
                    DeviceSerial = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Pin = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    PunchTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Size = table.Column<int>(type: "int", nullable: false),
                    StorageKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendancePunchPhotos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendancePunchPhotos_AttendanceDevices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "AttendanceDevices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AttendanceProfileAreas",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    AreaId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceProfileAreas", x => new { x.UserId, x.AreaId });
                    table.ForeignKey(
                        name: "FK_AttendanceProfileAreas_AttendanceAreas_AreaId",
                        column: x => x.AreaId,
                        principalTable: "AttendanceAreas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceProfileAreas_AttendanceProfiles_UserId",
                        column: x => x.UserId,
                        principalTable: "AttendanceProfiles",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AttendancePunchPhotoContents",
                columns: table => new
                {
                    PhotoId = table.Column<long>(type: "bigint", nullable: false),
                    Data = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendancePunchPhotoContents", x => x.PhotoId);
                    table.ForeignKey(
                        name: "FK_AttendancePunchPhotoContents_AttendancePunchPhotos_PhotoId",
                        column: x => x.PhotoId,
                        principalTable: "AttendancePunchPhotos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Branches_AttendanceAreaId",
                table: "Branches",
                column: "AttendanceAreaId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceAreas_Name",
                table: "AttendanceAreas",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceBiometrics_SourceDeviceId",
                table: "AttendanceBiometrics",
                column: "SourceDeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceBiometrics_UserId_Type_No_Index",
                table: "AttendanceBiometrics",
                columns: new[] { "UserId", "Type", "No", "Index" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceBioPhotos_SourceDeviceId",
                table: "AttendanceBioPhotos",
                column: "SourceDeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceDeviceCommands_CreatedAt",
                table: "AttendanceDeviceCommands",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceDeviceCommands_CreatedByUserId",
                table: "AttendanceDeviceCommands",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceDeviceCommands_DeviceId_State_Id",
                table: "AttendanceDeviceCommands",
                columns: new[] { "DeviceId", "State", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceDeviceLogs_DeviceId_CreatedAt",
                table: "AttendanceDeviceLogs",
                columns: new[] { "DeviceId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceDeviceLogs_UserId",
                table: "AttendanceDeviceLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceDevices_ApprovedByUserId",
                table: "AttendanceDevices",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceDevices_AreaId",
                table: "AttendanceDevices",
                column: "AreaId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceDevices_SerialNumber",
                table: "AttendanceDevices",
                column: "SerialNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceDevices_State_AreaId",
                table: "AttendanceDevices",
                columns: new[] { "State", "AreaId" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceDeviceUsers_DeviceId_Pin",
                table: "AttendanceDeviceUsers",
                columns: new[] { "DeviceId", "Pin" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceDeviceUsers_UserId",
                table: "AttendanceDeviceUsers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceProfileAreas_AreaId",
                table: "AttendanceProfileAreas",
                column: "AreaId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceProfiles_AreaId",
                table: "AttendanceProfiles",
                column: "AreaId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceProfiles_AttendanceNumber",
                table: "AttendanceProfiles",
                column: "AttendanceNumber",
                unique: true,
                filter: "[AttendanceNumber] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceProfiles_CardNumber",
                table: "AttendanceProfiles",
                column: "CardNumber",
                unique: true,
                filter: "[CardNumber] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AttendancePunches_DeviceId_PunchTime",
                table: "AttendancePunches",
                columns: new[] { "DeviceId", "PunchTime" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendancePunches_DeviceSerial_Pin_PunchTime",
                table: "AttendancePunches",
                columns: new[] { "DeviceSerial", "Pin", "PunchTime" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttendancePunches_PunchTime",
                table: "AttendancePunches",
                column: "PunchTime");

            migrationBuilder.CreateIndex(
                name: "IX_AttendancePunches_UnknownPin",
                table: "AttendancePunches",
                column: "Pin",
                filter: "[UserId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AttendancePunches_UserId_PunchTime",
                table: "AttendancePunches",
                columns: new[] { "UserId", "PunchTime" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendancePunchPhotos_DeviceId",
                table: "AttendancePunchPhotos",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendancePunchPhotos_DeviceSerial_Pin_PunchTime",
                table: "AttendancePunchPhotos",
                columns: new[] { "DeviceSerial", "Pin", "PunchTime" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttendancePunchPhotos_PunchTime",
                table: "AttendancePunchPhotos",
                column: "PunchTime");

            migrationBuilder.CreateIndex(
                name: "IX_AttendancePunchPhotos_StorageKey",
                table: "AttendancePunchPhotos",
                column: "StorageKey",
                unique: true,
                filter: "[StorageKey] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Branches_AttendanceAreas_AttendanceAreaId",
                table: "Branches",
                column: "AttendanceAreaId",
                principalTable: "AttendanceAreas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Branches_AttendanceAreas_AttendanceAreaId",
                table: "Branches");

            migrationBuilder.DropTable(
                name: "AttendanceBiometrics");

            migrationBuilder.DropTable(
                name: "AttendanceBioPhotos");

            migrationBuilder.DropTable(
                name: "AttendanceDeviceCommands");

            migrationBuilder.DropTable(
                name: "AttendanceDeviceLogs");

            migrationBuilder.DropTable(
                name: "AttendanceDeviceUsers");

            migrationBuilder.DropTable(
                name: "AttendanceProfileAreas");

            migrationBuilder.DropTable(
                name: "AttendancePunches");

            migrationBuilder.DropTable(
                name: "AttendancePunchPhotoContents");

            migrationBuilder.DropTable(
                name: "AttendanceProfiles");

            migrationBuilder.DropTable(
                name: "AttendancePunchPhotos");

            migrationBuilder.DropTable(
                name: "AttendanceDevices");

            migrationBuilder.DropTable(
                name: "AttendanceAreas");

            migrationBuilder.DropIndex(
                name: "IX_Branches_AttendanceAreaId",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "AttendanceAreaId",
                table: "Branches");
        }
    }
}
