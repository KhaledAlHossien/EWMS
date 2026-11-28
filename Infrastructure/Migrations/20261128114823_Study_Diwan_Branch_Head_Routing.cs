using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <summary>
    /// مسار معاملة الديوان الجديد (طلب الواجهة 2026-11-28): رئيس الفرع يحدد قرار المركز ويحيل إلى مدير المشاريع أو يعيد إلى الديوان،
    /// ومدير المشاريع يختار التعديل التخطيطي أو الفني بعد الرفض. يضيف مرحلة المعاملة ورقم جولتها وجدول الجولات (كتب بيدها):
    /// المرحلة تُستنتج من الحالة القديمة وسطر رئيس الفرع، والمعاملات المحالة سابقاً تأخذ جولتها الأولى، وقرار المعاملات التي لم
    /// يُحِلها رئيس الفرع بعد (سجّله الموظف في المسار القديم) يُمحى. الصلاحية الجديدة لمن يملك إصدار أمر البدء.
    /// </summary>
    public partial class Study_Diwan_Branch_Head_Routing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RoundNumber",
                table: "StudyDiwanTransactions",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "Stage",
                table: "StudyDiwanTransactions",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "StudyDiwanRounds",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TransactionId = table.Column<int>(type: "int", nullable: false),
                    RoundNumber = table.Column<int>(type: "int", nullable: false),
                    DispatchNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DispatchDate = table.Column<DateTime>(type: "date", nullable: false),
                    ReplyNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ReplyDate = table.Column<DateTime>(type: "date", nullable: false),
                    CentralDecision = table.Column<int>(type: "int", nullable: false),
                    ReferredByUserId = table.Column<int>(type: "int", nullable: false),
                    ReferredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReferralNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RevisionKind = table.Column<int>(type: "int", nullable: true),
                    RevisionByUserId = table.Column<int>(type: "int", nullable: true),
                    RevisionAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevisionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudyDiwanRounds", x => x.Id);
                    table.CheckConstraint("CK_StudyDiwanRounds_Decision", "[CentralDecision] IN (1, 2)");
                    table.CheckConstraint("CK_StudyDiwanRounds_Revision", "[RevisionKind] IS NULL OR [RevisionKind] IN (1, 2)");
                    table.ForeignKey(
                        name: "FK_StudyDiwanRounds_StudyDiwanTransactions_TransactionId",
                        column: x => x.TransactionId,
                        principalTable: "StudyDiwanTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StudyDiwanRounds_Users_ReferredByUserId",
                        column: x => x.ReferredByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudyDiwanRounds_Users_RevisionByUserId",
                        column: x => x.RevisionByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // ───────── المرحلة من الحالة القديمة ─────────
            // المسار القديم: رئيس الفرع «يوافق» على قرار سجّله الموظف. رفض سطره = إعادة إلى الديوان (3)؛ موافقته على رفض مركزي
            // = أُعيد الرد إلى القسم المتابع تلقائياً، فهي الآن تعديل فني (7)، أو جاهزة لصادر جديد (8) إن اعتُمد الرد المعدّل.
            migrationBuilder.Sql("""
                UPDATE t SET Stage = CASE
                    WHEN t.Status = 1 THEN 1
                    WHEN t.Status = 2 THEN 2
                    WHEN t.Status = 3 THEN 4
                    WHEN t.Status = 5 THEN 9
                    WHEN t.Status = 4 AND t.CentralDecision = 2 AND h.Decision IN (2, 3) AND r.Status = 3 THEN 8
                    WHEN t.Status = 4 AND t.CentralDecision = 2 AND h.Decision IN (2, 3) THEN 7
                    ELSE 3 END
                FROM StudyDiwanTransactions t
                JOIN StudyAssignmentResponses r ON r.Id = t.ResponseId
                OUTER APPLY (SELECT TOP 1 a.Decision FROM StudyApprovals a
                             WHERE a.DiwanTransactionId = t.Id AND a.SignerRole = 4 ORDER BY a.SortOrder) h;
                """);

            // ───────── الجولة الأولى للمعاملات المحالة (موافقة، تعديل فني، جاهزة، أو ملغاة بعد إحالة) ─────────
            migrationBuilder.Sql("""
                INSERT INTO StudyDiwanRounds (TransactionId, RoundNumber, DispatchNumber, DispatchDate, ReplyNumber, ReplyDate, CentralDecision,
                    ReferredByUserId, ReferredAt, ReferralNote, RevisionKind, RevisionByUserId, RevisionAt, RevisionReason)
                SELECT t.Id, 1, t.DispatchNumber, t.DispatchDate, t.ReplyNumber, t.ReplyDate, t.CentralDecision,
                       h.SignerUserId, h.DecidedAt, h.Note,
                       CASE WHEN t.CentralDecision = 2 THEN 2 END,
                       CASE WHEN t.CentralDecision = 2 THEN h.SignerUserId END,
                       CASE WHEN t.CentralDecision = 2 THEN h.DecidedAt END,
                       CASE WHEN t.CentralDecision = 2 THEN N'أُعيد الرد إلى القسم المتابع تلقائياً (المسار السابق لمعاملة الديوان)' END
                FROM StudyDiwanTransactions t
                CROSS APPLY (SELECT TOP 1 a.SignerUserId, a.DecidedAt, a.Note FROM StudyApprovals a
                             WHERE a.DiwanTransactionId = t.Id AND a.SignerRole = 4 AND a.Decision IN (2, 3)
                               AND a.SignerUserId IS NOT NULL AND a.DecidedAt IS NOT NULL
                             ORDER BY a.SortOrder) h
                WHERE t.Stage IN (4, 7, 8, 9) AND t.CentralDecision IS NOT NULL AND t.ReplyNumber IS NOT NULL AND t.ReplyDate IS NOT NULL;

                -- قرار لم يُحِله رئيس الفرع بعد: سجّله الموظف في المسار القديم، ويحدده رئيس الفرع الآن
                UPDATE StudyDiwanTransactions SET CentralDecision = NULL WHERE Stage IN (1, 2, 3);
                """);

            migrationBuilder.CreateIndex(
                name: "IX_StudyDiwanTransactions_Stage_CreatedAt",
                table: "StudyDiwanTransactions",
                columns: new[] { "Stage", "CreatedAt" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_StudyDiwanTransactions_Round",
                table: "StudyDiwanTransactions",
                sql: "[RoundNumber] >= 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_StudyDiwanTransactions_Stage",
                table: "StudyDiwanTransactions",
                sql: "[Stage] BETWEEN 1 AND 9");

            migrationBuilder.CreateIndex(
                name: "IX_StudyDiwanRounds_ReferredByUserId",
                table: "StudyDiwanRounds",
                column: "ReferredByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StudyDiwanRounds_RevisionByUserId",
                table: "StudyDiwanRounds",
                column: "RevisionByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StudyDiwanRounds_TransactionId_RoundNumber",
                table: "StudyDiwanRounds",
                columns: new[] { "TransactionId", "RoundNumber" },
                unique: true);

            // ───────── الصلاحيات ─────────
            // مستلم الرفض المركزي = مدير المشاريع، وهو نفسه من يصدر أمر البدء: تُمنح لكل دور يملك CreateStudyStartOrder
            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM Permissions WHERE Name = N'HandleStudyDiwanRejection')
                    INSERT INTO Permissions (Name, Description)
                    VALUES (N'HandleStudyDiwanRejection', N'استلام معاملة الديوان المرفوضة مركزياً وإحالتها: تعديل تخطيطي للرد أو تعديل فني يعيده إلى القسم المتابع مع السبب (في فرعه)');
                INSERT INTO RolePermissions (RoleId, PermissionId)
                SELECT DISTINCT rp.RoleId, t.Id
                FROM RolePermissions rp
                JOIN Permissions s ON s.Id = rp.PermissionId AND s.Name = N'CreateStudyStartOrder'
                JOIN Permissions t ON t.Name = N'HandleStudyDiwanRejection'
                WHERE NOT EXISTS (SELECT 1 FROM RolePermissions x WHERE x.RoleId = rp.RoleId AND x.PermissionId = t.Id);

                UPDATE Permissions SET Description = N'تعديل معاملة الديوان (الصادر ورقم الرد المركزي وتاريخه) وإرفاق صورة الكتاب وإرسالها إلى رئيس الفرع، وتسجيل صادر جديد بعد اعتماد الرد المعدّل (في فرعه)' WHERE Name = N'EditStudyDiwanTransaction';
                UPDATE Permissions SET Description = N'حذف معاملة ديوان ما زالت مسودة في جولتها الأولى (في فرعه)' WHERE Name = N'DeleteStudyDiwanTransaction';
                UPDATE Permissions SET Description = N'التوقيع بصفة رئيس الفرع (التكليفات وأوامر البدء وإنجاز المشاريع)، وفي معاملات الديوان: الإحالة إلى مدير المشاريع بموافقة أو رفض، أو الإعادة إلى الديوان' WHERE Name = N'SignStudyAsBranchHead';
                """);
        }

        /// <summary>
        /// يعيد قرار آخر جولة إلى المعاملة (المسار القديم يقرؤه من المعاملة نفسها)، ويحذف الجولات والمرحلة والصلاحية الجديدة.
        /// معاملة في تعديل تخطيطي يبقى ردها بانتظار توقيع التخطيط كما هو.
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE t SET CentralDecision = last.CentralDecision
                FROM StudyDiwanTransactions t
                CROSS APPLY (SELECT TOP 1 r.CentralDecision FROM StudyDiwanRounds r WHERE r.TransactionId = t.Id ORDER BY r.RoundNumber DESC) last
                WHERE t.CentralDecision IS NULL AND t.Stage NOT IN (1, 2, 3);

                DELETE FROM Permissions WHERE Name = N'HandleStudyDiwanRejection';
                UPDATE Permissions SET Description = N'تعديل معاملة الديوان (الصادر والرد المركزي وقراره) وإرفاق صورة الكتاب وإرسالها لاعتماد رئيس الفرع (المسودة أو المرفوضة، في فرعه)' WHERE Name = N'EditStudyDiwanTransaction';
                UPDATE Permissions SET Description = N'حذف معاملة ديوان ما زالت مسودة (في فرعه)' WHERE Name = N'DeleteStudyDiwanTransaction';
                UPDATE Permissions SET Description = N'التوقيع بصفة رئيس الفرع (التكليفات ومعاملات الديوان وأوامر البدء وإنجاز المشاريع)' WHERE Name = N'SignStudyAsBranchHead';
                """);

            migrationBuilder.DropTable(
                name: "StudyDiwanRounds");

            migrationBuilder.DropIndex(
                name: "IX_StudyDiwanTransactions_Stage_CreatedAt",
                table: "StudyDiwanTransactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_StudyDiwanTransactions_Round",
                table: "StudyDiwanTransactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_StudyDiwanTransactions_Stage",
                table: "StudyDiwanTransactions");

            migrationBuilder.DropColumn(
                name: "RoundNumber",
                table: "StudyDiwanTransactions");

            migrationBuilder.DropColumn(
                name: "Stage",
                table: "StudyDiwanTransactions");
        }
    }
}
