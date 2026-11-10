using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;

namespace Application.Features.CentralStudies.Responses
{
    /// <summary>الدوال المشتركة بين عمليات Responses (تحميل السجل، التحويل إلى DTO، الأعلام…) — منقولة من المعالج الواحد القديم</summary>
    public class StudyResponseSupport
    {
        private readonly IStudyResponseService _service;
        private readonly IUserService _users;
        private readonly IStudyApprovalService _approvals;
        private readonly StudyKit _kit;

        public StudyResponseSupport(IStudyResponseService service, IUserService users, IStudyApprovalService approvals, StudyKit kit)
        {
            _service = service;
            _users = users;
            _approvals = approvals;
            _kit = kit;
        }

        public StudyResponseDto ToDto(StudyAssignmentResponse e, Viewer viewer)
        {
            var dto = _kit.Mapper.Map<StudyResponseDto>(e);
            var inScope = StudyRules.InScope(viewer, e.Assignment.Request.BranchId);
            dto.CanEditFollowingPart = CanEditFollowing(viewer, e);
            dto.CanEditPlanningPart = false;   // يُحسب في التفاصيل (يحتاج سطور التواقيع)
            dto.CanEdit = dto.CanEditFollowingPart;
            dto.CanSubmit = dto.CanEditFollowingPart;
            dto.CanDelete = inScope && viewer.Has("DeleteStudyResponse") && e.Status == StudyDocumentStatus.Draft;
            dto.CanCancel = inScope && viewer.Has(StudyPermissions.CancelDocuments)
                            && e.Status is not (StudyDocumentStatus.Draft or StudyDocumentStatus.Cancelled)
                            && (e.StartOrder == null || e.StartOrder.Status == StudyDocumentStatus.Cancelled);
            dto.CanCreateStartOrder = inScope && viewer.Has("CreateStudyStartOrder") && e.Status == StudyDocumentStatus.Approved
                                      && e.Recommendation != StudyRecommendation.NotAvailable && e.StartOrder == null;
            return dto;
        }

        public async Task ValidateAsync(StudyResponseFormDto dto, int branchId)
        {
            if (dto.ProposedManagerUserId is int managerId)
                StudyRules.EnsureBranchEmployee(await _users.GetByIdAsync(managerId), branchId, "مدير المشروع المقترح");
        }

        public static void EnsureFollowingDepartment(Viewer viewer, StudyAssignmentResponse entity)
        {
            if (!StudyRules.InDepartment(viewer, entity.Assignment.FollowingDepartmentId))
                throw new UnauthorizedAccessException("يعدّل الرد موظفو القسم المتابع للتكليف");
        }

        /// <summary>جزء القسم المتابع: مسودة أو مرفوض، لموظفي قسم التكليف المتابع (مدير النظام بلا حدّ)</summary>
        public static bool CanEditFollowing(Viewer viewer, StudyAssignmentResponse e) =>
            StudyRules.InScope(viewer, e.Assignment.Request.BranchId) && viewer.Has("EditStudyResponse")
            && StudyRules.IsEditable(e.Status) && StudyRules.InDepartment(viewer, e.Assignment.FollowingDepartmentId);

        /// <summary>جزء التخطيط: الرد بانتظار الاعتماد، وقع القسم المتابع ولم يوقّع التخطيط بعد</summary>
        public static bool CanEditPlanning(Viewer viewer, StudyAssignmentResponse e, IReadOnlyCollection<StudyApproval> rows)
        {
            if (!StudyRules.InScope(viewer, e.Assignment.Request.BranchId) || !viewer.Has(StudyPermissions.EditResponsePlanning)
                || e.Status != StudyDocumentStatus.PendingApproval) return false;
            var planning = rows.FirstOrDefault(r => r.SignerRole == StudySignerRole.PlanningAndProjects);
            var following = rows.FirstOrDefault(r => r.SignerRole == StudySignerRole.FollowingDepartment);
            return planning is { Decision: StudyDecision.Pending } && following != null && StudyRules.IsPositive(following.Decision);
        }

        public async Task<StudyAssignmentResponse> LoadAsync(int id, Viewer viewer)
        {
            var entity = await _service.GetByIdAsync(id) ?? throw new KeyNotFoundException("إشعار الرد غير موجود");
            StudyRules.EnsureInScope(viewer, entity.Assignment.Request.BranchId);
            return entity;
        }

        public async Task<StudyResponseDto> DetailAsync(int id, Viewer viewer)
        {
            var entity = await LoadAsync(id, viewer);
            var dto = ToDto(entity, viewer);
            dto.Chain = await _kit.ChainAsync(entity.Assignment.RequestId);
            dto.Approvals = await _kit.ApprovalsAsync(ApprovalOwner.Of(entity));
            await _kit.AddWhoIsNextAsync(dto, StudyEntityType.Response, entity.Id, ApprovalOwner.Of(entity));
            var rows = await _approvals.GetForOwnerAsync(StudyEntityType.Response, entity.Id);
            dto.CanEditPlanningPart = CanEditPlanning(viewer, entity, rows);
            dto.CanEdit = dto.CanEditFollowingPart || dto.CanEditPlanningPart;
            return dto;
        }

        /// <summary>جزء القسم المتابع: الحقول البسيطة + المباني بنوعها (مفتاحها مركّب) وبنود التكلفة تُستبدل</summary>
        public static void Apply(StudyResponseFormDto dto, StudyAssignmentResponse e)
        {
            e.ResponseDate = dto.ResponseDate.Date;
            e.ProposedManagerUserId = dto.ProposedManagerUserId;
            e.Recommendation = (StudyRecommendation)dto.Recommendation;
            e.SiteArea = dto.SiteArea;
            e.InitialCost = dto.InitialCost;

            var buildingTypes = dto.Buildings.Select(b => (StudyBuildingType)b.BuildingType).ToHashSet();
            e.Buildings.RemoveAll(b => !buildingTypes.Contains(b.BuildingType));
            foreach (var b in dto.Buildings)
            {
                var type = (StudyBuildingType)b.BuildingType;
                var row = e.Buildings.FirstOrDefault(x => x.BuildingType == type);
                if (row == null) e.Buildings.Add(row = new ResponseBuilding { BuildingType = type });
                row.BuildingCount = b.BuildingCount;
                row.BuildingsArea = b.BuildingsArea;
            }

            e.CostItems.Clear();
            e.CostItems.AddRange(dto.CostItems.Select((c, i) => new ResponseCostItem
            {
                SortOrder = i + 1,
                Description = c.Description.Trim(),
                Unit = c.Unit.Trim(),
                Quantity = c.Quantity,
                UnitPrice = c.UnitPrice
            }));
        }
    }
}
