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
        private readonly StudyKit _kit;

        public StudyProjectSupport(IStudyProjectService service, StudyKit kit)
        {
            _service = service;
            _kit = kit;
        }

        public StudyProjectDto ToDto(StudyProject e, Viewer viewer)
        {
            var dto = _kit.Mapper.Map<StudyProjectDto>(e);
            ApplyFlags(dto, viewer);
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

        public async Task<StudyProjectDto> DetailAsync(int id, Viewer viewer)
        {
            var entity = await LoadAsync(id, viewer);
            var dto = ToDto(entity, viewer);
            // الفريق بإسقاط مباشر (بلا كيانات User) ثم أعلام إدارة أعضاء كل قسم
            dto.Departments = await _service.GetTeamAsync(id);
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
            return dto;
        }

        public static void EnsureOpen(StudyProject p)
        {
            if (!IsOpen(p)) throw new InvalidOperationException("المشروع مكتمل أو ملغى — لا يُعدَّل");
        }
    }
}
