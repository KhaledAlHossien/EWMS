using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;

namespace Application.Features.CentralStudies.StartOrders
{
    /// <summary>الدوال المشتركة بين عمليات StartOrders (تحميل السجل، التحويل إلى DTO، الأعلام…) — منقولة من المعالج الواحد القديم</summary>
    public class StudyStartOrderSupport
    {
        private readonly IStudyStartOrderService _service;
        private readonly IStudyLookupService _lookups;
        private readonly IDepartmentService _departments;
        private readonly IUserService _users;
        private readonly StudyKit _kit;

        public StudyStartOrderSupport(IStudyStartOrderService service, IStudyLookupService lookups, IDepartmentService departments, IUserService users, StudyKit kit)
        {
            _service = service;
            _lookups = lookups;
            _departments = departments;
            _users = users;
            _kit = kit;
        }

        public StudyStartOrderDto ToDto(StudyStartOrder e, Viewer viewer)
        {
            var dto = _kit.Mapper.Map<StudyStartOrderDto>(e);
            var inScope = StudyRules.InScope(viewer, e.Response.Assignment.Request.BranchId);
            dto.CanEdit = inScope && viewer.Has("EditStudyStartOrder") && StudyRules.IsEditable(e.Status);
            dto.CanSubmit = dto.CanEdit;
            dto.CanDelete = inScope && viewer.Has("DeleteStudyStartOrder") && e.Status == StudyDocumentStatus.Draft;
            dto.CanCancel = inScope && viewer.Has(StudyPermissions.CancelDocuments)
                            && e.Status is not (StudyDocumentStatus.Draft or StudyDocumentStatus.Cancelled)
                            && (e.Project == null || e.Project.Status == StudyProjectStatus.Cancelled);
            dto.CanCreateProject = inScope && viewer.Has("CreateStudyProject") && e.Status == StudyDocumentStatus.Approved && e.Project == null;
            return dto;
        }

        public async Task ValidateAsync(StudyStartOrderFormDto dto, int branchId)
        {
            var department = await _departments.GetByIdAsync(dto.CompetentDepartmentId) ?? throw new KeyNotFoundException("القسم المختص غير موجود");
            if (department.BranchId != branchId)
                throw new InvalidOperationException("القسم المختص يجب أن يكون من أقسام فرع الطلب");

            StudyRules.EnsureBranchEmployee(await _users.GetByIdAsync(dto.ProjectManagerUserId), branchId, "مدير المشروع");

            foreach (var engineerId in dto.Engineers.Select(e => e.EngineerUserId).Distinct())
                StudyRules.EnsureBranchEmployee(await _users.GetByIdAsync(engineerId), branchId, "أحد المهندسين");
            var disciplineIds = dto.Engineers.Select(e => e.DisciplineId).Distinct().ToList();
            if (disciplineIds.Count > 0 && await _lookups.CountActiveDisciplinesAsync(disciplineIds) != disciplineIds.Count)
                throw new KeyNotFoundException("أحد الاختصاصات غير موجود أو غير فعّال");
        }

        public async Task<StudyStartOrder> LoadAsync(int id, Viewer viewer)
        {
            var entity = await _service.GetByIdAsync(id) ?? throw new KeyNotFoundException("أمر البدء غير موجود");
            StudyRules.EnsureInScope(viewer, entity.Response.Assignment.Request.BranchId);
            return entity;
        }

        public async Task<StudyStartOrderDto> DetailAsync(int id, Viewer viewer)
        {
            var entity = await LoadAsync(id, viewer);
            var dto = ToDto(entity, viewer);
            dto.Chain = await _kit.ChainAsync(entity.Response.Assignment.RequestId);
            dto.Approvals = await _kit.ApprovalsAsync(ApprovalOwner.Of(entity));
            await _kit.AddWhoIsNextAsync(dto, StudyEntityType.StartOrder, entity.Id, ApprovalOwner.Of(entity));
            return dto;
        }

        /// <summary>الحقول + مزامنة المهندسين بمعرّفهم (مفتاح مركّب) واستبدال الموارد</summary>
        public static void Apply(StudyStartOrderFormDto dto, StudyStartOrder e)
        {
            e.CompetentDepartmentId = dto.CompetentDepartmentId;
            e.ProjectManagerUserId = dto.ProjectManagerUserId;
            e.OrderDate = dto.OrderDate.Date;
            e.StudyStartDate = dto.StudyStartDate.Date;
            e.StudyDeadline = dto.StudyDeadline.Date;

            var engineerIds = dto.Engineers.Select(x => x.EngineerUserId).ToHashSet();
            e.Engineers.RemoveAll(x => !engineerIds.Contains(x.EngineerUserId));
            foreach (var en in dto.Engineers)
            {
                var row = e.Engineers.FirstOrDefault(x => x.EngineerUserId == en.EngineerUserId);
                if (row == null) e.Engineers.Add(row = new StartOrderEngineer { EngineerUserId = en.EngineerUserId });
                row.DisciplineId = en.DisciplineId;
            }

            e.Resources.Clear();
            e.Resources.AddRange(dto.Resources.Select(x => new StartOrderResource
            {
                Item = x.Item.Trim(),
                Quantity = x.Quantity,
                Note = StudyRules.Clean(x.Note)
            }));
        }
    }
}
