using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.CentralStudies;
using AutoMapper;
using Domain.Entities.CentralStudiesBranch;

namespace Application.Helper.Profiles
{
    /// <summary>
    /// فرع الدراسات المركزية: الكيانات ← المخرجات (مع الأسماء العربية من StudyRules)، والنماذج ← الكيانات للحقول البسيطة.
    /// الأعلام (Can…) والسلسلة والتواقيع تُملأ في الـ handlers. الأجزاء (المباني، البنود…) تُزامن يدوياً في الـ handler.
    /// </summary>
    public class CentralStudiesProfile : Profile
    {
        public CentralStudiesProfile()
        {
            // ----- القوائم -----
            CreateMap<StudyStageType, StudyStageTypeDto>()
                .ForMember(d => d.LinkedDocument, o => o.MapFrom(s => (int)s.LinkedDocument))
                .ForMember(d => d.LinkedDocumentName, o => o.MapFrom(s => StudyRules.Name(s.LinkedDocument)));
            CreateMap<StudyStageTypeFormDto, StudyStageType>()
                .ForMember(d => d.Id, o => o.Ignore())
                .ForMember(d => d.RowVersion, o => o.Ignore())
                .ForMember(d => d.LinkedDocument, o => o.MapFrom(s => (StudyLinkedDocument)s.LinkedDocument));

            CreateMap<StudyAttachmentType, StudyLookupDto>();
            CreateMap<EngineeringDiscipline, StudyLookupDto>();
            CreateMap<StudyLookupFormDto, StudyAttachmentType>().ForMember(d => d.Id, o => o.Ignore());
            CreateMap<StudyLookupFormDto, EngineeringDiscipline>().ForMember(d => d.Id, o => o.Ignore());

            // ----- التواقيع -----
            CreateMap<StudyApproval, StudyApprovalDto>()
                .ForMember(d => d.SignerRole, o => o.MapFrom(s => (int)s.SignerRole))
                .ForMember(d => d.SignerRoleName, o => o.MapFrom(s => StudyRules.Name(s.SignerRole)))
                .ForMember(d => d.Decision, o => o.MapFrom(s => (int)s.Decision))
                .ForMember(d => d.DecisionName, o => o.MapFrom(s => StudyRules.Name(s.Decision)))
                .ForMember(d => d.SignerName, o => o.MapFrom(s => s.Signer != null ? s.Signer.FullName : null))
                .ForMember(d => d.HasSignature, o => o.MapFrom(s => s.SignatureId != null))
                .ForMember(d => d.CanDecide, o => o.Ignore());

            // ----- الطلب -----
            CreateMap<StudyRequest, StudyRequestDto>()
                .ForMember(d => d.Status, o => o.MapFrom(s => (int)s.Status))
                .ForMember(d => d.StatusName, o => o.MapFrom(s => StudyRules.Name(s.Status)))
                .ForMember(d => d.CreatedByName, o => o.MapFrom(s => s.CreatedBy != null ? s.CreatedBy.FullName : string.Empty))
                .ForMember(d => d.Urgency, o => o.MapFrom(s => (int)s.Urgency))
                .ForMember(d => d.UrgencyName, o => o.MapFrom(s => StudyRules.Name(s.Urgency)))
                .ForMember(d => d.WorkType, o => o.MapFrom(s => (int)s.WorkType))
                .ForMember(d => d.WorkTypeName, o => o.MapFrom(s => StudyRules.Name(s.WorkType)))
                .ForMember(d => d.GovernorateName, o => o.MapFrom(s => Governorates.NameOf(s.GovernorateCode)))
                .ForMember(d => d.SiteName, o => o.MapFrom(s => s.Site != null ? s.Site.Name : null))
                .ForMember(d => d.BranchName, o => o.MapFrom(s => s.Branch != null ? s.Branch.Name : string.Empty))
                .ForMember(d => d.Chain, o => o.Ignore())
                .ForMember(d => d.Approvals, o => o.Ignore());
            CreateMap<StudyRequestFormDto, StudyRequest>()
                .ForMember(d => d.Urgency, o => o.MapFrom(s => (StudyUrgency)s.Urgency))
                .ForMember(d => d.WorkType, o => o.MapFrom(s => (StudyWorkType)s.WorkType))
                .ForMember(d => d.RequestDate, o => o.MapFrom(s => s.RequestDate.Date))
                .ForMember(d => d.BranchId, o => o.Ignore())
                .ForMember(d => d.RowVersion, o => o.Ignore());

            // ----- التكليف -----
            CreateMap<StudyAssignment, StudyAssignmentDto>()
                .ForMember(d => d.Status, o => o.MapFrom(s => (int)s.Status))
                .ForMember(d => d.StatusName, o => o.MapFrom(s => StudyRules.Name(s.Status)))
                .ForMember(d => d.CreatedByName, o => o.MapFrom(s => s.CreatedBy != null ? s.CreatedBy.FullName : string.Empty))
                .ForMember(d => d.RequestNumber, o => o.MapFrom(s => s.Request.RequestNumber))
                .ForMember(d => d.MainBeneficiary, o => o.MapFrom(s => s.Request.MainBeneficiary))
                .ForMember(d => d.BranchId, o => o.MapFrom(s => s.Request.BranchId))
                .ForMember(d => d.BranchName, o => o.MapFrom(s => s.Request.Branch != null ? s.Request.Branch.Name : string.Empty))
                .ForMember(d => d.FollowingDepartmentName, o => o.MapFrom(s => s.FollowingDepartment != null ? s.FollowingDepartment.Name : string.Empty))
                .ForMember(d => d.Priority, o => o.MapFrom(s => (int)s.Priority))
                .ForMember(d => d.PriorityName, o => o.MapFrom(s => StudyRules.Name(s.Priority)))
                .ForMember(d => d.Chain, o => o.Ignore())
                .ForMember(d => d.Approvals, o => o.Ignore());

            // ----- الرد -----
            CreateMap<ResponseBuilding, ResponseBuildingDto>()
                .ForMember(d => d.BuildingType, o => o.MapFrom(s => (int)s.BuildingType))
                .ForMember(d => d.BuildingTypeName, o => o.MapFrom(s => StudyRules.Name(s.BuildingType)));
            CreateMap<ResponseCostItem, ResponseCostItemDto>()
                .ForMember(d => d.Total, o => o.MapFrom(s => Math.Round(s.Quantity * s.UnitPrice, 2)));
            CreateMap<ResponseStaffEstimate, ResponseStaffDto>()
                .ForMember(d => d.DisciplineName, o => o.MapFrom(s => s.Discipline != null ? s.Discipline.Name : string.Empty));
            CreateMap<ResponseLogisticItem, StudyItemDto>();
            CreateMap<StudyAssignmentResponse, StudyResponseDto>()
                .ForMember(d => d.Status, o => o.MapFrom(s => (int)s.Status))
                .ForMember(d => d.StatusName, o => o.MapFrom(s => StudyRules.Name(s.Status)))
                .ForMember(d => d.CreatedByName, o => o.MapFrom(s => s.CreatedBy != null ? s.CreatedBy.FullName : string.Empty))
                .ForMember(d => d.AssignmentNumber, o => o.MapFrom(s => s.Assignment.AssignmentNumber))
                .ForMember(d => d.ProjectCode, o => o.MapFrom(s => s.Assignment.ProjectCode))
                .ForMember(d => d.RequestId, o => o.MapFrom(s => s.Assignment.RequestId))
                .ForMember(d => d.BranchId, o => o.MapFrom(s => s.Assignment.Request.BranchId))
                .ForMember(d => d.BranchName, o => o.MapFrom(s => s.Assignment.Request.Branch != null ? s.Assignment.Request.Branch.Name : string.Empty))
                .ForMember(d => d.FollowingDepartmentId, o => o.MapFrom(s => s.Assignment.FollowingDepartmentId))
                .ForMember(d => d.FollowingDepartmentName, o => o.MapFrom(s => s.Assignment.FollowingDepartment != null ? s.Assignment.FollowingDepartment.Name : string.Empty))
                .ForMember(d => d.ProposedManagerName, o => o.MapFrom(s => s.ProposedManager != null ? s.ProposedManager.FullName : null))
                .ForMember(d => d.Recommendation, o => o.MapFrom(s => (int)s.Recommendation))
                .ForMember(d => d.RecommendationName, o => o.MapFrom(s => StudyRules.Name(s.Recommendation)))
                .ForMember(d => d.TotalCost, o => o.MapFrom(s => Math.Round(s.CostItems.Sum(c => c.Quantity * c.UnitPrice), 2)))
                .ForMember(d => d.Buildings, o => o.MapFrom(s => s.Buildings.OrderBy(b => b.BuildingType)))
                .ForMember(d => d.CostItems, o => o.MapFrom(s => s.CostItems.OrderBy(c => c.SortOrder).ThenBy(c => c.Id)))
                .ForMember(d => d.StaffEstimates, o => o.MapFrom(s => s.StaffEstimates.OrderBy(x => x.DisciplineId)))
                .ForMember(d => d.LogisticItems, o => o.MapFrom(s => s.LogisticItems.OrderBy(x => x.Id)))
                .ForMember(d => d.Chain, o => o.Ignore())
                .ForMember(d => d.Approvals, o => o.Ignore());

            // ----- أمر البدء -----
            CreateMap<StartOrderEngineer, StartOrderEngineerDto>()
                .ForMember(d => d.EngineerName, o => o.MapFrom(s => s.Engineer != null ? s.Engineer.FullName : string.Empty))
                .ForMember(d => d.DisciplineName, o => o.MapFrom(s => s.Discipline != null ? s.Discipline.Name : string.Empty));
            CreateMap<StartOrderResource, StudyItemDto>();
            CreateMap<StudyStartOrder, StudyStartOrderDto>()
                .ForMember(d => d.Status, o => o.MapFrom(s => (int)s.Status))
                .ForMember(d => d.StatusName, o => o.MapFrom(s => StudyRules.Name(s.Status)))
                .ForMember(d => d.CreatedByName, o => o.MapFrom(s => s.CreatedBy != null ? s.CreatedBy.FullName : string.Empty))
                .ForMember(d => d.AssignmentId, o => o.MapFrom(s => s.Response.AssignmentId))
                .ForMember(d => d.AssignmentNumber, o => o.MapFrom(s => s.Response.Assignment.AssignmentNumber))
                .ForMember(d => d.ProjectCode, o => o.MapFrom(s => s.Response.Assignment.ProjectCode))
                .ForMember(d => d.RequestId, o => o.MapFrom(s => s.Response.Assignment.RequestId))
                .ForMember(d => d.BranchId, o => o.MapFrom(s => s.Response.Assignment.Request.BranchId))
                .ForMember(d => d.BranchName, o => o.MapFrom(s => s.Response.Assignment.Request.Branch != null ? s.Response.Assignment.Request.Branch.Name : string.Empty))
                .ForMember(d => d.CompetentDepartmentName, o => o.MapFrom(s => s.CompetentDepartment != null ? s.CompetentDepartment.Name : string.Empty))
                .ForMember(d => d.ProjectManagerName, o => o.MapFrom(s => s.ProjectManager != null ? s.ProjectManager.FullName : string.Empty))
                .ForMember(d => d.Resources, o => o.MapFrom(s => s.Resources.OrderBy(x => x.Id)))
                .ForMember(d => d.Chain, o => o.Ignore())
                .ForMember(d => d.Approvals, o => o.Ignore());

            // ----- المشروع -----
            CreateMap<ProjectMember, ProjectMemberDto>()
                .ForMember(d => d.FullName, o => o.MapFrom(s => s.User != null ? s.User.FullName : string.Empty))
                .ForMember(d => d.AssignedByName, o => o.MapFrom(s => s.AssignedBy != null ? s.AssignedBy.FullName : string.Empty));
            CreateMap<ProjectDepartment, ProjectDepartmentDto>()
                .ForMember(d => d.DepartmentName, o => o.MapFrom(s => s.Department != null ? s.Department.Name : string.Empty))
                .ForMember(d => d.AddedByName, o => o.MapFrom(s => s.AddedBy != null ? s.AddedBy.FullName : string.Empty))
                .ForMember(d => d.Members, o => o.MapFrom(s => s.Members.OrderBy(m => m.AssignedAt)))
                .ForMember(d => d.Offices, o => o.Ignore())
                .ForMember(d => d.CanManageMembers, o => o.Ignore());
            CreateMap<ProjectStage, ProjectStageDto>()
                .ForMember(d => d.StageTypeName, o => o.MapFrom(s => s.StageType != null ? s.StageType.Name : string.Empty))
                .ForMember(d => d.LinkedDocument, o => o.MapFrom(s => s.StageType != null ? (int)s.StageType.LinkedDocument : 0))
                .ForMember(d => d.Status, o => o.MapFrom(s => (int)s.Status))
                .ForMember(d => d.StatusName, o => o.MapFrom(s => StudyRules.Name(s.Status)))
                .ForMember(d => d.Progress, o => o.MapFrom(s => StudyRules.StageProgress(s)))
                .ForMember(d => d.ResponsibleDepartmentName, o => o.MapFrom(s => s.ResponsibleDepartment != null ? s.ResponsibleDepartment.Name : null))
                .ForMember(d => d.ClosedByName, o => o.MapFrom(s => s.ClosedBy != null ? s.ClosedBy.FullName : null))
                .ForMember(d => d.TasksTotal, o => o.MapFrom(s => s.Tasks.Count))
                .ForMember(d => d.TasksDone, o => o.MapFrom(s => s.Tasks.Count(t => t.Status == StudyTaskStatus.Done)));
            CreateMap<StudyProject, StudyProjectDto>()
                .ForMember(d => d.StartOrderNumber, o => o.MapFrom(s => s.StartOrder != null ? s.StartOrder.OrderNumber : string.Empty))
                .ForMember(d => d.BranchName, o => o.MapFrom(s => s.Branch != null ? s.Branch.Name : string.Empty))
                .ForMember(d => d.ManagerName, o => o.MapFrom(s => s.Manager != null ? s.Manager.FullName : string.Empty))
                .ForMember(d => d.Status, o => o.MapFrom(s => (int)s.Status))
                .ForMember(d => d.StatusName, o => o.MapFrom(s => StudyRules.Name(s.Status)))
                .ForMember(d => d.Progress, o => o.MapFrom(s => StudyRules.ProjectProgress(s.Stages)))
                .ForMember(d => d.CreatedByName, o => o.MapFrom(s => s.CreatedBy != null ? s.CreatedBy.FullName : string.Empty))
                .ForMember(d => d.Departments, o => o.MapFrom(s => s.Departments.OrderBy(x => x.AddedAt)))
                .ForMember(d => d.Stages, o => o.MapFrom(s => s.Stages.OrderBy(x => x.SortOrder).ThenBy(x => x.Id)))
                .ForMember(d => d.Chain, o => o.Ignore())
                .ForMember(d => d.Approvals, o => o.Ignore());

            CreateMap<StageTask, StageTaskDto>()
                .ForMember(d => d.StageName, o => o.MapFrom(s => s.Stage != null && s.Stage.StageType != null ? s.Stage.StageType.Name : string.Empty))
                .ForMember(d => d.ProjectId, o => o.MapFrom(s => s.Stage != null ? s.Stage.ProjectId : 0))
                .ForMember(d => d.ProjectCode, o => o.MapFrom(s => s.Stage != null && s.Stage.Project != null ? s.Stage.Project.Code : string.Empty))
                .ForMember(d => d.ProjectName, o => o.MapFrom(s => s.Stage != null && s.Stage.Project != null ? s.Stage.Project.Name : string.Empty))
                .ForMember(d => d.TargetType, o => o.MapFrom(s => (int)s.TargetType))
                .ForMember(d => d.TargetTypeName, o => o.MapFrom(s => StudyRules.Name(s.TargetType)))
                .ForMember(d => d.TargetName, o => o.MapFrom(s =>
                    s.TargetType == StudyTaskTarget.Department ? (s.Department != null ? s.Department.Name : string.Empty)
                    : s.TargetType == StudyTaskTarget.Office ? (s.Office != null ? s.Office.Name : string.Empty)
                    : (s.Assignee != null ? s.Assignee.FullName : string.Empty)))
                .ForMember(d => d.AssignedByName, o => o.MapFrom(s => s.AssignedBy != null ? s.AssignedBy.FullName : string.Empty))
                .ForMember(d => d.Status, o => o.MapFrom(s => (int)s.Status))
                .ForMember(d => d.StatusName, o => o.MapFrom(s => StudyRules.Name(s.Status)))
                .ForMember(d => d.CanEdit, o => o.Ignore())
                .ForMember(d => d.CanUpdateProgress, o => o.Ignore());

            // ----- المخططات -----
            CreateMap<ArchitecturalVersion, ArchitecturalVersionDto>()
                .ForMember(d => d.Label, o => o.MapFrom(s => StudyRules.VersionLabel(s.VersionNumber)))
                .ForMember(d => d.Status, o => o.MapFrom(s => (int)s.Status))
                .ForMember(d => d.StatusName, o => o.MapFrom(s => StudyRules.Name(s.Status)))
                .ForMember(d => d.CreatedByName, o => o.MapFrom(s => s.CreatedBy != null ? s.CreatedBy.FullName : string.Empty))
                .ForMember(d => d.DesignApprovalId, o => o.MapFrom(s => s.DesignApproval != null ? s.DesignApproval.Id : (int?)null))
                .ForMember(d => d.DesignApprovalNumber, o => o.MapFrom(s => s.DesignApproval != null ? s.DesignApproval.CdaNumber : null))
                .ForMember(d => d.DesignApprovalDecision, o => o.MapFrom(s => s.DesignApproval != null ? (int)s.DesignApproval.Decision : (int?)null));
            CreateMap<ProjectDrawing, StudyDrawingDto>()
                .ForMember(d => d.ProjectCode, o => o.MapFrom(s => s.Project != null ? s.Project.Code : string.Empty))
                .ForMember(d => d.Kind, o => o.MapFrom(s => (int)s.Kind))
                .ForMember(d => d.KindName, o => o.MapFrom(s => StudyRules.Name(s.Kind)))
                .ForMember(d => d.DepartmentName, o => o.MapFrom(s => s.Department != null ? s.Department.Name : string.Empty))
                .ForMember(d => d.OfficeName, o => o.MapFrom(s => s.Office != null ? s.Office.Name : null))
                .ForMember(d => d.PreparedByName, o => o.MapFrom(s => s.PreparedBy != null ? s.PreparedBy.FullName : string.Empty))
                .ForMember(d => d.BasedOnVersionLabel, o => o.MapFrom(s => s.BasedOnVersion != null ? StudyRules.VersionLabel(s.BasedOnVersion.VersionNumber) : null))
                .ForMember(d => d.Status, o => o.MapFrom(s => (int)s.Status))
                .ForMember(d => d.StatusName, o => o.MapFrom(s => StudyRules.Name(s.Status)))
                .ForMember(d => d.Versions, o => o.MapFrom(s => s is ArchitecturalDrawing
                    ? ((ArchitecturalDrawing)s).Versions.OrderBy(v => v.VersionNumber).ToList()
                    : new List<ArchitecturalVersion>()))
                .ForMember(d => d.CanEdit, o => o.Ignore())
                .ForMember(d => d.CanReview, o => o.Ignore());
            CreateMap<DesignApproval, DesignApprovalDto>()
                .ForMember(d => d.VersionLabel, o => o.MapFrom(s => s.Version != null ? StudyRules.VersionLabel(s.Version.VersionNumber) : string.Empty))
                .ForMember(d => d.DrawingId, o => o.MapFrom(s => s.Version != null ? s.Version.DrawingId : 0))
                .ForMember(d => d.DrawingName, o => o.MapFrom(s => s.Version != null && s.Version.Drawing != null ? s.Version.Drawing.Name : string.Empty))
                .ForMember(d => d.ProjectId, o => o.MapFrom(s => s.Version != null && s.Version.Drawing != null ? s.Version.Drawing.ProjectId : 0))
                .ForMember(d => d.ProjectCode, o => o.MapFrom(s => s.Version != null && s.Version.Drawing != null && s.Version.Drawing.Project != null ? s.Version.Drawing.Project.Code : string.Empty))
                .ForMember(d => d.Decision, o => o.MapFrom(s => (int)s.Decision))
                .ForMember(d => d.DecisionName, o => o.MapFrom(s => StudyRules.Name(s.Decision)))
                .ForMember(d => d.CreatedByName, o => o.MapFrom(s => s.CreatedBy != null ? s.CreatedBy.FullName : string.Empty))
                .ForMember(d => d.CanEdit, o => o.Ignore())
                .ForMember(d => d.Approvals, o => o.Ignore());

            // ----- المرفقات والسجل -----
            CreateMap<StudyAttachment, StudyAttachmentDto>()
                .ForMember(d => d.OwnerType, o => o.MapFrom(s =>
                    s.RequestId != null ? (int)StudyEntityType.Request
                    : s.AssignmentId != null ? (int)StudyEntityType.Assignment
                    : s.ResponseId != null ? (int)StudyEntityType.Response
                    : s.StartOrderId != null ? (int)StudyEntityType.StartOrder
                    : s.ProjectId != null ? (int)StudyEntityType.Project
                    : s.DrawingId != null ? (int)StudyEntityType.Drawing
                    : (int)StudyEntityType.Version))
                .ForMember(d => d.OwnerId, o => o.MapFrom(s =>
                    s.RequestId ?? s.AssignmentId ?? s.ResponseId ?? s.StartOrderId ?? s.ProjectId ?? s.DrawingId ?? s.VersionId ?? 0))
                .ForMember(d => d.AttachmentTypeName, o => o.MapFrom(s => s.AttachmentType != null ? s.AttachmentType.Name : string.Empty))
                .ForMember(d => d.Previewable, o => o.MapFrom(s => Features.MapLayers.MapFileRules.IsPreviewable(s.ContentType)))
                .ForMember(d => d.UploadedByName, o => o.MapFrom(s => s.UploadedBy != null ? s.UploadedBy.FullName : string.Empty))
                .ForMember(d => d.CanDelete, o => o.Ignore());

            CreateMap<StudyActivityLog, StudyActivityLogDto>()
                .ForMember(d => d.EntityType, o => o.MapFrom(s => (int)s.EntityType))
                .ForMember(d => d.EntityTypeName, o => o.MapFrom(s => StudyRules.Name(s.EntityType)))
                .ForMember(d => d.Action, o => o.MapFrom(s => (int)s.Action))
                .ForMember(d => d.ActionName, o => o.MapFrom(s => StudyRules.Name(s.Action)))
                .ForMember(d => d.UserName, o => o.MapFrom(s => s.User != null ? s.User.FullName : string.Empty));
        }
    }
}
