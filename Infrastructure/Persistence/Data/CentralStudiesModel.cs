using Domain.Entities.CentralStudiesBranch;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Data
{
    /// <summary>
    /// تكوين جداول فرع الدراسات المركزية (Central Studies Branch).
    /// القاعدة: Restrict في كل العلاقات — المستندات الرسمية تُلغى ولا تُحذف، وحراسة الحذف برسالة عربية في الـ handlers؛
    /// Cascade للأجزاء البحتة فقط (أجزاء الرد وأمر البدء، أقسام المشروع وأعضاؤه، المراحل ومهامها، التواقيع والمرفقات وسجل الدراسة).
    /// </summary>
    internal static class CentralStudiesModel
    {
        private const string OneOwner = "(CASE WHEN [{0}] IS NULL THEN 0 ELSE 1 END)";

        private static string ExactlyOne(params string[] columns) =>
            string.Join(" + ", columns.Select(c => string.Format(OneOwner, c))) + " = 1";

        public static void Configure(ModelBuilder builder)
        {
            // ==================== المرجعيات ====================
            builder.Entity<StudyStageType>(e =>
            {
                e.Property(x => x.Name).HasMaxLength(100).IsRequired();
                e.Property(x => x.DefaultWeight).HasPrecision(5, 2);
                e.Property(x => x.LinkedDocument).HasConversion<int>();
                e.Property(x => x.RowVersion).IsRowVersion();
                e.HasIndex(x => x.Name).IsUnique();
                e.ToTable(t => t.HasCheckConstraint("CK_StudyStageTypes_Weight", "[DefaultWeight] >= 0 AND [DefaultWeight] <= 100"));
            });

            builder.Entity<StudyAttachmentType>(e =>
            {
                e.Property(x => x.Name).HasMaxLength(100).IsRequired();
                e.HasIndex(x => x.Name).IsUnique();
            });

            builder.Entity<EngineeringDiscipline>(e =>
            {
                e.Property(x => x.Name).HasMaxLength(100).IsRequired();
                e.HasIndex(x => x.Name).IsUnique();
            });

            // ==================== الطلب ====================
            builder.Entity<StudyRequest>(e =>
            {
                e.Property(x => x.RequestNumber).HasMaxLength(30).IsRequired();
                e.Property(x => x.RequestDate).HasColumnType("date");
                e.Property(x => x.Urgency).HasConversion<int>();
                e.Property(x => x.MinistryArchiveNumber).HasMaxLength(50);
                e.Property(x => x.MinistryArchiveDate).HasColumnType("date");
                e.Property(x => x.AdminArchiveNumber).HasMaxLength(50);
                e.Property(x => x.AdminArchiveDate).HasColumnType("date");
                e.Property(x => x.BeneficiaryArchiveNumber).HasMaxLength(50);
                e.Property(x => x.AddedDate).HasColumnType("date");
                e.Property(x => x.EntityCode).HasMaxLength(30);
                e.Property(x => x.MainBeneficiary).HasMaxLength(200).IsRequired();
                e.Property(x => x.SubBeneficiary).HasMaxLength(200);
                e.Property(x => x.GovernorateCode).HasMaxLength(4).IsRequired();
                e.Property(x => x.Area).HasMaxLength(100);
                e.Property(x => x.CityOrVillage).HasMaxLength(100);
                e.Property(x => x.PropertyNature).HasMaxLength(100);
                e.Property(x => x.WorkType).HasConversion<int>();
                e.Property(x => x.FollowerName).HasMaxLength(150);
                e.Property(x => x.FollowerPhone).HasMaxLength(20);
                e.Property(x => x.Notes).HasMaxLength(2000);
                e.Property(x => x.Status).HasConversion<int>();
                e.Property(x => x.RowVersion).IsRowVersion();

                e.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.Site).WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);

                e.HasIndex(x => x.RequestNumber).IsUnique();
                e.HasIndex(x => new { x.BranchId, x.Status, x.CreatedAt });
                e.HasIndex(x => x.MainBeneficiary);
            });

            // ==================== التكليف الداخلي (IPA) ====================
            builder.Entity<StudyAssignment>(e =>
            {
                e.Property(x => x.AssignmentNumber).HasMaxLength(30).IsRequired();
                e.Property(x => x.AssignmentDate).HasColumnType("date");
                e.Property(x => x.ProjectCode).HasMaxLength(50);
                e.Property(x => x.AllocatedBudget).HasPrecision(18, 2);
                e.Property(x => x.ScopeSummary).HasMaxLength(4000).IsRequired();
                e.Property(x => x.ReferralDate).HasColumnType("date");
                e.Property(x => x.ResponseDeadline).HasColumnType("date");
                e.Property(x => x.Priority).HasConversion<int>();
                e.Property(x => x.Status).HasConversion<int>();
                e.Property(x => x.RowVersion).IsRowVersion();

                // التكليف واحد لكل طلب: مفتاح أجنبي فريد = علاقة 1:1
                e.HasOne(x => x.Request).WithOne(r => r.Assignment).HasForeignKey<StudyAssignment>(x => x.RequestId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.FollowingDepartment).WithMany().HasForeignKey(x => x.FollowingDepartmentId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);

                e.HasIndex(x => x.AssignmentNumber).IsUnique();
                e.HasIndex(x => x.ProjectCode).IsUnique().HasFilter("[ProjectCode] IS NOT NULL");
                e.HasIndex(x => new { x.Status, x.CreatedAt });
            });

            // ==================== إشعار الرد وأجزاؤه ====================
            builder.Entity<StudyAssignmentResponse>(e =>
            {
                e.Property(x => x.ResponseDate).HasColumnType("date");
                e.Property(x => x.Recommendation).HasConversion<int>();
                e.Property(x => x.Status).HasConversion<int>();
                e.Property(x => x.RowVersion).IsRowVersion();

                e.HasOne(x => x.Assignment).WithOne(a => a.Response).HasForeignKey<StudyAssignmentResponse>(x => x.AssignmentId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.ProposedManager).WithMany().HasForeignKey(x => x.ProposedManagerUserId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
                e.HasIndex(x => new { x.Status, x.CreatedAt });
            });

            builder.Entity<ResponseBuilding>(e =>
            {
                // سطر لكل نوع بناء: المفتاح (الرد، النوع) يمنع أكثر من سطرين
                e.HasKey(x => new { x.ResponseId, x.BuildingType });
                e.Property(x => x.BuildingType).HasConversion<int>();
                e.Property(x => x.BuildingsArea).HasPrecision(12, 2);
                e.Property(x => x.SiteArea).HasPrecision(12, 2);
                e.Property(x => x.InitialCost).HasPrecision(18, 2);
                e.HasOne<StudyAssignmentResponse>().WithMany(r => r.Buildings).HasForeignKey(x => x.ResponseId).OnDelete(DeleteBehavior.Cascade);
                e.ToTable(t => t.HasCheckConstraint("CK_ResponseBuildings_Count", "[BuildingCount] >= 0"));
            });

            builder.Entity<ResponseCostItem>(e =>
            {
                e.Property(x => x.Description).HasMaxLength(500).IsRequired();
                e.Property(x => x.Unit).HasMaxLength(30).IsRequired();
                e.Property(x => x.Quantity).HasPrecision(18, 3);
                e.Property(x => x.UnitPrice).HasPrecision(18, 2);
                e.HasOne<StudyAssignmentResponse>().WithMany(r => r.CostItems).HasForeignKey(x => x.ResponseId).OnDelete(DeleteBehavior.Cascade);
                e.HasIndex(x => new { x.ResponseId, x.SortOrder });
                e.ToTable(t => t.HasCheckConstraint("CK_ResponseCostItems_Values", "[Quantity] >= 0 AND [UnitPrice] >= 0"));
            });

            builder.Entity<ResponseStaffEstimate>(e =>
            {
                e.HasKey(x => new { x.ResponseId, x.DisciplineId });
                e.HasOne<StudyAssignmentResponse>().WithMany(r => r.StaffEstimates).HasForeignKey(x => x.ResponseId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Discipline).WithMany().HasForeignKey(x => x.DisciplineId).OnDelete(DeleteBehavior.Restrict);
                e.ToTable(t => t.HasCheckConstraint("CK_ResponseStaffEstimates_Count", "[StaffCount] >= 0"));
            });

            builder.Entity<ResponseLogisticItem>(e =>
            {
                e.Property(x => x.Item).HasMaxLength(200).IsRequired();
                e.Property(x => x.Quantity).HasPrecision(12, 2);
                e.Property(x => x.Note).HasMaxLength(500);
                e.HasOne<StudyAssignmentResponse>().WithMany(r => r.LogisticItems).HasForeignKey(x => x.ResponseId).OnDelete(DeleteBehavior.Cascade);
            });

            // ==================== أمر البدء (PIO) وأجزاؤه ====================
            builder.Entity<StudyStartOrder>(e =>
            {
                e.Property(x => x.OrderNumber).HasMaxLength(30).IsRequired();
                e.Property(x => x.OrderDate).HasColumnType("date");
                e.Property(x => x.StudyStartDate).HasColumnType("date");
                e.Property(x => x.StudyDeadline).HasColumnType("date");
                e.Property(x => x.Status).HasConversion<int>();
                e.Property(x => x.RowVersion).IsRowVersion();

                e.HasOne(x => x.Response).WithOne(r => r.StartOrder).HasForeignKey<StudyStartOrder>(x => x.ResponseId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.CompetentDepartment).WithMany().HasForeignKey(x => x.CompetentDepartmentId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.ProjectManager).WithMany().HasForeignKey(x => x.ProjectManagerUserId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);

                e.HasIndex(x => x.OrderNumber).IsUnique();
                e.HasIndex(x => new { x.Status, x.CreatedAt });
            });

            builder.Entity<StartOrderEngineer>(e =>
            {
                e.HasKey(x => new { x.StartOrderId, x.EngineerUserId });
                e.HasOne<StudyStartOrder>().WithMany(o => o.Engineers).HasForeignKey(x => x.StartOrderId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Engineer).WithMany().HasForeignKey(x => x.EngineerUserId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.Discipline).WithMany().HasForeignKey(x => x.DisciplineId).OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<StartOrderResource>(e =>
            {
                e.Property(x => x.Item).HasMaxLength(200).IsRequired();
                e.Property(x => x.Quantity).HasPrecision(12, 2);
                e.Property(x => x.Note).HasMaxLength(500);
                e.HasOne<StudyStartOrder>().WithMany(o => o.Resources).HasForeignKey(x => x.StartOrderId).OnDelete(DeleteBehavior.Cascade);
            });

            // ==================== المشروع ====================
            builder.Entity<StudyProject>(e =>
            {
                e.Property(x => x.Code).HasMaxLength(50).IsRequired();
                e.Property(x => x.Name).HasMaxLength(200).IsRequired();
                e.Property(x => x.PlannedStartDate).HasColumnType("date");
                e.Property(x => x.PlannedEndDate).HasColumnType("date");
                e.Property(x => x.ActualStartDate).HasColumnType("date");
                e.Property(x => x.ActualEndDate).HasColumnType("date");
                e.Property(x => x.Status).HasConversion<int>();
                e.Property(x => x.RowVersion).IsRowVersion();

                e.HasOne(x => x.StartOrder).WithOne(o => o.Project).HasForeignKey<StudyProject>(x => x.StartOrderId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.Manager).WithMany().HasForeignKey(x => x.ManagerUserId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);

                e.HasIndex(x => x.Code).IsUnique();
                e.HasIndex(x => new { x.BranchId, x.Status, x.CreatedAt });
            });

            builder.Entity<ProjectDepartment>(e =>
            {
                e.HasOne(x => x.Project).WithMany(p => p.Departments).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.AddedBy).WithMany().HasForeignKey(x => x.AddedByUserId).OnDelete(DeleteBehavior.Restrict);
                e.HasIndex(x => new { x.ProjectId, x.DepartmentId }).IsUnique();
            });

            builder.Entity<ProjectMember>(e =>
            {
                e.HasKey(x => new { x.ProjectDepartmentId, x.UserId });
                e.Property(x => x.RoleTitle).HasMaxLength(100);
                e.HasOne(x => x.ProjectDepartment).WithMany(d => d.Members).HasForeignKey(x => x.ProjectDepartmentId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.AssignedBy).WithMany().HasForeignKey(x => x.AssignedByUserId).OnDelete(DeleteBehavior.Restrict);
                e.HasIndex(x => x.UserId);
            });

            builder.Entity<ProjectStage>(e =>
            {
                e.Property(x => x.Weight).HasPrecision(5, 2);
                e.Property(x => x.PlannedStartDate).HasColumnType("date");
                e.Property(x => x.PlannedEndDate).HasColumnType("date");
                e.Property(x => x.ActualStartDate).HasColumnType("date");
                e.Property(x => x.ActualEndDate).HasColumnType("date");
                e.Property(x => x.Status).HasConversion<int>();
                e.Property(x => x.RowVersion).IsRowVersion();

                e.HasOne(x => x.Project).WithMany(p => p.Stages).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.StageType).WithMany().HasForeignKey(x => x.StageTypeId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.ResponsibleDepartment).WithMany().HasForeignKey(x => x.ResponsibleDepartmentId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.ClosedBy).WithMany().HasForeignKey(x => x.ClosedByUserId).OnDelete(DeleteBehavior.Restrict);

                // نوع المرحلة مرة واحدة لكل مشروع؛ الترتيب يُفرض في الكود (فهرس فريد عليه يعرقل إعادة الترتيب)
                e.HasIndex(x => new { x.ProjectId, x.StageTypeId }).IsUnique();
                e.HasIndex(x => new { x.ProjectId, x.SortOrder });
                e.ToTable(t => t.HasCheckConstraint("CK_ProjectStages_Weight", "[Weight] >= 0 AND [Weight] <= 100"));
            });

            builder.Entity<StageTask>(e =>
            {
                e.Property(x => x.TargetType).HasConversion<int>();
                e.Property(x => x.Title).HasMaxLength(200).IsRequired();
                e.Property(x => x.Description).HasMaxLength(4000);
                e.Property(x => x.PlannedStartDate).HasColumnType("date");
                e.Property(x => x.PlannedEndDate).HasColumnType("date");
                e.Property(x => x.ActualStartDate).HasColumnType("date");
                e.Property(x => x.ActualEndDate).HasColumnType("date");
                e.Property(x => x.Progress).HasPrecision(5, 2);
                e.Property(x => x.Weight).HasPrecision(5, 2);
                e.Property(x => x.Status).HasConversion<int>();
                e.Property(x => x.RowVersion).IsRowVersion();

                e.HasOne(x => x.Stage).WithMany(s => s.Tasks).HasForeignKey(x => x.StageId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.Office).WithMany().HasForeignKey(x => x.OfficeId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.Assignee).WithMany().HasForeignKey(x => x.AssigneeUserId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.AssignedBy).WithMany().HasForeignKey(x => x.AssignedByUserId).OnDelete(DeleteBehavior.Restrict);

                e.ToTable(t =>
                {
                    // عمود إسناد واحد فقط مملوء، ويطابق نوع الإسناد
                    t.HasCheckConstraint("CK_StageTasks_Target",
                        "([TargetType] = 1 AND [DepartmentId] IS NOT NULL AND [OfficeId] IS NULL AND [AssigneeUserId] IS NULL) OR " +
                        "([TargetType] = 2 AND [OfficeId] IS NOT NULL AND [DepartmentId] IS NULL AND [AssigneeUserId] IS NULL) OR " +
                        "([TargetType] = 3 AND [AssigneeUserId] IS NOT NULL AND [DepartmentId] IS NULL AND [OfficeId] IS NULL)");
                    t.HasCheckConstraint("CK_StageTasks_Progress", "[Progress] >= 0 AND [Progress] <= 100 AND [Weight] >= 0 AND [Weight] <= 100");
                });
            });

            // ==================== المخططات (TPH: جدول واحد بعمود Kind) ====================
            builder.Entity<ProjectDrawing>(e =>
            {
                e.ToTable("ProjectDrawings");
                e.HasDiscriminator(x => x.Kind)
                    .HasValue<ArchitecturalDrawing>(DrawingKind.Architectural)
                    .HasValue<StructuralDrawing>(DrawingKind.Structural)
                    .HasValue<MepDrawing>(DrawingKind.Mep)
                    .HasValue<InteriorDrawing>(DrawingKind.Interior)
                    .HasValue<TechnicalDrawing>(DrawingKind.Technical);
                e.Property(x => x.Kind).HasConversion<int>().HasColumnName("DrawingKind");
                e.Property(x => x.Name).HasMaxLength(200).IsRequired();
                e.Property(x => x.DrawingNumber).HasMaxLength(50).IsRequired();
                e.Property(x => x.DrawingDate).HasColumnType("date");
                e.Property(x => x.Description).HasMaxLength(2000);
                e.Property(x => x.Status).HasConversion<int>();
                e.Property(x => x.RowVersion).IsRowVersion();

                e.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.Office).WithMany().HasForeignKey(x => x.OfficeId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.PreparedBy).WithMany().HasForeignKey(x => x.PreparedByUserId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.BasedOnVersion).WithMany().HasForeignKey(x => x.BasedOnVersionId).OnDelete(DeleteBehavior.Restrict);

                e.HasIndex(x => new { x.ProjectId, x.DrawingNumber }).IsUnique();
                e.HasIndex(x => new { x.ProjectId, x.Kind });
                // «مبني على» للمخططات غير المعمارية فقط
                e.ToTable(t => t.HasCheckConstraint("CK_ProjectDrawings_BasedOn", "[DrawingKind] <> 1 OR [BasedOnVersionId] IS NULL"));
            });

            // النسخ للمخطط المعماري فقط: العلاقة مع الصنف المشتق ArchitecturalDrawing
            builder.Entity<ArchitecturalVersion>(e =>
            {
                e.Property(x => x.VersionDate).HasColumnType("date");
                e.Property(x => x.Content).HasMaxLength(4000);
                e.Property(x => x.Status).HasConversion<int>();
                e.Property(x => x.RowVersion).IsRowVersion();

                e.HasOne(x => x.Drawing).WithMany(d => d.Versions).HasForeignKey(x => x.DrawingId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
                e.HasIndex(x => new { x.DrawingId, x.VersionNumber }).IsUnique();
            });

            builder.Entity<DesignApproval>(e =>
            {
                e.Property(x => x.CdaNumber).HasMaxLength(30).IsRequired();
                e.Property(x => x.CdaDate).HasColumnType("date");
                e.Property(x => x.Content).HasMaxLength(4000);
                e.Property(x => x.TechnicalNotes).HasMaxLength(4000);
                e.Property(x => x.Decision).HasConversion<int>();
                e.Property(x => x.RowVersion).IsRowVersion();

                e.HasOne(x => x.Version).WithOne(v => v.DesignApproval).HasForeignKey<DesignApproval>(x => x.VersionId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
                e.HasIndex(x => x.CdaNumber).IsUnique();
            });

            // ==================== الاعتمادات (مالك واحد) ====================
            builder.Entity<StudyApproval>(e =>
            {
                e.Property(x => x.SignerRole).HasConversion<int>();
                e.Property(x => x.Decision).HasConversion<int>();
                e.Property(x => x.JobTitle).HasMaxLength(150);
                e.Property(x => x.Note).HasMaxLength(1000);
                e.Property(x => x.RowVersion).IsRowVersion();

                e.HasOne(x => x.Assignment).WithMany().HasForeignKey(x => x.AssignmentId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Response).WithMany().HasForeignKey(x => x.ResponseId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.StartOrder).WithMany().HasForeignKey(x => x.StartOrderId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.DesignApproval).WithMany().HasForeignKey(x => x.DesignApprovalId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Signer).WithMany().HasForeignKey(x => x.SignerUserId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.Signature).WithMany().HasForeignKey(x => x.SignatureId).OnDelete(DeleteBehavior.Restrict);

                // صفة الموقّع مرة واحدة لكل مستند
                e.HasIndex(x => new { x.AssignmentId, x.SignerRole }).IsUnique().HasFilter("[AssignmentId] IS NOT NULL");
                e.HasIndex(x => new { x.ResponseId, x.SignerRole }).IsUnique().HasFilter("[ResponseId] IS NOT NULL");
                e.HasIndex(x => new { x.StartOrderId, x.SignerRole }).IsUnique().HasFilter("[StartOrderId] IS NOT NULL");
                e.HasIndex(x => new { x.DesignApprovalId, x.SignerRole }).IsUnique().HasFilter("[DesignApprovalId] IS NOT NULL");
                e.HasIndex(x => new { x.ProjectId, x.SignerRole }).IsUnique().HasFilter("[ProjectId] IS NOT NULL");
                e.HasIndex(x => new { x.SignerRole, x.Decision });

                e.ToTable(t => t.HasCheckConstraint("CK_StudyApprovals_OneOwner",
                    ExactlyOne("AssignmentId", "ResponseId", "StartOrderId", "DesignApprovalId", "ProjectId")));
            });

            // ==================== المرفقات (مالك واحد) ====================
            builder.Entity<StudyAttachment>(e =>
            {
                e.Property(x => x.FileName).HasMaxLength(200).IsRequired();
                e.Property(x => x.ContentType).HasMaxLength(150).IsRequired();
                e.Property(x => x.StorageKey).HasMaxLength(80).IsRequired();

                e.HasOne(x => x.Request).WithMany().HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Assignment).WithMany().HasForeignKey(x => x.AssignmentId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Response).WithMany().HasForeignKey(x => x.ResponseId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.StartOrder).WithMany().HasForeignKey(x => x.StartOrderId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Drawing).WithMany().HasForeignKey(x => x.DrawingId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Version).WithMany().HasForeignKey(x => x.VersionId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.AttachmentType).WithMany().HasForeignKey(x => x.AttachmentTypeId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.UploadedBy).WithMany().HasForeignKey(x => x.UploadedByUserId).OnDelete(DeleteBehavior.Restrict);

                e.HasIndex(x => x.StorageKey).IsUnique();

                e.ToTable(t => t.HasCheckConstraint("CK_StudyAttachments_OneOwner",
                    ExactlyOne("RequestId", "AssignmentId", "ResponseId", "StartOrderId", "ProjectId", "DrawingId", "VersionId")));
            });

            // ==================== سجل الدراسة والترقيم ====================
            builder.Entity<StudyActivityLog>(e =>
            {
                e.Property(x => x.EntityType).HasConversion<int>();
                e.Property(x => x.Action).HasConversion<int>();
                e.Property(x => x.Title).HasMaxLength(200).IsRequired();
                e.HasOne(x => x.Request).WithMany().HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
                e.HasIndex(x => new { x.RequestId, x.CreatedAt });
            });

            builder.Entity<StudyNumberSequence>(e =>
            {
                e.HasKey(x => new { x.DocumentType, x.Year });
                e.Property(x => x.DocumentType).HasConversion<int>();
            });
        }
    }
}
