using System.Linq.Expressions;
using Application.Common;
using Domain.Entities;

namespace Application.Features.Projects
{
    /// <summary>
    /// المشاريع وإضبارتها (2026-10-27، من تحليل الجنوبية) — مصدر واحد لحدود الرؤية والأسماء:
    /// - الحدّ ثابت في الكود لكل صلاحية: «مشاريعي» (مشارك أو منشئ) ثم قسمي (يعالجه قسمي أو فيه مشارك من قسمي) ثم فرعي، والمؤسسة كلها لنظرة المؤسسة.
    /// - إحالة المشروع بين الأقسام لا تفتحه لكل مستخدمي القسم الجديد: يراه من أُضيف مشاركاً، أو من يملك ViewDepartmentProjects/ViewBranchProjects (القاعدة 8).
    /// - الوثائق والتدقيق والتقرير تتبع حدّ المشاريع التي أراها: الدخول إليها يمرّ من <see cref="Visible"/> دائماً.
    /// </summary>
    public static class ProjectRules
    {
        public const int MaxMembers = 100;

        // ───────── الأسماء ─────────

        /// <summary>الرقم الداخلي PRJ-{سنة الإنشاء}-{id:D5} — يُحسب ولا يُخزَّن (كطلبات الصيانة والإجازات)</summary>
        public static string Number(int id, DateTime createdAt) => $"PRJ-{createdAt.Year}-{id:D5}";

        public static string StageAr(ProjectStage stage) => stage switch
        {
            ProjectStage.Study => "دراسة",
            ProjectStage.Contracting => "تعاقد",
            ProjectStage.Execution => "تنفيذ",
            ProjectStage.Handover => "استلام",
            ProjectStage.Warranty => "ضمان",
            ProjectStage.Closed => "مغلق",
            ProjectStage.Cancelled => "ملغى",
            _ => stage.ToString()
        };

        public static string KindAr(ProjectDocumentKind kind) => kind switch
        {
            ProjectDocumentKind.StudyOrder => "أمر الدراسة",
            ProjectDocumentKind.Correspondence => "مراسلة",
            ProjectDocumentKind.SurveyAndModels => "مساحة ونمذجة",
            ProjectDocumentKind.ArchitecturalDrawing => "مخطط معماري",
            ProjectDocumentKind.StructuralDrawing => "مخطط إنشائي",
            ProjectDocumentKind.ElectricalDrawing => "مخطط كهربائي",
            ProjectDocumentKind.MechanicalDrawing => "مخطط ميكانيكي",
            ProjectDocumentKind.Quantities => "كميات وتدقيق",
            ProjectDocumentKind.Planning => "تخطيط",
            ProjectDocumentKind.ApprovalDecision => "قرار اعتماد",
            ProjectDocumentKind.Contract => "عقد أو ملحق",
            ProjectDocumentKind.StartOrder => "أمر مباشرة",
            ProjectDocumentKind.Schedule => "جدول زمني",
            ProjectDocumentKind.DailyWeeklyReport => "تقرير يومي أو أسبوعي",
            ProjectDocumentKind.QualityReport => "تقرير جودة",
            ProjectDocumentKind.ContractorEvaluation => "تقييم مقاول",
            ProjectDocumentKind.Pricing => "تسعير وعروض أسعار",
            ProjectDocumentKind.Invoice => "فاتورة أو كشف",
            ProjectDocumentKind.HandoverMinutes => "محضر استلام أو تسليم",
            ProjectDocumentKind.Warranty => "وثيقة ضمان",
            ProjectDocumentKind.ClosureDecision => "قرار إغلاق",
            _ => "أخرى"
        };

        public static bool IsValidKind(int value) => Enum.IsDefined(typeof(ProjectDocumentKind), value);
        public static bool IsValidStage(int value) => Enum.IsDefined(typeof(ProjectStage), value);

        /// <summary>مشروع مغلق أو ملغى: لا تعديل على بياناته ولا إضافة وثائق إلا بإعادة فتحه بتغيير مرحلته</summary>
        public static bool IsFinished(ProjectStage stage) => stage is ProjectStage.Closed or ProjectStage.Cancelled;

        /// <summary>الرقم المرجعي بلا فراغات زائدة؛ الفارغ يصير null (الفهرس الفريد يتجاهل null)</summary>
        public static string? CleanReference(string? reference)
        {
            var value = (reference ?? string.Empty).Trim();
            return value.Length == 0 ? null : value;
        }

        // ───────── الحدود ─────────

        /// <summary>
        /// شرط رؤية المشروع للمستخدم، قابل للترجمة إلى SQL. يعتمد على المشاركين وقسم كل مشارك،
        /// فمن يستعمله في الذاكرة يحمّل <c>Members</c> مع <c>User</c>. ومن لا يملك أي صلاحية مشاريع لا يرى شيئاً.
        /// </summary>
        public static Expression<Func<Project, bool>> Visible(Viewer viewer)
        {
            if (viewer.IsOrganizationWide) return p => true;

            var userId = viewer.Id;
            int? branchId = viewer.Has(AppPermissions.ViewBranchProjects) ? viewer.User.BranchId : null;
            int? departmentId = viewer.Has(AppPermissions.ViewDepartmentProjects) ? viewer.User.DepartmentId : null;
            // «مشاريعي»: لمن يملك العرض الشخصي أو ينشئ المشاريع أو يعدّلها (يرى ما أنشأه وما أُضيف إليه)
            var mine = viewer.Has(AppPermissions.ViewProjects) || viewer.Has(AppPermissions.CreateProject) || viewer.Has(AppPermissions.EditProject);

            return p =>
                (branchId != null && p.BranchId == branchId)
                || (departmentId != null && (p.DepartmentId == departmentId || p.Members.Any(m => m.User.DepartmentId == departmentId)))
                || (mine && (p.CreatedByUserId == userId || p.Members.Any(m => m.UserId == userId)));
        }

        public static bool CanView(Viewer viewer, Project project) => Visible(viewer).Compile()(project);

        /// <summary>يرفض بـ 403 إن لم يكن المشروع ضمن حدّ المستخدم (المشروع المحمَّل بمشاركيه ومستخدميهم)</summary>
        public static void EnsureView(Viewer viewer, Project project)
        {
            if (!CanView(viewer, project))
                throw new UnauthorizedAccessException("هذا المشروع خارج صلاحياتك");
        }

        /// <summary>المشروع الجديد يُنشأ في فرع المنشئ (والمؤسسة كلها لنظرة المؤسسة)</summary>
        public static void EnsureCanCreateIn(Viewer viewer, int branchId)
        {
            if (!viewer.IsOrganizationWide && !OrganizationRole.InBranch(viewer.User, branchId))
                throw new UnauthorizedAccessException("لا تستطيع إنشاء مشروع في فرع آخر");
        }

        /// <summary>تعديل مشروع مغلق أو ملغى مرفوض (يُعاد فتحه بتغيير المرحلة أولاً)</summary>
        public static void EnsureOpen(Project project)
        {
            if (IsFinished(project.Stage))
                throw new InvalidOperationException($"المشروع {StageAr(project.Stage)}: غيّر مرحلته أولاً لإعادة فتحه");
        }
    }
}
