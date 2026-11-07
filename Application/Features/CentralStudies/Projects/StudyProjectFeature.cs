using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.CentralStudies.Requests;
using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;
using FluentValidation;
using MediatR;

namespace Application.Features.CentralStudies.Projects
{
    // المشروع — واحد لكل أمر بدء معتمد. يُنشأ بمراحله من أنواع المراحل الفعّالة (المرتبطة بمستند تُنشأ مغلقة من تواريخه)،
    // ويُضاف القسم المختص قسماً مشاركاً. الإنجاز يعتمده إدارة المشاريع ثم رئيس الفرع. حدّ الصلاحيات: مشاريع فرعه.
    public record SearchStudyProjectsQuery(StudyDocumentFilterDto Filter) : IRequest<PagedResultDto<StudyProjectDto>>;
    public record GetStudyProjectQuery(int Id) : IRequest<StudyProjectDto>;
    public record CreateStudyProjectCommand(StudyProjectFormDto Dto) : IRequest<StudyProjectDto>;
    public record UpdateStudyProjectCommand(int Id, StudyProjectFormDto Dto) : IRequest<StudyProjectDto>;
    public record DeleteStudyProjectCommand(int Id) : IRequest<Unit>;
    public record CancelStudyProjectCommand(int Id, string Reason) : IRequest<StudyProjectDto>;
    public record RequestStudyProjectCompletionCommand(int Id) : IRequest<StudyProjectDto>;

    public record AddStudyProjectDepartmentCommand(int ProjectId, int DepartmentId) : IRequest<StudyProjectDto>;
    public record RemoveStudyProjectDepartmentCommand(int ProjectDepartmentId) : IRequest<StudyProjectDto>;
    public record AddStudyProjectMemberCommand(int ProjectDepartmentId, ProjectMemberFormDto Dto) : IRequest<StudyProjectDto>;
    public record RemoveStudyProjectMemberCommand(int ProjectDepartmentId, int UserId) : IRequest<StudyProjectDto>;

    // ════════════════════ التحقق ════════════════════

    public class StudyProjectFormValidator : AbstractValidator<StudyProjectFormDto>
    {
        public StudyProjectFormValidator()
        {
            RuleFor(x => x.Name).NotEmpty().WithMessage("اسم المشروع مطلوب").MaximumLength(200).WithMessage("اسم المشروع لا يتجاوز 200 حرف");
            RuleFor(x => x.Code).MaximumLength(50).WithMessage("كود المشروع لا يتجاوز 50 حرفاً");
            RuleFor(x => x.PlannedStartDate).NotEmpty().WithMessage("تاريخ البدء المخطط مطلوب");
            RuleFor(x => x.PlannedEndDate).NotEmpty().WithMessage("تاريخ الانتهاء المخطط مطلوب")
                .GreaterThanOrEqualTo(x => x.PlannedStartDate).WithMessage("الانتهاء المخطط لا يسبق البدء");
            RuleFor(x => x.Status).Must(s => s is null or (int)StudyProjectStatus.Running or (int)StudyProjectStatus.Suspended)
                .WithMessage("الحالة جارٍ أو معلّق فقط — الإكمال بالاعتماد والإلغاء بأمر الإلغاء");
        }
    }

    public class CreateStudyProjectValidator : AbstractValidator<CreateStudyProjectCommand>
    {
        public CreateStudyProjectValidator()
        {
            RuleFor(x => x.Dto).SetValidator(new StudyProjectFormValidator());
            RuleFor(x => x.Dto.StartOrderId).GreaterThan(0).WithMessage("حدد أمر البدء");
        }
    }

    public class UpdateStudyProjectValidator : AbstractValidator<UpdateStudyProjectCommand>
    {
        public UpdateStudyProjectValidator() => RuleFor(x => x.Dto).SetValidator(new StudyProjectFormValidator());
    }

    public class CancelStudyProjectValidator : AbstractValidator<CancelStudyProjectCommand>
    {
        public CancelStudyProjectValidator() =>
            RuleFor(x => x.Reason).NotEmpty().WithMessage("اذكر سبب الإلغاء").MaximumLength(1000).WithMessage("السبب لا يتجاوز 1000 حرف");
    }

    public class AddStudyProjectMemberValidator : AbstractValidator<AddStudyProjectMemberCommand>
    {
        public AddStudyProjectMemberValidator()
        {
            RuleFor(x => x.Dto.UserId).GreaterThan(0).WithMessage("اختر الموظف");
            RuleFor(x => x.Dto.RoleTitle).MaximumLength(100).WithMessage("الدور لا يتجاوز 100 حرف");
        }
    }

    // ════════════════════ المعالج ════════════════════

    public class StudyProjectHandler :
        IRequestHandler<SearchStudyProjectsQuery, PagedResultDto<StudyProjectDto>>,
        IRequestHandler<GetStudyProjectQuery, StudyProjectDto>,
        IRequestHandler<CreateStudyProjectCommand, StudyProjectDto>,
        IRequestHandler<UpdateStudyProjectCommand, StudyProjectDto>,
        IRequestHandler<DeleteStudyProjectCommand, Unit>,
        IRequestHandler<CancelStudyProjectCommand, StudyProjectDto>,
        IRequestHandler<RequestStudyProjectCompletionCommand, StudyProjectDto>,
        IRequestHandler<AddStudyProjectDepartmentCommand, StudyProjectDto>,
        IRequestHandler<RemoveStudyProjectDepartmentCommand, StudyProjectDto>,
        IRequestHandler<AddStudyProjectMemberCommand, StudyProjectDto>,
        IRequestHandler<RemoveStudyProjectMemberCommand, StudyProjectDto>
    {
        private readonly IStudyProjectService _service;
        private readonly IStudyStartOrderService _startOrders;
        private readonly IStudyRequestService _requests;
        private readonly IStudyAssignmentService _assignments;
        private readonly IStudyLookupService _lookups;
        private readonly IStudyApprovalService _approvals;
        private readonly IDepartmentService _departments;
        private readonly IUserService _users;
        private readonly StudyKit _kit;

        public StudyProjectHandler(IStudyProjectService service, IStudyStartOrderService startOrders, IStudyRequestService requests,
            IStudyAssignmentService assignments, IStudyLookupService lookups, IStudyApprovalService approvals,
            IDepartmentService departments, IUserService users, StudyKit kit)
        {
            _service = service;
            _startOrders = startOrders;
            _requests = requests;
            _assignments = assignments;
            _lookups = lookups;
            _approvals = approvals;
            _departments = departments;
            _users = users;
            _kit = kit;
        }

        private static bool IsOpen(StudyProject p) => p.Status is StudyProjectStatus.Running or StudyProjectStatus.Suspended;

        private async Task<StudyProject> LoadAsync(int id, Viewer viewer, bool withTeam = false)
        {
            var entity = await _service.GetByIdAsync(id, withTeam) ?? throw new KeyNotFoundException("المشروع غير موجود");
            StudyRules.EnsureInScope(viewer, entity.BranchId);
            return entity;
        }

        private static void EnsureOpen(StudyProject p)
        {
            if (!IsOpen(p)) throw new InvalidOperationException("المشروع مكتمل أو ملغى — لا يُعدَّل");
        }

        private StudyProjectDto ToDto(StudyProject e, Viewer viewer)
        {
            var dto = _kit.Mapper.Map<StudyProjectDto>(e);
            var inScope = StudyRules.InScope(viewer, e.BranchId);
            dto.CanEdit = inScope && viewer.Has(StudyPermissions.EditProject) && IsOpen(e);
            dto.CanDelete = inScope && viewer.Has("DeleteStudyProject") && e.Status == StudyProjectStatus.Running
                            && e.Stages.All(s => s.Tasks.Count == 0);
            dto.CanCancel = inScope && viewer.Has(StudyPermissions.CancelDocuments) && IsOpen(e);
            dto.CanRequestCompletion = dto.CanEdit && e.Status == StudyProjectStatus.Running && e.Stages.Count > 0
                                       && e.Stages.All(s => s.Status == StudyStageStatus.Closed);
            foreach (var d in dto.Departments)
                d.CanManageMembers = viewer.Has("AddStudyProjectMember") && StudyRules.InDepartment(viewer, d.DepartmentId) && IsOpen(e);
            return dto;
        }

        private async Task<StudyProjectDto> DetailAsync(int id, Viewer viewer)
        {
            var entity = await LoadAsync(id, viewer, withTeam: true);
            var dto = ToDto(entity, viewer);
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

        private async Task<int> RequestIdOf(StudyProject p) =>
            p.StartOrder?.Response?.Assignment?.RequestId ?? await _service.GetRequestIdAsync(p.Id);

        public async Task<PagedResultDto<StudyProjectDto>> Handle(SearchStudyProjectsQuery request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var (page, size) = StudyRules.NormalizePaging(request.Filter.Page, request.Filter.PageSize);
            var (items, total) = await _service.SearchAsync(request.Filter, StudyRules.BranchLimit(viewer), page, size);
            return new PagedResultDto<StudyProjectDto>
            {
                Items = items.Select(x => ToDto(x, viewer)).ToList(),
                TotalCount = total,
                Page = page,
                PageSize = size
            };
        }

        public async Task<StudyProjectDto> Handle(GetStudyProjectQuery request, CancellationToken ct) =>
            await DetailAsync(request.Id, await _kit.ViewerAsync());

        public async Task<StudyProjectDto> Handle(CreateStudyProjectCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var order = await _startOrders.GetByIdAsync(request.Dto.StartOrderId) ?? throw new KeyNotFoundException("أمر البدء غير موجود");
            var assignment = order.Response.Assignment;
            var studyRequest = assignment.Request;
            StudyRules.EnsureInScope(viewer, studyRequest.BranchId);
            StudyRules.EnsureApproved(order.Status, "أمر البدء");
            if (await _service.ExistsForStartOrderAsync(order.Id))
                throw new InvalidOperationException("لأمر البدء مشروع مسبقاً — مشروع واحد لكل أمر");

            // الكود يُنسخ من التكليف، ويُطلب هنا إن خلا التكليف منه
            var code = assignment.ProjectCode ?? StudyRequestHandler.Clean(request.Dto.Code)
                ?? throw new InvalidOperationException("لا كود مشروع في التكليف — اكتب كود المشروع");
            if (assignment.ProjectCode == null && await _assignments.ProjectCodeExistsAsync(code, assignment.Id))
                throw new InvalidOperationException("كود المشروع مستخدم في تكليف أو مشروع آخر");
            if (await _service.CodeExistsAsync(code))
                throw new InvalidOperationException("كود المشروع مستخدم في مشروع آخر");

            var now = DateTime.Now;
            var project = new StudyProject
            {
                StartOrderId = order.Id,
                BranchId = studyRequest.BranchId,
                ManagerUserId = order.ProjectManagerUserId,
                CreatedByUserId = viewer.Id,
                Code = code,
                Name = request.Dto.Name.Trim(),
                PlannedStartDate = request.Dto.PlannedStartDate.Date,
                PlannedEndDate = request.Dto.PlannedEndDate.Date,
                ActualStartDate = (request.Dto.ActualStartDate ?? order.StudyStartDate).Date,
                Status = StudyProjectStatus.Running,
                CreatedAt = now
            };

            // المراحل من الأنواع الفعّالة بترتيبها؛ المرتبطة بمستند مغلقة وتواريخها من ذلك المستند
            var types = await _lookups.GetStageTypesAsync(activeOnly: true);
            var order1 = 0;
            foreach (var type in types)
            {
                var stage = new ProjectStage
                {
                    StageTypeId = type.Id,
                    SortOrder = ++order1,
                    Weight = type.DefaultWeight,
                    Status = StudyStageStatus.NotStarted
                };
                (DateTime Start, DateTime End)? dates = type.LinkedDocument switch
                {
                    StudyLinkedDocument.Request => (studyRequest.RequestDate, (studyRequest.UpdatedAt ?? studyRequest.CreatedAt).Date),
                    StudyLinkedDocument.Assignment => (assignment.AssignmentDate, (assignment.UpdatedAt ?? assignment.CreatedAt).Date),
                    StudyLinkedDocument.Response => (order.Response.ResponseDate, (order.Response.UpdatedAt ?? order.Response.CreatedAt).Date),
                    StudyLinkedDocument.StartOrder => (order.OrderDate, (order.UpdatedAt ?? order.CreatedAt).Date),
                    _ => null
                };
                if (dates is { } range)
                {
                    var (start, end) = range;
                    stage.Status = StudyStageStatus.Closed;
                    stage.PlannedStartDate = stage.ActualStartDate = start;
                    stage.PlannedEndDate = stage.ActualEndDate = end < start ? start : end;
                    stage.ClosedByUserId = viewer.Id;
                    stage.ClosedAt = now;
                }
                project.Stages.Add(stage);
            }

            project.Departments.Add(new ProjectDepartment
            {
                DepartmentId = order.CompetentDepartmentId,
                AddedByUserId = viewer.Id,
                AddedAt = now
            });

            await _service.AddAsync(project);
            await _kit.LogAsync(studyRequest.Id, StudyEntityType.Project, project.Id, StudyLogAction.Created,
                $"أنشأ المشروع {project.Code} — {project.Name}", $"المراحل: {project.Stages.Count}");
            return await DetailAsync(project.Id, viewer);
        }

        public async Task<StudyProjectDto> Handle(UpdateStudyProjectCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var entity = await LoadAsync(request.Id, viewer);
            EnsureOpen(entity);
            if (request.Dto.ManagerUserId is int managerId && managerId != entity.ManagerUserId)
            {
                StudyRules.EnsureBranchEmployee(await _users.GetByIdAsync(managerId), entity.BranchId, "مدير المشروع");
                entity.ManagerUserId = managerId;
            }
            entity.Name = request.Dto.Name.Trim();
            entity.PlannedStartDate = request.Dto.PlannedStartDate.Date;
            entity.PlannedEndDate = request.Dto.PlannedEndDate.Date;
            entity.ActualStartDate = request.Dto.ActualStartDate?.Date ?? entity.ActualStartDate;
            if (request.Dto.Status is int status) entity.Status = (StudyProjectStatus)status;
            entity.UpdatedAt = DateTime.Now;
            await _service.UpdateAsync(entity, request.Dto.RowVersion);
            await _kit.LogAsync(await RequestIdOf(entity), StudyEntityType.Project, entity.Id, StudyLogAction.Edited,
                $"عدّل المشروع {entity.Code}", $"الحالة: {StudyRules.Name(entity.Status)}");
            return await DetailAsync(entity.Id, viewer);
        }

        public async Task<Unit> Handle(DeleteStudyProjectCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var entity = await LoadAsync(request.Id, viewer);
            if (entity.Status != StudyProjectStatus.Running)
                throw new InvalidOperationException("لا يُحذف إلا مشروع جارٍ لم يُعمل عليه — غيره يُلغى");
            if (await _service.HasContentAsync(entity.Id))
                throw new InvalidOperationException("للمشروع مهام أو مخططات أو مرفقات — ألغِه بدلاً من حذفه");
            var requestId = await RequestIdOf(entity);
            var code = entity.Code;
            await _service.DeleteAsync(entity);   // المراحل والأقسام والتواقيع تُحذف معه
            await _kit.LogAsync(requestId, StudyEntityType.Project, request.Id, StudyLogAction.Deleted, $"حذف المشروع {code}");
            return Unit.Value;
        }

        public async Task<StudyProjectDto> Handle(CancelStudyProjectCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var entity = await LoadAsync(request.Id, viewer);
            EnsureOpen(entity);
            entity.Status = StudyProjectStatus.Cancelled;
            entity.UpdatedAt = DateTime.Now;
            await _service.UpdateAsync(entity);
            await _kit.LogAsync(await RequestIdOf(entity), StudyEntityType.Project, entity.Id, StudyLogAction.Cancelled,
                $"ألغى المشروع {entity.Code}", request.Reason.Trim());
            return await DetailAsync(entity.Id, viewer);
        }

        public async Task<StudyProjectDto> Handle(RequestStudyProjectCompletionCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var entity = await LoadAsync(request.Id, viewer);
            if (entity.Status != StudyProjectStatus.Running)
                throw new InvalidOperationException("يُطلب اعتماد الإنجاز لمشروع جارٍ فقط");
            if (entity.Stages.Count == 0 || entity.Stages.Any(s => s.Status != StudyStageStatus.Closed))
                throw new InvalidOperationException("أغلق كل مراحل المشروع قبل طلب اعتماد الإنجاز");
            var rows = await _approvals.GetForOwnerAsync(StudyEntityType.Project, entity.Id);
            if (rows.Any(r => r.Decision == StudyDecision.Pending) && rows.All(r => r.Decision != StudyDecision.Rejected))
                throw new InvalidOperationException("طلب اعتماد الإنجاز قيد التوقيع مسبقاً");

            await _kit.ResetApprovalsAsync(StudyEntityType.Project, entity.Id);
            entity.UpdatedAt = DateTime.Now;
            await _service.UpdateAsync(entity);

            var requestId = await RequestIdOf(entity);
            await _kit.LogAsync(requestId, StudyEntityType.Project, entity.Id, StudyLogAction.Submitted,
                $"طلب اعتماد إنجاز المشروع {entity.Code}");
            await _kit.NotifyNextSignersAsync(ApprovalOwner.Of(entity, requestId));
            return await DetailAsync(entity.Id, viewer);
        }

        // ════════════════════ الأقسام المشاركة والفريق ════════════════════

        public async Task<StudyProjectDto> Handle(AddStudyProjectDepartmentCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var project = await LoadAsync(request.ProjectId, viewer);
            EnsureOpen(project);
            var department = await _departments.GetByIdAsync(request.DepartmentId) ?? throw new KeyNotFoundException("القسم غير موجود");
            if (department.BranchId != project.BranchId)
                throw new InvalidOperationException("القسم المشارك يجب أن يكون من أقسام فرع المشروع");
            if (await _service.DepartmentParticipatesAsync(project.Id, department.Id))
                throw new InvalidOperationException("القسم مشارك في المشروع مسبقاً");

            await _service.AddProjectDepartmentAsync(new ProjectDepartment
            {
                ProjectId = project.Id,
                DepartmentId = department.Id,
                AddedByUserId = viewer.Id,
                AddedAt = DateTime.Now
            });
            await _kit.LogAsync(await RequestIdOf(project), StudyEntityType.ProjectDepartment, project.Id, StudyLogAction.Created,
                $"أضاف القسم {department.Name} إلى المشروع {project.Code}");
            return await DetailAsync(project.Id, viewer);
        }

        public async Task<StudyProjectDto> Handle(RemoveStudyProjectDepartmentCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var participation = await _service.GetProjectDepartmentAsync(request.ProjectDepartmentId)
                ?? throw new KeyNotFoundException("القسم غير مشارك في المشروع");
            var project = await LoadAsync(participation.ProjectId, viewer);
            EnsureOpen(project);
            if (participation.Members.Count > 0)
                throw new InvalidOperationException("للقسم أعضاء في فريق المشروع — أزلهم أولاً");
            if (await _service.HasTasksForDepartmentAsync(project.Id, participation.DepartmentId))
                throw new InvalidOperationException("للقسم أو مكاتبه مهام في المشروع — انقلها أو احذفها أولاً");

            var name = participation.Department.Name;
            await _service.RemoveProjectDepartmentAsync(participation);
            await _kit.LogAsync(await RequestIdOf(project), StudyEntityType.ProjectDepartment, project.Id, StudyLogAction.Deleted,
                $"أزال القسم {name} من المشروع {project.Code}");
            return await DetailAsync(project.Id, viewer);
        }

        /// <summary>رئيس القسم المشارك يعيّن موظفي قسمه فقط (مدير النظام بلا حدّ)</summary>
        private async Task<(ProjectDepartment Participation, StudyProject Project)> LoadParticipationForMembersAsync(int projectDepartmentId, Viewer viewer)
        {
            var participation = await _service.GetProjectDepartmentAsync(projectDepartmentId)
                ?? throw new KeyNotFoundException("القسم غير مشارك في المشروع");
            var project = await LoadAsync(participation.ProjectId, viewer);
            EnsureOpen(project);
            if (!StudyRules.InDepartment(viewer, participation.DepartmentId))
                throw new UnauthorizedAccessException("يعيّن رئيس القسم المشارك موظفي قسمه فقط");
            return (participation, project);
        }

        public async Task<StudyProjectDto> Handle(AddStudyProjectMemberCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var (participation, project) = await LoadParticipationForMembersAsync(request.ProjectDepartmentId, viewer);
            var user = await _users.GetByIdAsync(request.Dto.UserId);
            if (user is not { IsActive: true }) throw new KeyNotFoundException("الموظف غير موجود أو غير نشط");
            if (user.DepartmentId != participation.DepartmentId)
                throw new InvalidOperationException("الموظف ليس من القسم المشارك");
            if (await _service.IsMemberAsync(project.Id, user.Id))
                throw new InvalidOperationException("الموظف عضو في فريق المشروع مسبقاً");

            await _service.AddMemberAsync(new ProjectMember
            {
                ProjectDepartmentId = participation.Id,
                UserId = user.Id,
                RoleTitle = StudyRequestHandler.Clean(request.Dto.RoleTitle),
                AssignedByUserId = viewer.Id,
                AssignedAt = DateTime.Now
            });
            await _kit.LogAsync(await RequestIdOf(project), StudyEntityType.ProjectMember, project.Id, StudyLogAction.Created,
                $"عيّن {user.FullName} في فريق المشروع {project.Code}", request.Dto.RoleTitle);
            return await DetailAsync(project.Id, viewer);
        }

        public async Task<StudyProjectDto> Handle(RemoveStudyProjectMemberCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var (participation, project) = await LoadParticipationForMembersAsync(request.ProjectDepartmentId, viewer);
            var member = participation.Members.FirstOrDefault(m => m.UserId == request.UserId)
                ?? throw new KeyNotFoundException("الموظف ليس عضواً في فريق القسم");
            if (await _service.HasTasksForUserAsync(project.Id, request.UserId))
                throw new InvalidOperationException("للعضو مهام في المشروع — انقلها أولاً");

            await _service.RemoveMemberAsync(member);
            await _kit.LogAsync(await RequestIdOf(project), StudyEntityType.ProjectMember, project.Id, StudyLogAction.Deleted,
                $"أزال عضواً من فريق المشروع {project.Code}", $"المستخدم رقم {request.UserId}");
            return await DetailAsync(project.Id, viewer);
        }
    }
}
