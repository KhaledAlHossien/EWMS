using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;
using System.Globalization;

namespace Application.Features.CentralStudies.Assignments
{
    /// <summary>الدوال المشتركة بين عمليات Assignments (تحميل السجل، التحويل إلى DTO، الأعلام…) — منقولة من المعالج الواحد القديم</summary>
    public class StudyAssignmentSupport
    {
        private readonly IStudyAssignmentService _service;
        private readonly IDepartmentService _departments;
        private readonly StudyKit _kit;

        public StudyAssignmentSupport(IStudyAssignmentService service, IDepartmentService departments, StudyKit kit)
        {
            _service = service;
            _departments = departments;
            _kit = kit;
        }

        public StudyAssignmentDto ToDto(StudyAssignment e, Viewer viewer)
        {
            var dto = _kit.Mapper.Map<StudyAssignmentDto>(e);
            var inScope = StudyRules.InScope(viewer, e.Request.BranchId);
            dto.CanEdit = inScope && viewer.Has("EditStudyAssignment") && StudyRules.IsEditable(e.Status);
            dto.CanSubmit = dto.CanEdit;
            dto.CanDelete = inScope && viewer.Has("DeleteStudyAssignment") && e.Status == StudyDocumentStatus.Draft;
            dto.CanCancel = inScope && viewer.Has(StudyPermissions.CancelDocuments)
                            && e.Status is not (StudyDocumentStatus.Draft or StudyDocumentStatus.Cancelled)
                            && (e.Response == null || e.Response.Status == StudyDocumentStatus.Cancelled);
            dto.CanCreateResponse = inScope && viewer.Has("CreateStudyResponse")
                                    && e.Status == StudyDocumentStatus.Approved && e.Response == null;
            return dto;
        }

        public async Task EnsureDepartmentAsync(int departmentId, int branchId)
        {
            var department = await _departments.GetByIdAsync(departmentId)
                ?? throw new KeyNotFoundException("القسم المتابع غير موجود");
            if (department.BranchId != branchId)
                throw new InvalidOperationException("القسم المتابع يجب أن يكون من أقسام فرع الطلب");
        }

        public async Task<string> NewProjectCodeAsync(StudyRequest request, DateTime assignmentDate, string? adminArchiveNumber)
        {
            var code = FormatProjectCode(request, assignmentDate, adminArchiveNumber);
            if (await _service.ProjectCodeExistsAsync(code))
                throw new InvalidOperationException("كود المشروع مستخدم في تكليف أو مشروع آخر");
            return code;
        }

        public static string FormatProjectCode(StudyRequest request, DateTime assignmentDate, string? adminArchiveNumber)
        {
            if (assignmentDate == default)
                throw new InvalidOperationException("تاريخ التكليف مطلوب");
            var requestNumber = StudyRules.Clean(request.RequestNumber)
                ?? throw new InvalidOperationException("رقم طلب الدراسة مطلوب لتوليد كود المشروع");
            var archiveNumber = StudyRules.Clean(request.AdminArchiveNumber) ?? StudyRules.Clean(adminArchiveNumber)
                ?? throw new InvalidOperationException("رقم أرشيف ديوان الإدارة مطلوب لتوليد كود المشروع");
            if (archiveNumber.Length > 50)
                throw new InvalidOperationException("رقم أرشيف ديوان الإدارة لا يتجاوز 50 حرفاً");
            var workCode = request.WorkType switch
            {
                StudyWorkType.Construction => "N",
                StudyWorkType.MaintenanceAndRestoration => "R",
                StudyWorkType.ConstructionAndMaintenanceAndRestoration => "RN",
                _ => throw new InvalidOperationException("نوع العمل في طلب الدراسة غير صالح لتوليد كود المشروع")
            };
            return string.Create(CultureInfo.InvariantCulture,
                $"{request.BranchId}-{assignmentDate:yy}-{workCode}-{requestNumber}-{archiveNumber}");
        }

        public async Task<StudyAssignment> LoadAsync(int id, Viewer viewer)
        {
            var entity = await _service.GetByIdAsync(id) ?? throw new KeyNotFoundException("التكليف غير موجود");
            StudyRules.EnsureInScope(viewer, entity.Request.BranchId);
            return entity;
        }

        public async Task<StudyAssignmentDto> DetailAsync(int id, Viewer viewer)
        {
            var entity = await LoadAsync(id, viewer);
            var dto = ToDto(entity, viewer);
            dto.Chain = await _kit.ChainAsync(entity.RequestId);
            dto.Approvals = await _kit.ApprovalsAsync(ApprovalOwner.Of(entity));
            await _kit.AddWhoIsNextAsync(dto, StudyEntityType.Assignment, entity.Id, ApprovalOwner.Of(entity));
            return dto;
        }

        public static void Apply(StudyAssignmentFormDto dto, StudyAssignment e)
        {
            e.FollowingDepartmentId = dto.FollowingDepartmentId;
            e.AssignmentDate = dto.AssignmentDate.Date;
            e.AllocatedBudget = dto.AllocatedBudget;
            e.ScopeSummary = dto.ScopeSummary.Trim();
            e.NeedsTechnicalEvaluation = dto.NeedsTechnicalEvaluation;
            e.NeedsResourceEstimate = dto.NeedsResourceEstimate;
            e.NeedsPreliminaryBudget = dto.NeedsPreliminaryBudget;
            e.NeedsFinalRecommendation = dto.NeedsFinalRecommendation;
            e.ReferralDate = dto.ReferralDate.Date;
            e.ResponseDeadline = dto.ResponseDeadline.Date;
            e.Priority = (StudyPriority)dto.Priority;
        }
    }
}
