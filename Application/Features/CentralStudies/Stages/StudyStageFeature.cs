using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.CentralStudies.Requests;
using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;
using FluentValidation;
using MediatR;

namespace Application.Features.CentralStudies.Stages
{
    // مراحل المشروع (متتالية: لا تُغلق مرحلة قبل ما قبلها، وتغلقها إدارة المشاريع) ومهامها (حرّة داخل المرحلة).
    // مهام المراحل منفصلة عن لوحة مهام EWMS وبنفس آلية الإسناد: قسم مشارك، مكتب منه، أو عضو في الفريق.
    public record GetStudyStagesQuery(int ProjectId) : IRequest<List<ProjectStageDto>>;
    public record CreateStudyStageCommand(StudyStageFormDto Dto) : IRequest<ProjectStageDto>;
    public record UpdateStudyStageCommand(int Id, StudyStageFormDto Dto) : IRequest<ProjectStageDto>;
    public record DeleteStudyStageCommand(int Id) : IRequest<Unit>;
    public record ReorderStudyStagesCommand(int ProjectId, List<int> StageIds) : IRequest<List<ProjectStageDto>>;
    public record CloseStudyStageCommand(int Id) : IRequest<ProjectStageDto>;
    public record ReopenStudyStageCommand(int Id) : IRequest<ProjectStageDto>;

    public record GetStudyTasksQuery(int StageId) : IRequest<List<StageTaskDto>>;
    public record GetStudyTaskQuery(int Id) : IRequest<StageTaskDto>;
    public record GetMyStudyTasksQuery(bool OpenOnly, int Page, int PageSize) : IRequest<PagedResultDto<StageTaskDto>>;
    public record CreateStudyTaskCommand(StudyTaskFormDto Dto) : IRequest<StageTaskDto>;
    public record UpdateStudyTaskCommand(int Id, StudyTaskFormDto Dto) : IRequest<StageTaskDto>;
    public record DeleteStudyTaskCommand(int Id) : IRequest<Unit>;
    public record UpdateStudyTaskProgressCommand(int Id, StudyTaskProgressDto Dto) : IRequest<StageTaskDto>;

    // ════════════════════ التحقق ════════════════════

    public class StudyStageFormValidator : AbstractValidator<StudyStageFormDto>
    {
        public StudyStageFormValidator()
        {
            RuleFor(x => x.Weight).InclusiveBetween(0, 100).WithMessage("وزن المرحلة بين 0 و100");
            RuleFor(x => x.PlannedEndDate).GreaterThanOrEqualTo(x => x.PlannedStartDate)
                .When(x => x.PlannedStartDate != null && x.PlannedEndDate != null).WithMessage("الانتهاء المخطط لا يسبق البدء");
            RuleFor(x => x.Status).Must(s => s is null or (int)StudyStageStatus.NotStarted or (int)StudyStageStatus.InProgress)
                .WithMessage("الحالة «لم تبدأ» أو «جارية» — الإغلاق بأمر الإغلاق");
        }
    }

    public class CreateStudyStageValidator : AbstractValidator<CreateStudyStageCommand>
    {
        public CreateStudyStageValidator()
        {
            RuleFor(x => x.Dto).SetValidator(new StudyStageFormValidator());
            RuleFor(x => x.Dto.ProjectId).GreaterThan(0).WithMessage("حدد المشروع");
            RuleFor(x => x.Dto.StageTypeId).GreaterThan(0).WithMessage("اختر نوع المرحلة");
        }
    }

    public class UpdateStudyStageValidator : AbstractValidator<UpdateStudyStageCommand>
    {
        public UpdateStudyStageValidator() => RuleFor(x => x.Dto).SetValidator(new StudyStageFormValidator());
    }

    public class StudyTaskFormValidator : AbstractValidator<StudyTaskFormDto>
    {
        public StudyTaskFormValidator()
        {
            RuleFor(x => x.TargetType).Must(v => Enum.IsDefined(typeof(StudyTaskTarget), v)).WithMessage("حدد جهة الإسناد");
            RuleFor(x => x.DepartmentId).NotNull().When(x => x.TargetType == (int)StudyTaskTarget.Department).WithMessage("اختر القسم");
            RuleFor(x => x.OfficeId).NotNull().When(x => x.TargetType == (int)StudyTaskTarget.Office).WithMessage("اختر المكتب");
            RuleFor(x => x.AssigneeUserId).NotNull().When(x => x.TargetType == (int)StudyTaskTarget.User).WithMessage("اختر الموظف");
            RuleFor(x => x.Title).NotEmpty().WithMessage("عنوان المهمة مطلوب").MaximumLength(200).WithMessage("العنوان لا يتجاوز 200 حرف");
            RuleFor(x => x.Description).MaximumLength(4000).WithMessage("الوصف لا يتجاوز 4000 حرف");
            RuleFor(x => x.Weight).InclusiveBetween(0, 100).WithMessage("وزن المهمة بين 0 و100");
            RuleFor(x => x.PlannedEndDate).GreaterThanOrEqualTo(x => x.PlannedStartDate)
                .When(x => x.PlannedStartDate != null && x.PlannedEndDate != null).WithMessage("الانتهاء المخطط لا يسبق البدء");
        }
    }

    public class CreateStudyTaskValidator : AbstractValidator<CreateStudyTaskCommand>
    {
        public CreateStudyTaskValidator()
        {
            RuleFor(x => x.Dto).SetValidator(new StudyTaskFormValidator());
            RuleFor(x => x.Dto.StageId).GreaterThan(0).WithMessage("حدد المرحلة");
        }
    }

    public class UpdateStudyTaskValidator : AbstractValidator<UpdateStudyTaskCommand>
    {
        public UpdateStudyTaskValidator() => RuleFor(x => x.Dto).SetValidator(new StudyTaskFormValidator());
    }

    public class UpdateStudyTaskProgressValidator : AbstractValidator<UpdateStudyTaskProgressCommand>
    {
        public UpdateStudyTaskProgressValidator()
        {
            RuleFor(x => x.Dto.Progress).InclusiveBetween(0, 100).WithMessage("نسبة الإنجاز بين 0 و100");
            RuleFor(x => x.Dto.Status).Must(v => Enum.IsDefined(typeof(StudyTaskStatus), v)).WithMessage("حالة المهمة غير صالحة");
        }
    }

    // ════════════════════ المعالج ════════════════════

    public class StudyStageHandler :
        IRequestHandler<GetStudyStagesQuery, List<ProjectStageDto>>,
        IRequestHandler<CreateStudyStageCommand, ProjectStageDto>,
        IRequestHandler<UpdateStudyStageCommand, ProjectStageDto>,
        IRequestHandler<DeleteStudyStageCommand, Unit>,
        IRequestHandler<ReorderStudyStagesCommand, List<ProjectStageDto>>,
        IRequestHandler<CloseStudyStageCommand, ProjectStageDto>,
        IRequestHandler<ReopenStudyStageCommand, ProjectStageDto>,
        IRequestHandler<GetStudyTasksQuery, List<StageTaskDto>>,
        IRequestHandler<GetStudyTaskQuery, StageTaskDto>,
        IRequestHandler<GetMyStudyTasksQuery, PagedResultDto<StageTaskDto>>,
        IRequestHandler<CreateStudyTaskCommand, StageTaskDto>,
        IRequestHandler<UpdateStudyTaskCommand, StageTaskDto>,
        IRequestHandler<DeleteStudyTaskCommand, Unit>,
        IRequestHandler<UpdateStudyTaskProgressCommand, StageTaskDto>
    {
        private readonly IStudyStageService _service;
        private readonly IStudyProjectService _projects;
        private readonly IStudyLookupService _lookups;
        private readonly IDepartmentService _departments;
        private readonly IOfficeService _offices;
        private readonly StudyKit _kit;

        public StudyStageHandler(IStudyStageService service, IStudyProjectService projects, IStudyLookupService lookups,
            IDepartmentService departments, IOfficeService offices, StudyKit kit)
        {
            _service = service;
            _projects = projects;
            _lookups = lookups;
            _departments = departments;
            _offices = offices;
            _kit = kit;
        }

        private static bool IsOpen(StudyProject p) => p.Status is StudyProjectStatus.Running or StudyProjectStatus.Suspended;

        private static void EnsureOpen(StudyProject p)
        {
            if (!IsOpen(p)) throw new InvalidOperationException("المشروع مكتمل أو ملغى — لا تُعدَّل مراحله ومهامه");
        }

        private static void EnsureStageOpen(ProjectStage s)
        {
            if (s.Status == StudyStageStatus.Closed) throw new InvalidOperationException("المرحلة مغلقة — أعد فتحها أولاً");
        }

        private async Task<ProjectStage> LoadStageAsync(int id, Viewer viewer)
        {
            var stage = await _service.GetStageAsync(id) ?? throw new KeyNotFoundException("المرحلة غير موجودة");
            StudyRules.EnsureInScope(viewer, stage.Project.BranchId);
            return stage;
        }

        private async Task<StudyProject> LoadProjectAsync(int id, Viewer viewer)
        {
            var project = await _projects.GetByIdAsync(id) ?? throw new KeyNotFoundException("المشروع غير موجود");
            StudyRules.EnsureInScope(viewer, project.BranchId);
            return project;
        }

        private ProjectStageDto StageDto(ProjectStage stage) => _kit.Mapper.Map<ProjectStageDto>(stage);

        private async Task<ProjectStageDto> StageDetailAsync(int id) =>
            StageDto(await _service.GetStageAsync(id) ?? throw new KeyNotFoundException("المرحلة غير موجودة"));

        private async Task LogAsync(int projectId, StudyEntityType type, int id, StudyLogAction action, string title, string? details = null) =>
            await _kit.LogAsync(await _projects.GetRequestIdAsync(projectId), type, id, action, title, details);

        // ════════════════════ المراحل ════════════════════

        public async Task<List<ProjectStageDto>> Handle(GetStudyStagesQuery request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            await LoadProjectAsync(request.ProjectId, viewer);
            return (await _service.GetStagesAsync(request.ProjectId)).Select(StageDto).ToList();
        }

        private async Task EnsureResponsibleAsync(int? departmentId, int branchId)
        {
            if (departmentId is not int id) return;
            var department = await _departments.GetByIdAsync(id) ?? throw new KeyNotFoundException("القسم المسؤول غير موجود");
            if (department.BranchId != branchId) throw new InvalidOperationException("القسم المسؤول يجب أن يكون من أقسام فرع المشروع");
        }

        public async Task<ProjectStageDto> Handle(CreateStudyStageCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var project = await LoadProjectAsync(request.Dto.ProjectId, viewer);
            EnsureOpen(project);
            var type = await _lookups.GetStageTypeAsync(request.Dto.StageTypeId);
            if (type is not { IsActive: true }) throw new KeyNotFoundException("نوع المرحلة غير موجود أو غير فعّال");
            if (project.Stages.Any(s => s.StageTypeId == type.Id))
                throw new InvalidOperationException("نوع المرحلة موجود في المشروع مسبقاً");
            await EnsureResponsibleAsync(request.Dto.ResponsibleDepartmentId, project.BranchId);

            var stage = new ProjectStage
            {
                ProjectId = project.Id,
                StageTypeId = type.Id,
                SortOrder = project.Stages.Count == 0 ? 1 : project.Stages.Max(s => s.SortOrder) + 1,
                Weight = request.Dto.Weight,
                PlannedStartDate = request.Dto.PlannedStartDate?.Date,
                PlannedEndDate = request.Dto.PlannedEndDate?.Date,
                ActualStartDate = request.Dto.ActualStartDate?.Date,
                ResponsibleDepartmentId = request.Dto.ResponsibleDepartmentId,
                Status = (StudyStageStatus?)request.Dto.Status ?? StudyStageStatus.NotStarted
            };
            await _service.AddStageAsync(stage);
            await LogAsync(project.Id, StudyEntityType.Stage, stage.Id, StudyLogAction.Created, $"أضاف المرحلة «{type.Name}» إلى المشروع {project.Code}");
            return await StageDetailAsync(stage.Id);
        }

        public async Task<ProjectStageDto> Handle(UpdateStudyStageCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var stage = await LoadStageAsync(request.Id, viewer);
            EnsureOpen(stage.Project);
            await EnsureResponsibleAsync(request.Dto.ResponsibleDepartmentId, stage.Project.BranchId);

            stage.Weight = request.Dto.Weight;
            stage.PlannedStartDate = request.Dto.PlannedStartDate?.Date;
            stage.PlannedEndDate = request.Dto.PlannedEndDate?.Date;
            stage.ResponsibleDepartmentId = request.Dto.ResponsibleDepartmentId;
            if (stage.Status != StudyStageStatus.Closed)
            {
                stage.ActualStartDate = request.Dto.ActualStartDate?.Date ?? stage.ActualStartDate;
                if (request.Dto.Status is int status)
                {
                    stage.Status = (StudyStageStatus)status;
                    if (stage.Status == StudyStageStatus.InProgress) stage.ActualStartDate ??= DateTime.Today;
                }
            }
            await _service.UpdateStageAsync(stage, request.Dto.RowVersion);
            await LogAsync(stage.ProjectId, StudyEntityType.Stage, stage.Id, StudyLogAction.Edited, $"عدّل المرحلة «{stage.StageType.Name}»");
            return await StageDetailAsync(stage.Id);
        }

        public async Task<Unit> Handle(DeleteStudyStageCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var stage = await LoadStageAsync(request.Id, viewer);
            EnsureOpen(stage.Project);
            if (stage.StageType.LinkedDocument != StudyLinkedDocument.None)
                throw new InvalidOperationException("مرحلة المستند (الطلب/التكليف/الرد/أمر البدء) لا تُحذف");
            if (stage.Tasks.Count > 0) throw new InvalidOperationException("للمرحلة مهام — احذفها أولاً");
            if (stage.Status == StudyStageStatus.Closed) throw new InvalidOperationException("المرحلة مغلقة — لا تُحذف");

            var (projectId, name) = (stage.ProjectId, stage.StageType.Name);
            await _service.DeleteStageAsync(stage);
            await LogAsync(projectId, StudyEntityType.Stage, request.Id, StudyLogAction.Deleted, $"حذف المرحلة «{name}»");
            return Unit.Value;
        }

        public async Task<List<ProjectStageDto>> Handle(ReorderStudyStagesCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var project = await LoadProjectAsync(request.ProjectId, viewer);
            EnsureOpen(project);
            var stages = await _service.GetStagesAsync(project.Id);
            if (request.StageIds.Count != stages.Count || request.StageIds.Distinct().Count() != stages.Count
                || stages.Any(s => !request.StageIds.Contains(s.Id)))
                throw new ArgumentException("أرسل كل مراحل المشروع بالترتيب الجديد");

            // المراحل متتالية: لا تسبق مرحلة مفتوحة مرحلة مغلقة
            var ordered = request.StageIds.Select(id => stages.First(s => s.Id == id)).ToList();
            var firstOpen = ordered.FindIndex(s => s.Status != StudyStageStatus.Closed);
            if (firstOpen >= 0 && ordered.Skip(firstOpen).Any(s => s.Status == StudyStageStatus.Closed))
                throw new InvalidOperationException("المراحل المغلقة تبقى قبل المفتوحة — المراحل متتالية");

            for (var i = 0; i < ordered.Count; i++) ordered[i].SortOrder = i + 1;
            await _service.SaveAsync();
            await LogAsync(project.Id, StudyEntityType.Project, project.Id, StudyLogAction.Edited, $"أعاد ترتيب مراحل المشروع {project.Code}");
            return (await _service.GetStagesAsync(project.Id)).Select(StageDto).ToList();
        }

        public async Task<ProjectStageDto> Handle(CloseStudyStageCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var stage = await LoadStageAsync(request.Id, viewer);
            EnsureOpen(stage.Project);
            EnsureStageOpen(stage);
            var stages = await _service.GetStagesAsync(stage.ProjectId);
            if (stages.Any(s => s.SortOrder < stage.SortOrder && s.Status != StudyStageStatus.Closed))
                throw new InvalidOperationException("المراحل متتالية — أغلق المراحل السابقة أولاً");
            if (stage.Tasks.Any(t => t.Status != StudyTaskStatus.Done))
                throw new InvalidOperationException("في المرحلة مهام غير منجزة");

            stage.Status = StudyStageStatus.Closed;
            stage.ClosedByUserId = viewer.Id;
            stage.ClosedAt = DateTime.Now;
            stage.ActualStartDate ??= stage.Tasks.Min(t => t.ActualStartDate) ?? DateTime.Today;
            stage.ActualEndDate ??= DateTime.Today;
            await _service.UpdateStageAsync(stage);
            await LogAsync(stage.ProjectId, StudyEntityType.Stage, stage.Id, StudyLogAction.Closed, $"أغلق المرحلة «{stage.StageType.Name}»");
            return await StageDetailAsync(stage.Id);
        }

        public async Task<ProjectStageDto> Handle(ReopenStudyStageCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var stage = await LoadStageAsync(request.Id, viewer);
            EnsureOpen(stage.Project);
            if (stage.Status != StudyStageStatus.Closed) throw new InvalidOperationException("المرحلة غير مغلقة");
            if (stage.StageType.LinkedDocument != StudyLinkedDocument.None)
                throw new InvalidOperationException("مرحلة المستند أُغلقت من المستند المعتمد — لا يُعاد فتحها");
            var stages = await _service.GetStagesAsync(stage.ProjectId);
            if (stages.Any(s => s.SortOrder > stage.SortOrder && s.Status == StudyStageStatus.Closed))
                throw new InvalidOperationException("أُغلقت مراحل بعدها — يُعاد فتح آخر مرحلة مغلقة فقط");

            stage.Status = StudyStageStatus.InProgress;
            stage.ClosedByUserId = null;
            stage.ClosedAt = null;
            stage.ActualEndDate = null;
            await _service.UpdateStageAsync(stage);
            await LogAsync(stage.ProjectId, StudyEntityType.Stage, stage.Id, StudyLogAction.Reopened, $"أعاد فتح المرحلة «{stage.StageType.Name}»");
            return await StageDetailAsync(stage.Id);
        }

        // ════════════════════ المهام ════════════════════

        /// <summary>المهمة مسندة إلى المستخدم أو مكتبه أو قسمه (للقسم: موظفوه المباشرون بلا مكتب)</summary>
        private static bool IsTarget(Viewer viewer, StageTask t) =>
            viewer.IsSuperAdmin
            || (t.TargetType == StudyTaskTarget.User && t.AssigneeUserId == viewer.Id)
            || (t.TargetType == StudyTaskTarget.Office && t.OfficeId != null && t.OfficeId == viewer.User.OfficeId)
            || (t.TargetType == StudyTaskTarget.Department && t.DepartmentId != null && viewer.User.OfficeId == null
                && t.DepartmentId == viewer.User.DepartmentId);

        private StageTaskDto TaskDto(StageTask t, Viewer viewer)
        {
            var dto = _kit.Mapper.Map<StageTaskDto>(t);
            var editable = IsOpen(t.Stage.Project) && t.Stage.Status != StudyStageStatus.Closed;
            dto.CanEdit = editable && viewer.Has("EditStudyTask") && StudyRules.InScope(viewer, t.Stage.Project.BranchId);
            dto.CanUpdateProgress = editable && viewer.Has(StudyPermissions.UpdateTaskProgress) && IsTarget(viewer, t);
            return dto;
        }

        private async Task<StageTaskDto> TaskDetailAsync(int id, Viewer viewer) =>
            TaskDto(await _service.GetTaskAsync(id) ?? throw new KeyNotFoundException("المهمة غير موجودة"), viewer);

        /// <summary>الجهة من فريق المشروع: قسم مشارك، مكتب من قسم مشارك، أو عضو في الفريق</summary>
        private async Task ValidateTargetAsync(StudyTaskFormDto dto, int projectId)
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

        private static void ApplyTarget(StudyTaskFormDto dto, StageTask t)
        {
            t.TargetType = (StudyTaskTarget)dto.TargetType;
            t.DepartmentId = t.TargetType == StudyTaskTarget.Department ? dto.DepartmentId : null;
            t.OfficeId = t.TargetType == StudyTaskTarget.Office ? dto.OfficeId : null;
            t.AssigneeUserId = t.TargetType == StudyTaskTarget.User ? dto.AssigneeUserId : null;
        }

        public async Task<List<StageTaskDto>> Handle(GetStudyTasksQuery request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            await LoadStageAsync(request.StageId, viewer);
            return (await _service.GetTasksAsync(request.StageId)).Select(t => TaskDto(t, viewer)).ToList();
        }

        /// <summary>يراها من يرى مشاريع فرعه، أو المكلَّف بها</summary>
        public async Task<StageTaskDto> Handle(GetStudyTaskQuery request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var task = await _service.GetTaskAsync(request.Id) ?? throw new KeyNotFoundException("المهمة غير موجودة");
            var canSee = (viewer.Has(StudyPermissions.ViewProjects) && StudyRules.InScope(viewer, task.Stage.Project.BranchId))
                         || (viewer.Has(StudyPermissions.UpdateTaskProgress) && IsTarget(viewer, task));
            if (!canSee) throw new UnauthorizedAccessException("لا تملك صلاحية عرض هذه المهمة");
            return TaskDto(task, viewer);
        }

        public async Task<PagedResultDto<StageTaskDto>> Handle(GetMyStudyTasksQuery request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var (page, size) = StudyRules.NormalizePaging(request.Page, request.PageSize);
            var (items, total) = await _service.GetAssignedAsync(viewer.Id, viewer.User.OfficeId, viewer.User.DepartmentId,
                request.OpenOnly, page, size);
            return new PagedResultDto<StageTaskDto>
            {
                Items = items.Select(t => TaskDto(t, viewer)).ToList(),
                TotalCount = total,
                Page = page,
                PageSize = size
            };
        }

        public async Task<StageTaskDto> Handle(CreateStudyTaskCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var stage = await LoadStageAsync(request.Dto.StageId, viewer);
            EnsureOpen(stage.Project);
            EnsureStageOpen(stage);
            await ValidateTargetAsync(request.Dto, stage.ProjectId);

            var task = new StageTask
            {
                StageId = stage.Id,
                AssignedByUserId = viewer.Id,
                Title = request.Dto.Title.Trim(),
                Description = StudyRequestHandler.Clean(request.Dto.Description),
                PlannedStartDate = request.Dto.PlannedStartDate?.Date,
                PlannedEndDate = request.Dto.PlannedEndDate?.Date,
                Weight = request.Dto.Weight,
                Progress = 0,
                Status = StudyTaskStatus.NotStarted,
                CreatedAt = DateTime.Now
            };
            ApplyTarget(request.Dto, task);
            await _service.AddTaskAsync(task);
            await LogAsync(stage.ProjectId, StudyEntityType.Task, task.Id, StudyLogAction.Created,
                $"أضاف المهمة «{task.Title}» في المرحلة «{stage.StageType.Name}»");
            await _kit.NotifyTaskAssignedAsync(task, stage.Project.Code);
            return await TaskDetailAsync(task.Id, viewer);
        }

        public async Task<StageTaskDto> Handle(UpdateStudyTaskCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var task = await _service.GetTaskAsync(request.Id) ?? throw new KeyNotFoundException("المهمة غير موجودة");
            StudyRules.EnsureInScope(viewer, task.Stage.Project.BranchId);
            EnsureOpen(task.Stage.Project);
            EnsureStageOpen(task.Stage);
            await ValidateTargetAsync(request.Dto, task.Stage.ProjectId);

            var oldTarget = (task.TargetType, task.DepartmentId, task.OfficeId, task.AssigneeUserId);
            task.Title = request.Dto.Title.Trim();
            task.Description = StudyRequestHandler.Clean(request.Dto.Description);
            task.PlannedStartDate = request.Dto.PlannedStartDate?.Date;
            task.PlannedEndDate = request.Dto.PlannedEndDate?.Date;
            task.Weight = request.Dto.Weight;
            ApplyTarget(request.Dto, task);
            task.UpdatedAt = DateTime.Now;
            await _service.UpdateTaskAsync(task, request.Dto.RowVersion);

            await LogAsync(task.Stage.ProjectId, StudyEntityType.Task, task.Id, StudyLogAction.Edited, $"عدّل المهمة «{task.Title}»");
            if (oldTarget != (task.TargetType, task.DepartmentId, task.OfficeId, task.AssigneeUserId))
                await _kit.NotifyTaskAssignedAsync(task, task.Stage.Project.Code);
            return await TaskDetailAsync(task.Id, viewer);
        }

        public async Task<Unit> Handle(DeleteStudyTaskCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var task = await _service.GetTaskAsync(request.Id) ?? throw new KeyNotFoundException("المهمة غير موجودة");
            StudyRules.EnsureInScope(viewer, task.Stage.Project.BranchId);
            EnsureOpen(task.Stage.Project);
            EnsureStageOpen(task.Stage);
            var (projectId, title) = (task.Stage.ProjectId, task.Title);
            await _service.DeleteTaskAsync(task);
            await LogAsync(projectId, StudyEntityType.Task, request.Id, StudyLogAction.Deleted, $"حذف المهمة «{title}»");
            return Unit.Value;
        }

        public async Task<StageTaskDto> Handle(UpdateStudyTaskProgressCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var task = await _service.GetTaskAsync(request.Id) ?? throw new KeyNotFoundException("المهمة غير موجودة");
            if (!IsTarget(viewer, task)) throw new UnauthorizedAccessException("يحدّث الإنجاز المكلَّف بالمهمة فقط");
            EnsureOpen(task.Stage.Project);
            EnsureStageOpen(task.Stage);

            var status = (StudyTaskStatus)request.Dto.Status;
            var progress = Math.Round(request.Dto.Progress, 1);
            if (status == StudyTaskStatus.Done) progress = 100;
            else if (progress >= 100) status = StudyTaskStatus.Done;
            else if (progress > 0 && status == StudyTaskStatus.NotStarted) status = StudyTaskStatus.InProgress;

            task.Progress = progress;
            task.Status = status;
            if (status != StudyTaskStatus.NotStarted) task.ActualStartDate ??= DateTime.Today;
            task.ActualEndDate = status == StudyTaskStatus.Done ? task.ActualEndDate ?? DateTime.Today : null;
            task.UpdatedAt = DateTime.Now;

            // أول عمل على مرحلة لم تبدأ يجعلها جارية
            if (status != StudyTaskStatus.NotStarted && task.Stage.Status == StudyStageStatus.NotStarted)
            {
                task.Stage.Status = StudyStageStatus.InProgress;
                task.Stage.ActualStartDate ??= DateTime.Today;
            }
            await _service.UpdateTaskAsync(task);
            await LogAsync(task.Stage.ProjectId, StudyEntityType.Task, task.Id, StudyLogAction.Edited,
                $"حدّث إنجاز المهمة «{task.Title}»", $"{StudyRules.Name(status)} — {progress}%");
            return await TaskDetailAsync(task.Id, viewer);
        }
    }
}
