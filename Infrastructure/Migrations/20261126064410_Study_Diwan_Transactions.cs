using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <summary>
    /// معاملة الديوان مستنداً (DWN) بدل السجل الملحق بالرد (طلب الواجهة 2026-11-26) — مكتوب يدوياً:
    /// <list type="bullet">
    /// <item>جدول StudyDiwanTransactions، وعمود DiwanTransactionId في StudyApprovals وStudyAttachments (القيدان الحصريان) وStudyStartOrders.</item>
    /// <item>كل StudyDiwanCases يصير معاملة: الرد المركزي الأحدث يملأ حقولها (المرحلة 2 = صادر جديد بلا رد بعد)، والحالة والسطر:
    /// 2→مسودة بلا سطر، 3→بانتظار الاعتماد وسطر رئيس الفرع معلّق، 4→مرفوضة وسطر مرفوض بملاحظة ReviewNote، 5 و7→معتمدة،
    /// 6→مرفوضة بقرار رفض مركزي وسطر معتمد. الموقّع والتاريخ من سجل الدراسة (إجراء رئيس الفرع «… الكتاب المركزي …»)، بلا نسخة توقيع.</item>
    /// <item>كل كتاب رد مركزي يصير مرفقاً على المعاملة بنوع «كتاب رسمي» (يُنشأ النوع إن لم يوجد)؛ الحجم 0 ثم تقرؤه
    /// DbSeeder.BackfillStudyAttachmentSizesAsync من الملف عند الإقلاع.</item>
    /// <item>أوامر البدء ذات DiwanReplyId تُربط بمعاملة ردها؛ الأوامر الأقدم تبقى بلا معاملة. أرقام DWN بترتيب تاريخ الصادر، وStudyNumberSequences.</item>
    /// <item>الصلاحيات: Create/Edit/DeleteStudyDiwanTransaction لمن كان يملك ManageStudyDiwanCorrespondence (وتُحذف)، والعرض لمن يملك ViewStudyResponses.</item>
    /// <item>الجدولان القديمان لا يُحذفان الآن: يُعاد تسميتهما StudyDiwanCases_Legacy وStudyDiwanReplies_Legacy بلا مفاتيح أجنبية،
    /// ويُحذفان بترحيل لاحق بعد التحقق من الترحيل.</item>
    /// </list>
    /// </summary>
    public partial class Study_Diwan_Transactions : Migration
    {
        /// <summary>العبارات التي تقرأ أعمدة أضيفت في الترحيل نفسه تُنفَّذ بـ EXEC كي تعمل أيضاً حين يُطبَّق بسكربت (دفعة واحدة)</summary>
        private static string Exec(string sql) => "EXEC(N'" + sql.Replace("'", "''") + "');";

        private const string ApprovalsCheck =
            "(CASE WHEN [AssignmentId] IS NULL THEN 0 ELSE 1 END) + (CASE WHEN [ResponseId] IS NULL THEN 0 ELSE 1 END) + (CASE WHEN [StartOrderId] IS NULL THEN 0 ELSE 1 END) + (CASE WHEN [DesignApprovalId] IS NULL THEN 0 ELSE 1 END) + (CASE WHEN [ProjectId] IS NULL THEN 0 ELSE 1 END)";
        private const string AttachmentsCheck =
            "(CASE WHEN [RequestId] IS NULL THEN 0 ELSE 1 END) + (CASE WHEN [AssignmentId] IS NULL THEN 0 ELSE 1 END) + (CASE WHEN [ResponseId] IS NULL THEN 0 ELSE 1 END) + (CASE WHEN [StartOrderId] IS NULL THEN 0 ELSE 1 END) + (CASE WHEN [ProjectId] IS NULL THEN 0 ELSE 1 END) + (CASE WHEN [DrawingId] IS NULL THEN 0 ELSE 1 END) + (CASE WHEN [VersionId] IS NULL THEN 0 ELSE 1 END)";
        private const string DiwanColumn = " + (CASE WHEN [DiwanTransactionId] IS NULL THEN 0 ELSE 1 END)";

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ───────── البنية الجديدة ─────────
            migrationBuilder.DropForeignKey(name: "FK_StudyStartOrders_StudyDiwanReplies_ResponseId_DiwanReplyId", table: "StudyStartOrders");
            migrationBuilder.DropIndex(name: "IX_StudyStartOrders_ResponseId_DiwanReplyId", table: "StudyStartOrders");
            migrationBuilder.DropCheckConstraint(name: "CK_StudyAttachments_OneOwner", table: "StudyAttachments");
            migrationBuilder.DropCheckConstraint(name: "CK_StudyApprovals_OneOwner", table: "StudyApprovals");

            migrationBuilder.AddColumn<int>(name: "DiwanTransactionId", table: "StudyStartOrders", type: "int", nullable: true);
            migrationBuilder.AddColumn<int>(name: "DiwanTransactionId", table: "StudyAttachments", type: "int", nullable: true);
            migrationBuilder.AddColumn<int>(name: "DiwanTransactionId", table: "StudyApprovals", type: "int", nullable: true);

            migrationBuilder.CreateTable(
                name: "StudyDiwanTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ResponseId = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    TransactionNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    DispatchNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DispatchDate = table.Column<DateTime>(type: "date", nullable: false),
                    ReplyNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ReplyDate = table.Column<DateTime>(type: "date", nullable: true),
                    CentralDecision = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudyDiwanTransactions", x => x.Id);
                    table.CheckConstraint("CK_StudyDiwanTransactions_Decision", "[CentralDecision] IS NULL OR [CentralDecision] IN (1, 2)");
                    table.ForeignKey(
                        name: "FK_StudyDiwanTransactions_StudyAssignmentResponses_ResponseId",
                        column: x => x.ResponseId,
                        principalTable: "StudyAssignmentResponses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudyDiwanTransactions_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(name: "IX_StudyDiwanTransactions_CreatedByUserId", table: "StudyDiwanTransactions", column: "CreatedByUserId");
            migrationBuilder.CreateIndex(name: "IX_StudyDiwanTransactions_ResponseId", table: "StudyDiwanTransactions", column: "ResponseId", unique: true);
            migrationBuilder.CreateIndex(name: "IX_StudyDiwanTransactions_Status_CreatedAt", table: "StudyDiwanTransactions", columns: new[] { "Status", "CreatedAt" });
            migrationBuilder.CreateIndex(name: "IX_StudyDiwanTransactions_TransactionNumber", table: "StudyDiwanTransactions", column: "TransactionNumber", unique: true);

            // ───────── ترحيل البيانات ─────────
            migrationBuilder.Sql(Exec("""
                DECLARE @Letter int = (SELECT TOP 1 Id FROM StudyAttachmentTypes WHERE Name = N'كتاب رسمي' ORDER BY Id);
                IF @Letter IS NULL AND EXISTS (SELECT 1 FROM StudyDiwanReplies)
                BEGIN
                    INSERT StudyAttachmentTypes (Name, IsActive) VALUES (N'كتاب رسمي', 1);
                    SET @Letter = SCOPE_IDENTITY();
                END;

                -- 1) معاملة لكل حالة ديوان، بالرد المركزي الأحدث (المرحلة 2 = صادر جديد ينتظر رده)، وأرقام DWN بترتيب تاريخ الصادر
                WITH latest AS (SELECT r.ResponseId, r.Number, r.Date, r.Decision,
                                       ROW_NUMBER() OVER (PARTITION BY r.ResponseId ORDER BY r.Id DESC) AS n FROM StudyDiwanReplies r),
                     creator AS (SELECT l.EntityId AS ResponseId, l.UserId, l.CreatedAt,
                                        ROW_NUMBER() OVER (PARTITION BY l.EntityId ORDER BY l.CreatedAt, l.Id) AS n
                                 FROM StudyActivityLogs l WHERE l.EntityType = 3 AND l.Action = 3 AND l.Title LIKE N'%الصادر%'),
                     numbered AS (SELECT c.*, ROW_NUMBER() OVER (PARTITION BY YEAR(c.DispatchDate) ORDER BY c.DispatchDate, c.ResponseId) AS k
                                  FROM StudyDiwanCases c)
                INSERT StudyDiwanTransactions (ResponseId, CreatedByUserId, TransactionNumber, DispatchNumber, DispatchDate,
                                               ReplyNumber, ReplyDate, CentralDecision, Notes, Status, CreatedAt, UpdatedAt)
                SELECT c.ResponseId, COALESCE(cr.UserId, resp.CreatedByUserId),
                       N'DWN-' + RIGHT(N'0' + CAST(YEAR(c.DispatchDate) % 100 AS nvarchar(2)), 2) + N'-' + RIGHT(N'000' + CAST(c.k AS nvarchar(10)), 4),
                       c.DispatchNumber, c.DispatchDate,
                       CASE WHEN c.Stage = 2 THEN NULL ELSE lr.Number END,
                       CASE WHEN c.Stage = 2 THEN NULL ELSE lr.Date END,
                       CASE WHEN c.Stage = 2 THEN NULL ELSE lr.Decision END,
                       NULL,
                       CASE c.Stage WHEN 2 THEN 1 WHEN 3 THEN 2 WHEN 4 THEN 4 WHEN 6 THEN 4 ELSE 3 END,
                       COALESCE(cr.CreatedAt, CAST(c.DispatchDate AS datetime2)), resp.UpdatedAt
                FROM numbered c
                JOIN StudyAssignmentResponses resp ON resp.Id = c.ResponseId
                LEFT JOIN latest lr ON lr.ResponseId = c.ResponseId AND lr.n = 1
                LEFT JOIN creator cr ON cr.ResponseId = c.ResponseId AND cr.n = 1;

                -- 2) سطر رئيس الفرع (إلا المسودة): الموقّع والتاريخ من آخر إجراء له في سجل الدراسة، بلا نسخة توقيع (يُطبع مربعاً فارغاً)
                INSERT StudyApprovals (DiwanTransactionId, SignerUserId, SignatureId, SignerRole, SortOrder, Decision, JobTitle, DecidedAt, Note)
                SELECT t.Id,
                       CASE WHEN c.Stage = 3 THEN NULL ELSE s.UserId END, NULL, 4, 1,
                       CASE c.Stage WHEN 3 THEN 1 WHEN 4 THEN 4 ELSE 2 END,
                       CASE WHEN c.Stage = 3 THEN NULL ELSE ro.Name END,
                       CASE WHEN c.Stage = 3 THEN NULL ELSE s.CreatedAt END,
                       CASE WHEN c.Stage = 3 THEN NULL ELSE c.ReviewNote END
                FROM StudyDiwanTransactions t
                JOIN StudyDiwanCases c ON c.ResponseId = t.ResponseId
                OUTER APPLY (SELECT TOP 1 l.UserId, l.CreatedAt FROM StudyActivityLogs l
                             WHERE l.EntityType = 3 AND l.EntityId = t.ResponseId AND l.Action <> 3 AND l.Title LIKE N'%الكتاب المركزي%'
                             ORDER BY l.CreatedAt DESC, l.Id DESC) s
                LEFT JOIN Users u ON u.Id = s.UserId
                LEFT JOIN Roles ro ON ro.Id = u.RoleId
                WHERE c.Stage <> 2;

                -- 3) كل كتاب رد مركزي مرفق على معاملته (الرافع من سجل تسجيله، وإلا منشئ المعاملة)
                INSERT StudyAttachments (DiwanTransactionId, AttachmentTypeId, UploadedByUserId, FileName, ContentType, Size, StorageKey, UploadedAt)
                SELECT t.Id, @Letter, COALESCE(up.UserId, t.CreatedByUserId), LEFT(r.FileName, 200), LEFT(r.ContentType, 150), 0, r.StorageKey, r.RecordedAt
                FROM StudyDiwanReplies r
                JOIN StudyDiwanTransactions t ON t.ResponseId = r.ResponseId
                OUTER APPLY (SELECT TOP 1 l.UserId FROM StudyActivityLogs l
                             WHERE l.EntityType = 3 AND l.EntityId = r.ResponseId AND l.Action = 3
                               AND l.Title LIKE N'%الرد المركزي%' AND l.Title LIKE N'%' + r.Number + N'%'
                             ORDER BY ABS(DATEDIFF(SECOND, l.CreatedAt, r.RecordedAt))) up;

                -- 4) أوامر البدء الصادرة من كتاب مركزي تُربط بمعاملة ردها
                UPDATE o SET DiwanTransactionId = t.Id
                FROM StudyStartOrders o JOIN StudyDiwanTransactions t ON t.ResponseId = o.ResponseId
                WHERE o.DiwanReplyId IS NOT NULL;

                -- 5) عدّاد DWN لكل سنة
                MERGE StudyNumberSequences AS s
                USING (SELECT YEAR(DispatchDate) AS [Year], COUNT(*) AS LastNumber FROM StudyDiwanTransactions GROUP BY YEAR(DispatchDate)) AS n
                ON s.DocumentType = 6 AND s.[Year] = n.[Year]
                WHEN MATCHED THEN UPDATE SET LastNumber = CASE WHEN s.LastNumber > n.LastNumber THEN s.LastNumber ELSE n.LastNumber END
                WHEN NOT MATCHED THEN INSERT (DocumentType, [Year], LastNumber) VALUES (6, n.[Year], n.LastNumber);
                """));

            // ───────── القيود والمفاتيح بعد البيانات ─────────
            migrationBuilder.AddCheckConstraint(name: "CK_StudyAttachments_OneOwner", table: "StudyAttachments", sql: AttachmentsCheck + DiwanColumn + " = 1");
            migrationBuilder.AddCheckConstraint(name: "CK_StudyApprovals_OneOwner", table: "StudyApprovals", sql: ApprovalsCheck + DiwanColumn + " = 1");
            migrationBuilder.CreateIndex(name: "IX_StudyStartOrders_DiwanTransactionId", table: "StudyStartOrders", column: "DiwanTransactionId");
            migrationBuilder.CreateIndex(name: "IX_StudyAttachments_DiwanTransactionId", table: "StudyAttachments", column: "DiwanTransactionId");
            migrationBuilder.CreateIndex(name: "IX_StudyApprovals_DiwanTransactionId_SignerRole", table: "StudyApprovals",
                columns: new[] { "DiwanTransactionId", "SignerRole" }, unique: true, filter: "[DiwanTransactionId] IS NOT NULL");
            migrationBuilder.AddForeignKey(name: "FK_StudyApprovals_StudyDiwanTransactions_DiwanTransactionId", table: "StudyApprovals",
                column: "DiwanTransactionId", principalTable: "StudyDiwanTransactions", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
            migrationBuilder.AddForeignKey(name: "FK_StudyAttachments_StudyDiwanTransactions_DiwanTransactionId", table: "StudyAttachments",
                column: "DiwanTransactionId", principalTable: "StudyDiwanTransactions", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
            migrationBuilder.AddForeignKey(name: "FK_StudyStartOrders_StudyDiwanTransactions_DiwanTransactionId", table: "StudyStartOrders",
                column: "DiwanTransactionId", principalTable: "StudyDiwanTransactions", principalColumn: "Id", onDelete: ReferentialAction.Restrict);

            // ───────── الجدولان القديمان: نسخة للتحقق بلا مفاتيح، وعمود الكتاب في أمر البدء يُحذف ─────────
            migrationBuilder.DropColumn(name: "DiwanReplyId", table: "StudyStartOrders");
            migrationBuilder.DropForeignKey(name: "FK_StudyDiwanReplies_StudyDiwanCases_ResponseId", table: "StudyDiwanReplies");
            migrationBuilder.DropForeignKey(name: "FK_StudyDiwanCases_StudyAssignmentResponses_ResponseId", table: "StudyDiwanCases");
            migrationBuilder.RenameTable(name: "StudyDiwanReplies", newName: "StudyDiwanReplies_Legacy");
            migrationBuilder.RenameTable(name: "StudyDiwanCases", newName: "StudyDiwanCases_Legacy");

            // ───────── الصلاحيات ─────────
            migrationBuilder.Sql("""
                DECLARE @p TABLE (Name nvarchar(100), Description nvarchar(300));
                INSERT INTO @p VALUES
                 (N'ViewStudyDiwanTransactions', N'عرض معاملات الديوان DWN (معاملات فرعه)'),
                 (N'CreateStudyDiwanTransaction', N'تسجيل الصادر إلى الديوان المركزي لرد معتمد: إنشاء معاملة الديوان (في فرعه)'),
                 (N'EditStudyDiwanTransaction', N'تعديل معاملة الديوان (الصادر والرد المركزي وقراره) وإرفاق صورة الكتاب وإرسالها لاعتماد رئيس الفرع (المسودة أو المرفوضة، في فرعه)'),
                 (N'DeleteStudyDiwanTransaction', N'حذف معاملة ديوان ما زالت مسودة (في فرعه)');
                INSERT INTO Permissions (Name, Description)
                SELECT p.Name, p.Description FROM @p p WHERE NOT EXISTS (SELECT 1 FROM Permissions x WHERE x.Name = p.Name);

                DECLARE @map TABLE (Source nvarchar(100), Target nvarchar(100));
                INSERT INTO @map VALUES
                 (N'ManageStudyDiwanCorrespondence', N'CreateStudyDiwanTransaction'),
                 (N'ManageStudyDiwanCorrespondence', N'EditStudyDiwanTransaction'),
                 (N'ManageStudyDiwanCorrespondence', N'DeleteStudyDiwanTransaction'),
                 (N'ViewStudyResponses', N'ViewStudyDiwanTransactions');
                INSERT INTO RolePermissions (RoleId, PermissionId)
                SELECT DISTINCT rp.RoleId, t.Id
                FROM RolePermissions rp
                JOIN Permissions s ON s.Id = rp.PermissionId
                JOIN @map m ON m.Source = s.Name
                JOIN Permissions t ON t.Name = m.Target
                WHERE NOT EXISTS (SELECT 1 FROM RolePermissions x WHERE x.RoleId = rp.RoleId AND x.PermissionId = t.Id);

                DELETE FROM Permissions WHERE Name = N'ManageStudyDiwanCorrespondence';

                UPDATE Permissions SET Description = N'إصدار أمر بدء دراسة PIO من معاملة ديوان معتمدة بقرار موافقة مركزي (في فرعه)' WHERE Name = N'CreateStudyStartOrder';
                UPDATE Permissions SET Description = N'التوقيع بصفة رئيس الفرع (التكليفات ومعاملات الديوان وأوامر البدء وإنجاز المشاريع)' WHERE Name = N'SignStudyAsBranchHead';
                UPDATE Permissions SET Description = REPLACE(Description, N'والرد وأمر البدء', N'والرد ومعاملة الديوان وأمر البدء') WHERE Name = N'PrintStudyDocuments';
                """);
        }

        /// <summary>
        /// يعيد الجدولين القديمين ومفاتيحهما وعمود DiwanReplyId (من آخر كتاب لرد أمر البدء) والصلاحية القديمة.
        /// المعاملات وسطورها ومرفقاتها تُحذف (ملفات الكتب تبقى في مكانها ويشير إليها الجدول القديم).
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM Permissions WHERE Name = N'ManageStudyDiwanCorrespondence')
                    INSERT INTO Permissions (Name, Description)
                    VALUES (N'ManageStudyDiwanCorrespondence', N'ديوان فرع الدراسات: تسجيل الصادر والرد الورقي المركزي وإرفاق صورة الكتاب في فرعه (مع صلاحية عرض إشعارات الرد)');
                INSERT INTO RolePermissions (RoleId, PermissionId)
                SELECT DISTINCT rp.RoleId, o.Id
                FROM RolePermissions rp
                JOIN Permissions s ON s.Id = rp.PermissionId AND s.Name = N'EditStudyDiwanTransaction'
                JOIN Permissions o ON o.Name = N'ManageStudyDiwanCorrespondence'
                WHERE NOT EXISTS (SELECT 1 FROM RolePermissions x WHERE x.RoleId = rp.RoleId AND x.PermissionId = o.Id);
                DELETE FROM Permissions WHERE Name IN (N'ViewStudyDiwanTransactions', N'CreateStudyDiwanTransaction',
                    N'EditStudyDiwanTransaction', N'DeleteStudyDiwanTransaction');
                """);

            migrationBuilder.RenameTable(name: "StudyDiwanCases_Legacy", newName: "StudyDiwanCases");
            migrationBuilder.RenameTable(name: "StudyDiwanReplies_Legacy", newName: "StudyDiwanReplies");
            migrationBuilder.AddForeignKey(name: "FK_StudyDiwanCases_StudyAssignmentResponses_ResponseId", table: "StudyDiwanCases",
                column: "ResponseId", principalTable: "StudyAssignmentResponses", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey(name: "FK_StudyDiwanReplies_StudyDiwanCases_ResponseId", table: "StudyDiwanReplies",
                column: "ResponseId", principalTable: "StudyDiwanCases", principalColumn: "ResponseId", onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddColumn<int>(name: "DiwanReplyId", table: "StudyStartOrders", type: "int", nullable: true);
            migrationBuilder.Sql(Exec("""
                UPDATE o SET DiwanReplyId = (SELECT MAX(r.Id) FROM StudyDiwanReplies r WHERE r.ResponseId = o.ResponseId)
                FROM StudyStartOrders o WHERE o.DiwanTransactionId IS NOT NULL;
                DELETE FROM StudyApprovals WHERE DiwanTransactionId IS NOT NULL;
                DELETE FROM StudyAttachments WHERE DiwanTransactionId IS NOT NULL;
                UPDATE StudyStartOrders SET DiwanTransactionId = NULL;
                """));

            migrationBuilder.DropForeignKey(name: "FK_StudyApprovals_StudyDiwanTransactions_DiwanTransactionId", table: "StudyApprovals");
            migrationBuilder.DropForeignKey(name: "FK_StudyAttachments_StudyDiwanTransactions_DiwanTransactionId", table: "StudyAttachments");
            migrationBuilder.DropForeignKey(name: "FK_StudyStartOrders_StudyDiwanTransactions_DiwanTransactionId", table: "StudyStartOrders");
            migrationBuilder.DropIndex(name: "IX_StudyApprovals_DiwanTransactionId_SignerRole", table: "StudyApprovals");
            migrationBuilder.DropIndex(name: "IX_StudyAttachments_DiwanTransactionId", table: "StudyAttachments");
            migrationBuilder.DropIndex(name: "IX_StudyStartOrders_DiwanTransactionId", table: "StudyStartOrders");
            migrationBuilder.DropCheckConstraint(name: "CK_StudyAttachments_OneOwner", table: "StudyAttachments");
            migrationBuilder.DropCheckConstraint(name: "CK_StudyApprovals_OneOwner", table: "StudyApprovals");
            migrationBuilder.DropColumn(name: "DiwanTransactionId", table: "StudyApprovals");
            migrationBuilder.DropColumn(name: "DiwanTransactionId", table: "StudyAttachments");
            migrationBuilder.DropColumn(name: "DiwanTransactionId", table: "StudyStartOrders");
            migrationBuilder.DropTable(name: "StudyDiwanTransactions");

            migrationBuilder.AddCheckConstraint(name: "CK_StudyAttachments_OneOwner", table: "StudyAttachments", sql: AttachmentsCheck + " = 1");
            migrationBuilder.AddCheckConstraint(name: "CK_StudyApprovals_OneOwner", table: "StudyApprovals", sql: ApprovalsCheck + " = 1");
            migrationBuilder.CreateIndex(name: "IX_StudyStartOrders_ResponseId_DiwanReplyId", table: "StudyStartOrders", columns: new[] { "ResponseId", "DiwanReplyId" });
            migrationBuilder.AddForeignKey(name: "FK_StudyStartOrders_StudyDiwanReplies_ResponseId_DiwanReplyId", table: "StudyStartOrders",
                columns: new[] { "ResponseId", "DiwanReplyId" }, principalTable: "StudyDiwanReplies", principalColumns: new[] { "ResponseId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }
    }
}
