using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;

namespace Application.Features.CentralStudies.Stages
{
    /// <summary>الدوال المشتركة بين عمليات Stages (تحميل السجل، التحويل إلى DTO، الأعلام…) — منقولة من المعالج الواحد القديم</summary>
    public class StudyStageSupport
    {
        private readonly IStudyStageService _service;
        private readonly IStudyProjectService _projects;
        private readonly IDepartmentService _departments;
        private readonly IOfficeService _offices;
        private readonly StudyKit _kit;

        public StudyStageSupport(IStudyStageService service, IStudyProjectService projects, IDepartmentService departments, IOfficeService offices, StudyKit kit)
        {
            _service = service;
            _projects = projects;
            _departments = departments;
            _offices = offices;
            _kit = kit;
        }

        public async Task EnsureResponsibleAsync(int? departmentId, int branchId)
        {
            if (departmentId is not int id) return;
            var department = await _departments.GetByIdAsync(id) ?? throw new KeyNotFoundException("القسم المسؤول غير موجود");
            if (department.BranchId != branchId) throw new InvalidOperationException("القسم المسؤول يجب أن يكون من أقسام فرع المشروع");
        }

        public static void EnsureStageOpen(ProjectStage s)
        {
            if (s.Status == StudyStageStatus.Closed) throw new InvalidOperationException("المرحلة مغلقة — أعد فتحها أولاً");
        }

        public async Task<StageTaskDto> TaskDetailAsync(int id, Viewer viewer) =>
            TaskDto(await _service.GetTaskAsync(id) ?? throw new KeyNotFoundException("المهمة غير موجودة"), viewer);

        /// <summary>الجهة من فريق المشروع: قسم مشارك، مكتب من قسم مشارك، أو عضو في الفريق</summary>
        public async Task ValidateTargetAsync(StudyTaskFormDto dto, int projectId)
        {
            switch ((StudyTaskTarget)dto.TargetType)
            {
                case StudyTaskTarget.Department:
                    if (!await _projects.DepartmentParticipatesAsync(projectId, dto.DepartmentId!.Value))
                        throw new InvalidOperationException("القسم غير مشارك في المشروع — أضفه أولاً");
                    break;
                case StudyTaskTarget.Office:
                    var office = await _offices.GetByIdAsync(dto.OfficeId!.Value) ?? throw new KeyNotFoundException("المكتب غير موجود");
                    if (!await _projects.DepartmentParticipatesAsync(projectId, office.DepartmentId))
                        throw new InvalidOperationException("قسم المكتب غير مشارك في المشروع — أضفه أولاً");
                    break;
                default:
                    if (!await _projects.IsMemberAsync(projectId, dto.AssigneeUserId!.Value))
                        throw new InvalidOperationException("الموظف ليس عضواً في فريق المشروع");
                    break;
            }
        }

        public static void ApplyTarget(StudyTaskFormDto dto, StageTask t)
        {
            t.TargetType = (StudyTaskTarget)dto.TargetType;
            t.DepartmentId = t.TargetType == StudyTaskTarget.Department ? dto.DepartmentId : null;
            t.OfficeId = t.TargetType == StudyTaskTarget.Office ? dto.OfficeId : null;
            t.AssigneeUserId = t.TargetType == StudyTaskTarget.User ? dto.AssigneeUserId : null;
        }

        public async Task<ProjectStageDto> StageDetailAsync(int id, Viewer viewer)
        {
            var stage = await _service.GetStageAsync(id) ?? throw new KeyNotFoundException("المرحلة غير موجودة");
            return StageDto(stage, stage.Project, viewer);
        }

        public ProjectStageDto StageDto(ProjectStage stage, StudyProject project, Viewer viewer)
        {
            var dto = _kit.Mapper.Map<ProjectStageDto>(stage);
            dto.CanAddTask = IsOpen(project) && stage.Status != StudyStageStatus.Closed
                && StudyRules.InScope(viewer, project.BranchId) && viewer.Has("CreateStudyTask")
                && StudyRules.CanManageStageTasks(viewer, stage.ResponsibleDepartmentId);
            return dto;
        }

        public StageTaskDto TaskDto(StageTask t, Viewer viewer)
        {
            var dto = _kit.Mapper.Map<StageTaskDto>(t);
            var editable = IsOpen(t.Stage.Project) && t.Stage.Status != StudyStageStatus.Closed;
            // التعديل والحذف لمدير النظام أو القسم المسؤول عن المرحلة فقط (تحديث الإنجاز بقاعدته الخاصة أدناه)
            dto.CanEdit = editable && viewer.Has("EditStudyTask") && StudyRules.InScope(viewer, t.Stage.Project.BranchId)
                && StudyRules.CanManageStageTasks(viewer, t.Stage.ResponsibleDepartmentId);
            dto.CanUpdateProgress = editable && viewer.Has(StudyPermissions.UpdateTaskProgress) && IsTarget(viewer, t);
            return dto;
        }

        public static bool IsOpen(StudyProject p) => p.Status is StudyProjectStatus.Running or StudyProjectStatus.Suspended;

        /// <summary>المهمة مسندة إلى المستخدم أو مكتبه أو قسمه (للقسم: موظفوه المباشرون بلا مكتب)</summary>
        public static bool IsTarget(Viewer viewer, StageTask t) =>
            viewer.IsSuperAdmin
            || (t.TargetType == StudyTaskTarget.User && t.AssigneeUserId == viewer.Id)
            || (t.TargetType == StudyTaskTarget.Office && t.OfficeId != null && t.OfficeId == viewer.User.OfficeId)
            || (t.TargetType == StudyTaskTarget.Department && t.DepartmentId != null && viewer.User.OfficeId == null
                && t.DepartmentId == viewer.User.DepartmentId);

        public static void EnsureOpen(StudyProject p)
        {
            if (!IsOpen(p)) throw new InvalidOperationException("المشروع مكتمل أو ملغى — لا تُعدَّل مراحله ومهامه");
        }

        public async Task<ProjectStage> LoadStageAsync(int id, Viewer viewer)
        {
            var stage = await _service.GetStageAsync(id) ?? throw new KeyNotFoundException("المرحلة غير موجودة");
            StudyRules.EnsureInScope(viewer, stage.Project.BranchId);
            return stage;
        }

        public async Task<StudyProject> LoadProjectAsync(int id, Viewer viewer)
        {
            var project = await _projects.GetByIdAsync(id) ?? throw new KeyNotFoundException("المشروع غير موجود");
            StudyRules.EnsureInScope(viewer, project.BranchId);
            return project;
        }

        public async Task LogAsync(int projectId, StudyEntityType type, int id, StudyLogAction action, string title, string? details = null) =>
            await _kit.LogAsync(await _projects.GetRequestIdAsync(projectId), type, id, action, title, details);
    }
}
