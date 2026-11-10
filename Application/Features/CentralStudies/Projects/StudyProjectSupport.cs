using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;
using FluentValidation;

namespace Application.Features.CentralStudies.Projects
{
    /// <summary>الدوال المشتركة بين عمليات Projects (تحميل السجل، التحويل إلى DTO، الأعلام…) — منقولة من المعالج الواحد القديم</summary>
    public class StudyProjectSupport
    {
        private readonly IStudyProjectService _service;
        private readonly IStudyStageService _stages;
        private readonly StudyKit _kit;

        public StudyProjectSupport(IStudyProjectService service, IStudyStageService stages, StudyKit kit)
        {
            _service = service;
            _stages = stages;
            _kit = kit;
        }

        public StudyProjectDto ToDto(StudyProject e, Viewer viewer)
        {
            var dto = _kit.Mapper.Map<StudyProjectDto>(e);
            ApplyFlags(dto, viewer);
            ProjectScope.For(viewer, e).Stamp(dto);
            return dto;
        }

        /// <summary>رئيس القسم المشارك يعيّن موظفي قسمه فقط (مدير النظام بلا حدّ)</summary>
        public async Task<(ProjectDepartment Participation, StudyProject Project)> LoadParticipationForMembersAsync(int projectDepartmentId, Viewer viewer)
        {
            var participation = await _service.GetProjectDepartmentAsync(projectDepartmentId)
                ?? throw new KeyNotFoundException("القسم غير مشارك في المشروع");
            var project = await LoadAsync(participation.ProjectId, viewer);
            EnsureOpen(project);
            if (!StudyRules.InDepartment(viewer, participation.DepartmentId))
                throw new UnauthorizedAccessException("يعيّن رئيس القسم المشارك موظفي قسمه فقط");
            return (participation, project);
        }

        /// <summary>أعلام الأزرار من الـ DTO نفسه (حالة المشروع ومراحله وعدد مهامها) — قاعدة واحدة للتفاصيل والقائمة</summary>
        public static void ApplyFlags(StudyProjectDto dto, Viewer viewer)
        {
            var status = (StudyProjectStatus)dto.Status;
            var open = status is StudyProjectStatus.Running or StudyProjectStatus.Suspended;
            var inScope = StudyRules.InScope(viewer, dto.BranchId);
            dto.CanEdit = inScope && viewer.Has(StudyPermissions.EditProject) && open;
            dto.CanDelete = inScope && viewer.Has("DeleteStudyProject") && status == StudyProjectStatus.Running
                            && dto.Stages.All(s => s.TasksTotal == 0);
            dto.CanCancel = inScope && viewer.Has(StudyPermissions.CancelDocuments) && open;
            dto.CanRequestCompletion = dto.CanEdit && status == StudyProjectStatus.Running && dto.Stages.Count > 0
                                       && dto.Stages.All(s => s.Status == (int)StudyStageStatus.Closed);
            // إضافة مهمة: القسم المسؤول عن المرحلة فقط — القاعدة نفسها في StudyStageSupport.StageDto
            foreach (var s in dto.Stages)
                s.CanAddTask = open && inScope && s.Status != (int)StudyStageStatus.Closed && viewer.Has("CreateStudyTask")
                               && StudyRules.CanManageStageTasks(viewer, s.ResponsibleDepartmentId);
        }

        public async Task<int> RequestIdOf(StudyProject p) =>
            p.StartOrder?.Response?.Assignment?.RequestId ?? await _service.GetRequestIdAsync(p.Id);

        public static bool IsOpen(StudyProject p) => p.Status is StudyProjectStatus.Running or StudyProjectStatus.Suspended;

        public async Task<StudyProject> LoadAsync(int id, Viewer viewer)
        {
            var entity = await _service.GetByIdAsync(id) ?? throw new KeyNotFoundException("المشروع غير موجود");
            StudyRules.EnsureInScope(viewer, entity.BranchId);
            return entity;
        }

        /// <summary>
        /// حدّ الرؤية على قائمة مشاريع (2026-11-10): العلمان CanViewFull/ViewLevel، ولغير الكامل تُخفى المراحل خارج حدّه
        /// (مراحل قسمه، وكل مرحلة فيها مهمة يراها). الأعلام تُحسب قبلاً من كل المراحل، والنسبة الكلية من كل المراحل كما هي.
        /// </summary>
        public async Task ApplyScopeAsync(IReadOnlyList<StudyProjectDto> dtos, Viewer viewer)
        {
            var restricted = new List<(StudyProjectDto Dto, ProjectScope Scope)>();
            foreach (var dto in dtos)
            {
                var scope = ProjectScope.For(viewer, dto.ManagerUserId);
                scope.Stamp(dto);
                if (!scope.IsFull) restricted.Add((dto, scope));
            }
            if (restricted.Count == 0) return;
            var withTasks = await _stages.GetStageIdsWithTasksAsync(restricted.Select(r => r.Dto.Id), restricted[0].Scope.TaskFilter()!);
            foreach (var (dto, scope) in restricted)
                dto.Stages = dto.Stages.Where(s => scope.CanSeeStage(s.ResponsibleDepartmentId, s.Id, withTasks)).ToList();
        }

        public async Task<StudyProjectDto> DetailAsync(int id, Viewer viewer)
        {
            var entity = await LoadAsync(id, viewer);
            var dto = ToDto(entity, viewer);
            var scope = ProjectScope.For(viewer, entity);
            await ApplyScopeAsync([dto], viewer);
            // الفريق بإسقاط مباشر (بلا كيانات User) ثم أعلام إدارة أعضاء كل قسم؛ ولغير الكامل صف قسمه بأعضاء حدّه فقط
            var teamLimit = scope.TeamLimit();
            dto.Departments = scope.IsFull ? await _service.GetTeamAsync(id) : teamLimit == null ? [] : await _service.GetTeamAsync(id, teamLimit);
            foreach (var d in dto.Departments)
                d.CanManageMembers = viewer.Has("AddStudyProjectMember") && StudyRules.InDepartment(viewer, d.DepartmentId) && IsOpen(entity);
            var requestId = entity.StartOrder.Response.Assignment.RequestId;
            // مكاتب كل قسم مشارك (هدف «مكتب» في المهمة) — بلا ViewOffices
            var offices = await _service.GetOfficesOfDepartmentsAsync(dto.Departments.Select(d => d.DepartmentId));
            foreach (var d in dto.Departments)
                d.Offices = offices.Where(o => o.DepartmentId == d.DepartmentId).Select(o => new StudyOfficeOptionDto(o.Id, o.Name)).ToList();
            dto.Chain = await _kit.ChainAsync(requestId);
            dto.Approvals = await _kit.ApprovalsAsync(ApprovalOwner.Of(entity, requestId));
            // طلب إنجاز معلّق (سطر لم يُقرَّر بعد ولم يُرفض الطلب)
            if (dto.Approvals.Any(a => a.Decision == (int)StudyDecision.Pending) && dto.Approvals.All(a => a.Decision != (int)StudyDecision.Rejected))
                dto.CanRequestCompletion = false;
            // تواقيع الإنجاز لمن وقّع سطراً منها أو يملك صلاحية توقيع سطر منها (أو الرؤية الكاملة)
            if (!scope.CanSeeApprovals(viewer, dto.Approvals)) dto.Approvals = [];
            return dto;
        }

        public static void EnsureOpen(StudyProject p)
        {
            if (!IsOpen(p)) throw new InvalidOperationException("المشروع مكتمل أو ملغى — لا يُعدَّل");
        }
    }
}
