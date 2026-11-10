using System.Linq.Expressions;
using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;

namespace Application.Features.CentralStudies
{
    /// <summary>مستوى ما يراه المستخدم من تفاصيل مشروع (الأوسع أولاً)</summary>
    public enum StudyViewLevel
    {
        /// <summary>كل شيء (ViewFullStudyProject، أو مدير المشروع، أو مدير النظام)</summary>
        Full = 1,
        /// <summary>مهام قسمه ومراحله وفريقه (ViewDepartmentStudyTasks)</summary>
        Department = 2,
        /// <summary>مهام مكتبه وموظفيه وفريق مكتبه (ViewOfficeStudyTasks)</summary>
        Office = 3,
        /// <summary>مهامه فقط (المسندة إليه أو إلى مكتبه أو قسمه) وزملاء مكتبه</summary>
        Mine = 4
    }

    /// <summary>
    /// حدّ رؤية مشروع دراسة (2026-11-10): القاعدة الوحيدة لتفاصيل المشروع (المراحل والمهام والمخطط الزمني والفريق والتواقيع
    /// والمرفقات وسجل النشاط). حدّ الفرع يُفحص قبلها (EnsureInScope)، وهي تحدّد ما يراه المستخدم داخل مشاريع فرعه.
    /// لا يُستنتج المنصب من الدور ولا من الموقع: الصلاحيات الثلاث تُمنح من صفحة الأدوار، ومدير المشروع (حدّ سجل لا منصب) يراه كاملاً.
    /// </summary>
    public sealed class ProjectScope
    {
        public StudyViewLevel Level { get; }
        public bool IsFull => Level == StudyViewLevel.Full;
        public int ViewerId { get; }
        public int? DepartmentId { get; }
        public int? OfficeId { get; }

        private ProjectScope(StudyViewLevel level, int viewerId, int? departmentId, int? officeId)
        {
            Level = level;
            ViewerId = viewerId;
            DepartmentId = departmentId;
            OfficeId = officeId;
        }

        public static ProjectScope For(Viewer viewer, StudyProject project) => For(viewer, project.ManagerUserId);

        public static ProjectScope For(Viewer viewer, int managerUserId)
        {
            var departmentId = viewer.User.DepartmentId;
            var officeId = viewer.User.OfficeId;
            if (viewer.IsSuperAdmin || viewer.Has(StudyPermissions.ViewFullProject) || managerUserId == viewer.Id)
                return new(StudyViewLevel.Full, viewer.Id, departmentId, officeId);
            // القسم يغني عن المكتب؛ صلاحية بلا وحدة (قسم أو مكتب) لا تعطي شيئاً فيبقى «مهامي»
            if (viewer.Has(StudyPermissions.ViewDepartmentTasks) && departmentId != null)
                return new(StudyViewLevel.Department, viewer.Id, departmentId, officeId);
            if (viewer.Has(StudyPermissions.ViewOfficeTasks) && officeId != null)
                return new(StudyViewLevel.Office, viewer.Id, departmentId, officeId);
            return new(StudyViewLevel.Mine, viewer.Id, departmentId, officeId);
        }

        public static string LevelName(StudyViewLevel level) => level switch
        {
            StudyViewLevel.Full => "كاملة",
            StudyViewLevel.Department => "مهام القسم",
            StudyViewLevel.Office => "مهام المكتب",
            _ => "مهامي"
        };

        /// <summary>يملأ علمي الرؤية في تفاصيل المشروع ليخفي المستخدم في الواجهة ما لا يصل إليه</summary>
        public void Stamp(StudyProjectDto dto)
        {
            dto.CanViewFull = IsFull;
            dto.ViewLevel = (int)Level;
            dto.ViewLevelName = LevelName(Level);
        }

        // ════════════════════ المهام ════════════════════

        /// <summary>
        /// شرط المهام المرئية (يُنفَّذ في SQL)؛ null = كلها. «مهامي» = المسندة إليه، أو إلى مكتبه، أو إلى قسمه إن لم يكن له مكتب
        /// (قاعدة IsTarget نفسها). القسم يضيف مهام مراحل قسمه والمسندة إلى قسمه أو مكاتبه أو موظفيه، والمكتب يضيف مكتبه وموظفيه.
        /// </summary>
        public Expression<Func<StageTask, bool>>? TaskFilter()
        {
            var u = ViewerId;
            switch (Level)
            {
                case StudyViewLevel.Full:
                    return null;
                case StudyViewLevel.Department:
                {
                    var d = DepartmentId!.Value;
                    return t => t.Stage.ResponsibleDepartmentId == d || t.DepartmentId == d
                                || t.Office!.DepartmentId == d || t.Assignee!.DepartmentId == d;
                }
                case StudyViewLevel.Office:
                {
                    var o = OfficeId!.Value;
                    return t => t.OfficeId == o || t.Assignee!.OfficeId == o || t.AssigneeUserId == u;
                }
                default:
                    if (OfficeId is int office)
                        return t => (t.TargetType == StudyTaskTarget.User && t.AssigneeUserId == u)
                                    || (t.TargetType == StudyTaskTarget.Office && t.OfficeId == office);
                    if (DepartmentId is int dept)
                        return t => (t.TargetType == StudyTaskTarget.User && t.AssigneeUserId == u)
                                    || (t.TargetType == StudyTaskTarget.Department && t.DepartmentId == dept);
                    return t => t.TargetType == StudyTaskTarget.User && t.AssigneeUserId == u;
            }
        }

        // ════════════════════ المراحل ════════════════════

        /// <summary>مرحلة قسمه يراها كل موظفيه (وغيرها تظهر فقط إن كان فيها مهمة يراها — يحسبها المستودع)</summary>
        public bool OwnsStage(int? responsibleDepartmentId) =>
            IsFull || (DepartmentId != null && responsibleDepartmentId == DepartmentId);

        public bool CanSeeStage(int? responsibleDepartmentId, int stageId, ISet<int> stagesWithVisibleTasks) =>
            OwnsStage(responsibleDepartmentId) || stagesWithVisibleTasks.Contains(stageId);

        // ════════════════════ الفريق ════════════════════

        /// <summary>
        /// بالقسم: صف قسمه بكل أعضائه. بالمكتب أو بلا شيء: صف قسمه بأعضاء مكتبه فقط (وهو وحده إن لم يكن له مكتب).
        /// null = لا قسم له فلا يرى فريقاً.
        /// </summary>
        public TeamLimit? TeamLimit()
        {
            if (IsFull || DepartmentId is not int d) return null;
            return Level == StudyViewLevel.Department
                ? new TeamLimit(d, null, null)
                : new TeamLimit(d, OfficeId, OfficeId == null ? ViewerId : null);
        }

        // ════════════════════ التواقيع ════════════════════

        /// <summary>تواقيع إنجاز المشروع: لمن وقّع سطراً منها أو يملك صلاحية توقيع سطر منها (حدّ الفرع مفحوص قبلاً)</summary>
        public bool CanSeeApprovals(Viewer viewer, IEnumerable<StudyApprovalDto> approvals) =>
            IsFull || approvals.Any(a => a.SignerUserId == viewer.Id || viewer.Has(StudyRules.SignPermission((StudySignerRole)a.SignerRole)));

        // ════════════════════ المرفقات ════════════════════

        /// <summary>مرفق المشروع نفسه (لا مرفقات الوثائق ولا المخططات): لصاحب الرؤية الكاملة ومن رفعه</summary>
        public bool CanSeeProjectAttachment(int uploadedByUserId) => IsFull || uploadedByUserId == ViewerId;
    }
}
