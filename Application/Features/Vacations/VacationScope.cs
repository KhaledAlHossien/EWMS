using System.Linq.Expressions;
using Application.Common;
using Domain.Entities;

namespace Application.Features.Vacations
{
    /// <summary>
    /// حدّ عرض الإجازات لاستعلام (التقارير) — الشرط نفسه في VacationAccess.CanView:
    /// مدير النظام كل الإجازات، وغيره إجازاته + فرعه (ViewBranchVacations أو صلاحية موافقة) + قسمه (ViewDepartmentVacations).
    /// </summary>
    public sealed record VacationScope(bool All, int OwnerId, int? BranchId, int? DepartmentId)
    {
        public static VacationScope For(Viewer viewer) => viewer.IsSuperAdmin
            ? new(true, viewer.Id, null, null)
            : new(false, viewer.Id,
                VacationAccess.CanSeeBranch(viewer, viewer.User.BranchId) ? viewer.User.BranchId : null,
                VacationAccess.CanSeeDepartment(viewer, viewer.User.DepartmentId) ? viewer.User.DepartmentId : null);

        /// <summary>يرى إجازات غيره (فرعاً أو قسماً أو الكل)، لا إجازاته فقط</summary>
        public bool SeesOthers => All || BranchId != null || DepartmentId != null;

        public Expression<Func<Vacation, bool>> Predicate()
        {
            if (All) return v => true;
            var (owner, branch, department) = (OwnerId, BranchId, DepartmentId);
            return v => v.UserId == owner
                || (branch != null && v.BranchId == branch)
                || (department != null && v.DepartmentId == department);
        }

        public bool IncludesDepartment(int departmentId, int branchId) =>
            All || branchId == BranchId || departmentId == DepartmentId;
    }
}
