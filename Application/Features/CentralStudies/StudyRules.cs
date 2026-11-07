using Application.Common;
using Domain.Entities;
using Domain.Entities.CentralStudiesBranch;

namespace Application.Features.CentralStudies
{
    /// <summary>أسماء صلاحيات فرع الدراسات التي تفحصها الـ handlers (تعريفها ووصفها في AppPermissions.All)</summary>
    public static class StudyPermissions
    {
        public const string ViewLookups = "ViewStudyLookups";
        public const string ViewRequests = "ViewStudyRequests";
        public const string EditRequest = "EditStudyRequest";
        public const string ViewAssignments = "ViewStudyAssignments";
        public const string EditAssignment = "EditStudyAssignment";
        public const string ViewResponses = "ViewStudyResponses";
        public const string EditResponse = "EditStudyResponse";
        public const string ViewStartOrders = "ViewStudyStartOrders";
        public const string EditStartOrder = "EditStudyStartOrder";
        public const string CancelDocuments = "CancelStudyDocuments";
        public const string ViewProjects = "ViewStudyProjects";
        public const string EditProject = "EditStudyProject";
        public const string UpdateTaskProgress = "UpdateStudyTaskProgress";
        public const string ViewDrawings = "ViewStudyDrawings";
        public const string EditDrawing = "EditStudyDrawing";
        public const string EditVersion = "EditArchitecturalVersion";
        public const string ViewActivityLog = "ViewStudyActivityLog";
        public const string EditResponsePlanning = "EditStudyResponsePlanning";
        public const string PrintDocuments = "PrintStudyDocuments";
    }

    /// <summary>
    /// قواعد فرع الدراسات المركزية — المصدر الوحيد: الأسماء العربية، الترقيم، التواقيع المطلوبة لكل مستند وصلاحية كل صفة،
    /// حدود السجلات (فرع الدراسة)، ونسب التقدم.
    /// </summary>
    public static class StudyRules
    {
        public const int MaxPageSize = 100;
        public const string OutOfScope = "هذا السجل خارج حدود فرعك";

        public static (int Page, int PageSize) NormalizePaging(int page, int pageSize) =>
            (Math.Max(1, page), Math.Clamp(pageSize <= 0 ? 20 : pageSize, 1, MaxPageSize));

        // ════════════════════ الحدود ════════════════════

        /// <summary>null = كل الفروع (نظرة المؤسسة)، وإلا فرع المستخدم (0 = بلا فرع → لا نتائج)</summary>
        public static int? BranchLimit(Viewer viewer) => StructureScope.BranchOf(viewer);

        public static bool InScope(Viewer viewer, int branchId) => StructureScope.Includes(viewer, branchId);

        public static void EnsureInScope(Viewer viewer, int branchId) =>
            StructureScope.EnsureIncludes(viewer, branchId, OutOfScope);

        public static void Require(Viewer viewer, string permission, string message = "لا تملك صلاحية تنفيذ هذا الإجراء")
        {
            if (!viewer.Has(permission)) throw new UnauthorizedAccessException(message);
        }

        public static bool Can(Viewer viewer, string permission, int branchId) => viewer.Has(permission) && InScope(viewer, branchId);

        /// <summary>
        /// موظف نشط من فرع الدراسة (مدير المشروع المقترح، مدير المشروع، المهندسون) — يطابق قائمة StudyLookups/Employees.
        /// who: «مدير المشروع» / «أحد المهندسين»…
        /// </summary>
        public static void EnsureBranchEmployee(User? user, int branchId, string who)
        {
            if (user is not { IsActive: true }) throw new KeyNotFoundException($"{who} غير موجود أو غير نشط");
            if (user.BranchId != branchId) throw new InvalidOperationException($"{who} ليس من موظفي فرع الدراسة");
        }

        /// <summary>المستخدم في القسم نفسه (مدير النظام بلا حدّ)</summary>
        public static bool InDepartment(Viewer viewer, int? departmentId) =>
            viewer.IsSuperAdmin || (departmentId != null && viewer.User.DepartmentId == departmentId);

        // ════════════════════ حالات المستند ════════════════════

        /// <summary>يُعدَّل المستند وهو مسودة أو بعد رفضه (التعديل يعيده مسودة)</summary>
        public static bool IsEditable(StudyDocumentStatus status) =>
            status is StudyDocumentStatus.Draft or StudyDocumentStatus.Rejected;

        public static void EnsureEditable(StudyDocumentStatus status)
        {
            if (!IsEditable(status))
                throw new InvalidOperationException("لا يُعدَّل المستند إلا وهو مسودة أو بعد رفضه");
        }

        public static void EnsureDraftForDelete(StudyDocumentStatus status)
        {
            if (status != StudyDocumentStatus.Draft)
                throw new InvalidOperationException("لا يُحذف إلا المستند الذي ما زال مسودة — المستند الرسمي يُلغى ولا يُحذف");
        }

        public static void EnsureApproved(StudyDocumentStatus status, string what)
        {
            if (status != StudyDocumentStatus.Approved)
                throw new InvalidOperationException($"{what} لم يُعتمد بعد");
        }

        // ════════════════════ الترقيم ════════════════════

        /// <summary>بادئة رقم إشعار الرد — مؤقتة حتى تصل ترويسة النموذج الورقي (تغييرها هنا فقط يغيّر الأرقام الجديدة)</summary>
        public const string ResponseNumberPrefix = "RSP";

        public static string NumberPrefix(StudyDocumentType type) => type switch
        {
            StudyDocumentType.Request => "REQ",
            StudyDocumentType.Assignment => "IPA",
            StudyDocumentType.Response => ResponseNumberPrefix,
            StudyDocumentType.StartOrder => "PIO",
            StudyDocumentType.DesignApproval => "CDA",
            _ => "DOC"
        };

        /// <summary>IPA-26-0001: النوع - آخر رقمين من السنة - الرقم المتسلسل</summary>
        public static string FormatNumber(StudyDocumentType type, int year, int number) =>
            $"{NumberPrefix(type)}-{year % 100:D2}-{number:D4}";

        /// <summary>التاريخ الهجري (أم القرى) للطباعة بقاعدة طباعة الإجازة نفسها: yyyy/MM/dd</summary>
        public static string Hijri(DateTime date)
        {
            var calendar = new System.Globalization.UmAlQuraCalendar();
            var shifted = date.Date.AddDays(Vacations.VacationRules.HijriDayOffset);
            return $"{calendar.GetYear(shifted):D4}/{calendar.GetMonth(shifted):D2}/{calendar.GetDayOfMonth(shifted):D2}";
        }

        public static string VersionLabel(int versionNumber) => $"Draft-{versionNumber}";

        // ════════════════════ التواقيع ════════════════════

        /// <summary>الصفات المطلوبة بالترتيب لكل مستند يُعتمد (الطلب يُسجَّل مباشرة بلا تواقيع)</summary>
        public static IReadOnlyList<StudySignerRole> RequiredSigners(StudyEntityType owner) => owner switch
        {
            StudyEntityType.Assignment => [StudySignerRole.BranchHead],
            StudyEntityType.Response => [StudySignerRole.FollowingDepartment, StudySignerRole.PlanningAndProjects],
            StudyEntityType.StartOrder => [StudySignerRole.CompetentDepartmentHead, StudySignerRole.BranchHead],
            StudyEntityType.DesignApproval => [StudySignerRole.SendingDepartment, StudySignerRole.DesignManagement, StudySignerRole.QualityControl],
            StudyEntityType.Project => [StudySignerRole.ProjectManagement, StudySignerRole.BranchHead],
            _ => []
        };

        /// <summary>سطور التوقيع المعلّقة لمستند (مرتبة)</summary>
        public static List<StudyApproval> BuildApprovalRows(StudyEntityType owner, int ownerId) =>
            RequiredSigners(owner).Select((role, i) =>
            {
                var row = new StudyApproval { SignerRole = role, SortOrder = i + 1, Decision = StudyDecision.Pending };
                switch (owner)
                {
                    case StudyEntityType.Assignment: row.AssignmentId = ownerId; break;
                    case StudyEntityType.Response: row.ResponseId = ownerId; break;
                    case StudyEntityType.StartOrder: row.StartOrderId = ownerId; break;
                    case StudyEntityType.DesignApproval: row.DesignApprovalId = ownerId; break;
                    case StudyEntityType.Project: row.ProjectId = ownerId; break;
                    default: throw new ArgumentException("هذا السجل لا يُعتمد بالتواقيع", nameof(owner));
                }
                return row;
            }).ToList();

        /// <summary>صلاحية التوقيع لكل صفة — حدّها في وصفها (AppPermissions)</summary>
        public static string SignPermission(StudySignerRole role) => role switch
        {
            StudySignerRole.FollowingDepartment => "SignStudyAsFollowingDepartment",
            StudySignerRole.PlanningAndProjects => "SignStudyAsPlanning",
            StudySignerRole.CompetentDepartmentHead => "SignStudyAsCompetentDepartment",
            StudySignerRole.BranchHead => "SignStudyAsBranchHead",
            StudySignerRole.SendingDepartment => "SignStudyAsSendingDepartment",
            StudySignerRole.DesignManagement => "SignStudyAsDesignManagement",
            StudySignerRole.QualityControl => "SignStudyAsQualityControl",
            StudySignerRole.ProjectManagement => "SignStudyAsProjectManagement",
            _ => throw new ArgumentOutOfRangeException(nameof(role))
        };

        /// <summary>صفات يحدّها قسم المستند (القسم المتابع/المختص/المرسل)، والباقي يحدّه فرع الدراسة</summary>
        public static bool IsDepartmentRole(StudySignerRole role) =>
            role is StudySignerRole.FollowingDepartment or StudySignerRole.CompetentDepartmentHead or StudySignerRole.SendingDepartment;

        public static bool IsPositive(StudyDecision decision) =>
            decision is StudyDecision.Approved or StudyDecision.ApprovedWithNotes;

        // ════════════════════ نسب التقدم (تُحسب ولا تُخزَّن) ════════════════════

        /// <summary>متوسط موزون؛ إن كانت كل الأوزان صفراً فمتوسط بسيط</summary>
        public static decimal Weighted(IEnumerable<(decimal Value, decimal Weight)> items)
        {
            var list = items.ToList();
            if (list.Count == 0) return 0;
            var totalWeight = list.Sum(x => x.Weight);
            var value = totalWeight > 0
                ? list.Sum(x => x.Value * x.Weight) / totalWeight
                : list.Average(x => x.Value);
            return Math.Round(Math.Clamp(value, 0, 100), 1);
        }

        public static decimal StageProgress(ProjectStage stage) =>
            stage.Status == StudyStageStatus.Closed ? 100 : Weighted(stage.Tasks.Select(t => (t.Progress, t.Weight)));

        public static decimal ProjectProgress(IEnumerable<ProjectStage> stages) =>
            Weighted(stages.Select(s => (StageProgress(s), s.Weight)));

        // ════════════════════ الأسماء العربية ════════════════════

        public static string Name(StudyDocumentStatus s) => s switch
        {
            StudyDocumentStatus.Draft => "مسودة",
            StudyDocumentStatus.PendingApproval => "بانتظار الاعتماد",
            StudyDocumentStatus.Approved => "معتمد",
            StudyDocumentStatus.Rejected => "مرفوض",
            StudyDocumentStatus.Cancelled => "ملغى",
            _ => s.ToString()
        };

        public static string Name(StudyUrgency s) => s == StudyUrgency.Urgent ? "مستعجل" : "عادي";

        public static string Name(StudyWorkType s) => s == StudyWorkType.Construction ? "إنشاء" : "صيانة وترميم";

        public static string Name(StudyPriority s) => s switch
        {
            StudyPriority.TopSecretUrgent => "عاجل جداً وسري",
            StudyPriority.Urgent => "عاجل",
            _ => "عادي"
        };

        public static string Name(StudyRecommendation s) => s switch
        {
            StudyRecommendation.Accept => "قبول",
            StudyRecommendation.Modify => "تعديل",
            _ => "غير متاح"
        };

        public static string Name(StudyBuildingType s) => s == StudyBuildingType.Rehabilitation ? "إعادة تأهيل" : "إنشاء جديد";

        public static string Name(StudyProjectStatus s) => s switch
        {
            StudyProjectStatus.Running => "جارٍ",
            StudyProjectStatus.Suspended => "معلّق",
            StudyProjectStatus.Completed => "مكتمل",
            StudyProjectStatus.Cancelled => "ملغى",
            _ => s.ToString()
        };

        public static string Name(StudyStageStatus s) => s switch
        {
            StudyStageStatus.NotStarted => "لم تبدأ",
            StudyStageStatus.InProgress => "جارية",
            _ => "مغلقة"
        };

        public static string Name(StudyTaskStatus s) => s switch
        {
            StudyTaskStatus.NotStarted => "لم تبدأ",
            StudyTaskStatus.InProgress => "قيد التنفيذ",
            _ => "منجزة"
        };

        public static string Name(StudyTaskTarget s) => s switch
        {
            StudyTaskTarget.Department => "قسم",
            StudyTaskTarget.Office => "مكتب",
            _ => "موظف"
        };

        public static string Name(DrawingKind s) => s switch
        {
            DrawingKind.Architectural => "معماري",
            DrawingKind.Structural => "إنشائي",
            DrawingKind.Mep => "كهرباء وميكانيك",
            DrawingKind.Interior => "تصميم داخلي",
            DrawingKind.Technical => "تقني",
            _ => s.ToString()
        };

        public static string Name(DrawingStatus s) => s switch
        {
            DrawingStatus.InPreparation => "قيد الإعداد",
            DrawingStatus.PendingReview => "بانتظار المراجعة",
            DrawingStatus.Approved => "معتمد",
            _ => "مرفوض"
        };

        public static string Name(StudyDecision s) => s switch
        {
            StudyDecision.Pending => "بانتظار القرار",
            StudyDecision.Approved => "معتمد",
            StudyDecision.ApprovedWithNotes => "معتمد مع ملاحظات",
            _ => "مرفوض"
        };

        public static string Name(StudySignerRole s) => s switch
        {
            StudySignerRole.FollowingDepartment => "القسم المتابع",
            StudySignerRole.PlanningAndProjects => "التخطيط وإدارة المشاريع",
            StudySignerRole.CompetentDepartmentHead => "رئيس القسم المختص",
            StudySignerRole.BranchHead => "رئيس الفرع",
            StudySignerRole.SendingDepartment => "القسم المرسل",
            StudySignerRole.DesignManagement => "إدارة التصميم",
            StudySignerRole.QualityControl => "ضبط الجودة",
            _ => "إدارة المشاريع"
        };

        public static string Name(StudyLinkedDocument s) => s switch
        {
            StudyLinkedDocument.Request => "طلب تجهيز الدراسة",
            StudyLinkedDocument.Assignment => "التكليف الداخلي",
            StudyLinkedDocument.Response => "إشعار الرد",
            StudyLinkedDocument.StartOrder => "أمر البدء",
            _ => "لا شيء"
        };

        public static string Name(StudyEntityType s) => s switch
        {
            StudyEntityType.Request => "طلب تجهيز دراسة",
            StudyEntityType.Assignment => "تكليف داخلي",
            StudyEntityType.Response => "إشعار رد",
            StudyEntityType.StartOrder => "أمر بدء",
            StudyEntityType.Project => "مشروع",
            StudyEntityType.Stage => "مرحلة",
            StudyEntityType.Task => "مهمة",
            StudyEntityType.Drawing => "مخطط",
            StudyEntityType.Version => "نسخة معمارية",
            StudyEntityType.DesignApproval => "اعتماد مخططات أولي",
            StudyEntityType.Attachment => "مرفق",
            StudyEntityType.ProjectDepartment => "قسم مشارك",
            StudyEntityType.ProjectMember => "عضو فريق",
            _ => s.ToString()
        };

        public static string Name(StudyLogAction s) => s switch
        {
            StudyLogAction.Created => "إنشاء",
            StudyLogAction.Edited => "تعديل",
            StudyLogAction.Submitted => "إرسال للاعتماد",
            StudyLogAction.Approved => "اعتماد",
            StudyLogAction.Rejected => "رفض",
            StudyLogAction.Cancelled => "إلغاء",
            StudyLogAction.Closed => "إغلاق",
            StudyLogAction.Deleted => "حذف",
            StudyLogAction.Reopened => "إعادة فتح",
            _ => s.ToString()
        };
    }
}
