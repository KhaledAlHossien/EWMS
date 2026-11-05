using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.CentralStudies.Requests;
using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;
using FluentValidation;
using MediatR;

namespace Application.Features.CentralStudies.Drawings
{
    // المخططات: كل نوع صنف مستقل (TPH). القسم المُصدِر قسم مشارك في المشروع، ويعدّل مخططاته قسمه فقط.
    // المعماري يصدر أولاً بنسخ Draft-N يعتمد كلاً منها CDA (القسم المرسل ← إدارة التصميم ← ضبط الجودة)،
    // وغير المعماري «مبني على» نسخة معمارية معتمدة، وتراجعه صلاحية ReviewStudyDrawing.
    public record GetStudyDrawingsQuery(int ProjectId, int? Kind) : IRequest<List<StudyDrawingDto>>;
    public record GetStudyDrawingQuery(int Id) : IRequest<StudyDrawingDto>;
    public record CreateStudyDrawingCommand(StudyDrawingFormDto Dto) : IRequest<StudyDrawingDto>;
    public record UpdateStudyDrawingCommand(int Id, StudyDrawingFormDto Dto) : IRequest<StudyDrawingDto>;
    public record DeleteStudyDrawingCommand(int Id) : IRequest<Unit>;
    public record ReviewStudyDrawingCommand(int Id, StudyDrawingReviewDto Dto) : IRequest<StudyDrawingDto>;

    public record CreateArchitecturalVersionCommand(int DrawingId, ArchitecturalVersionFormDto Dto) : IRequest<StudyDrawingDto>;
    public record UpdateArchitecturalVersionCommand(int Id, ArchitecturalVersionFormDto Dto) : IRequest<StudyDrawingDto>;
    public record DeleteArchitecturalVersionCommand(int Id) : IRequest<StudyDrawingDto>;

    public record GetDesignApprovalQuery(int Id) : IRequest<DesignApprovalDto>;
    public record CreateDesignApprovalCommand(DesignApprovalFormDto Dto) : IRequest<DesignApprovalDto>;
    public record UpdateDesignApprovalCommand(int Id, DesignApprovalFormDto Dto) : IRequest<DesignApprovalDto>;
    public record DeleteDesignApprovalCommand(int Id) : IRequest<Unit>;

    // ════════════════════ التحقق ════════════════════

    public class StudyDrawingFormValidator : AbstractValidator<StudyDrawingFormDto>
    {
        public StudyDrawingFormValidator()
        {
            RuleFor(x => x.Name).NotEmpty().WithMessage("اسم المخطط مطلوب").MaximumLength(200).WithMessage("الاسم لا يتجاوز 200 حرف");
            RuleFor(x => x.DrawingNumber).NotEmpty().WithMessage("رقم المخطط مطلوب").MaximumLength(50).WithMessage("الرقم لا يتجاوز 50 حرفاً");
            RuleFor(x => x.DrawingDate).NotEmpty().WithMessage("تاريخ المخطط مطلوب");
            RuleFor(x => x.Description).MaximumLength(2000).WithMessage("الوصف لا يتجاوز 2000 حرف");
            RuleFor(x => x.Status).Must(s => s is null or (int)DrawingStatus.InPreparation or (int)DrawingStatus.PendingReview)
                .WithMessage("الحالة «قيد الإعداد» أو «بانتظار المراجعة» — الاعتماد بالمراجعة أو CDA");
        }
    }

    public class CreateStudyDrawingValidator : AbstractValidator<CreateStudyDrawingCommand>
    {
        public CreateStudyDrawingValidator()
        {
            RuleFor(x => x.Dto).SetValidator(new StudyDrawingFormValidator());
            RuleFor(x => x.Dto.ProjectId).GreaterThan(0).WithMessage("حدد المشروع");
            RuleFor(x => x.Dto.Kind).Must(v => Enum.IsDefined(typeof(DrawingKind), v)).WithMessage("حدد نوع المخطط");
        }
    }

    public class UpdateStudyDrawingValidator : AbstractValidator<UpdateStudyDrawingCommand>
    {
        public UpdateStudyDrawingValidator() => RuleFor(x => x.Dto).SetValidator(new StudyDrawingFormValidator());
    }

    public class ReviewStudyDrawingValidator : AbstractValidator<ReviewStudyDrawingCommand>
    {
        public ReviewStudyDrawingValidator()
        {
            RuleFor(x => x.Dto.Note).NotEmpty().When(x => !x.Dto.Approve).WithMessage("اذكر سبب الرفض");
            RuleFor(x => x.Dto.Note).MaximumLength(1000).WithMessage("الملاحظة لا تتجاوز 1000 حرف");
        }
    }

    public class ArchitecturalVersionFormValidator : AbstractValidator<ArchitecturalVersionFormDto>
    {
        public ArchitecturalVersionFormValidator()
        {
            RuleFor(x => x.VersionDate).NotEmpty().WithMessage("تاريخ النسخة مطلوب");
            RuleFor(x => x.Content).MaximumLength(4000).WithMessage("المحتوى لا يتجاوز 4000 حرف");
        }
    }

    public class CreateArchitecturalVersionValidator : AbstractValidator<CreateArchitecturalVersionCommand>
    {
        public CreateArchitecturalVersionValidator() => RuleFor(x => x.Dto).SetValidator(new ArchitecturalVersionFormValidator());
    }

    public class UpdateArchitecturalVersionValidator : AbstractValidator<UpdateArchitecturalVersionCommand>
    {
        public UpdateArchitecturalVersionValidator() => RuleFor(x => x.Dto).SetValidator(new ArchitecturalVersionFormValidator());
    }

    public class DesignApprovalFormValidator : AbstractValidator<DesignApprovalFormDto>
    {
        public DesignApprovalFormValidator()
        {
            RuleFor(x => x.CdaDate).NotEmpty().WithMessage("تاريخ الاعتماد مطلوب");
            RuleFor(x => x.Content).MaximumLength(4000).WithMessage("المحتوى لا يتجاوز 4000 حرف");
            RuleFor(x => x.TechnicalNotes).MaximumLength(4000).WithMessage("الملاحظات الفنية لا تتجاوز 4000 حرف");
        }
    }

    public class CreateDesignApprovalValidator : AbstractValidator<CreateDesignApprovalCommand>
    {
        public CreateDesignApprovalValidator()
        {
            RuleFor(x => x.Dto).SetValidator(new DesignApprovalFormValidator());
            RuleFor(x => x.Dto.VersionId).GreaterThan(0).WithMessage("حدد النسخة المعمارية");
        }
    }

    public class UpdateDesignApprovalValidator : AbstractValidator<UpdateDesignApprovalCommand>
    {
        public UpdateDesignApprovalValidator() => RuleFor(x => x.Dto).SetValidator(new DesignApprovalFormValidator());
    }

    // ════════════════════ المعالج ════════════════════

    public class StudyDrawingHandler :
        IRequestHandler<GetStudyDrawingsQuery, List<StudyDrawingDto>>,
        IRequestHandler<GetStudyDrawingQuery, StudyDrawingDto>,
        IRequestHandler<CreateStudyDrawingCommand, StudyDrawingDto>,
        IRequestHandler<UpdateStudyDrawingCommand, StudyDrawingDto>,
        IRequestHandler<DeleteStudyDrawingCommand, Unit>,
        IRequestHandler<ReviewStudyDrawingCommand, StudyDrawingDto>,
        IRequestHandler<CreateArchitecturalVersionCommand, StudyDrawingDto>,
        IRequestHandler<UpdateArchitecturalVersionCommand, StudyDrawingDto>,
        IRequestHandler<DeleteArchitecturalVersionCommand, StudyDrawingDto>,
        IRequestHandler<GetDesignApprovalQuery, DesignApprovalDto>,
        IRequestHandler<CreateDesignApprovalCommand, DesignApprovalDto>,
        IRequestHandler<UpdateDesignApprovalCommand, DesignApprovalDto>,
        IRequestHandler<DeleteDesignApprovalCommand, Unit>
    {
        private readonly IStudyDrawingService _service;
        private readonly IStudyProjectService _projects;
        private readonly IStudyApprovalService _approvals;
        private readonly IStudyNumberService _numbers;
        private readonly IOfficeService _offices;
        private readonly IDepartmentService _departments;
        private readonly StudyKit _kit;

        public StudyDrawingHandler(IStudyDrawingService service, IStudyProjectService projects, IStudyApprovalService approvals,
            IStudyNumberService numbers, IOfficeService offices, IDepartmentService departments, StudyKit kit)
        {
            _service = service;
            _projects = projects;
            _approvals = approvals;
            _numbers = numbers;
            _offices = offices;
            _departments = departments;
            _kit = kit;
        }

        private static bool IsOpen(StudyProject p) => p.Status is StudyProjectStatus.Running or StudyProjectStatus.Suspended;

        private static void EnsureOpen(StudyProject p)
        {
            if (!IsOpen(p)) throw new InvalidOperationException("المشروع مكتمل أو ملغى — لا تُعدَّل مخططاته");
        }

        private static void EnsureOwnDepartment(Viewer viewer, ProjectDrawing d)
        {
            if (!StudyRules.InDepartment(viewer, d.DepartmentId))
                throw new UnauthorizedAccessException("يعدّل المخطط قسمه المُصدِر فقط");
        }

        private async Task<ProjectDrawing> LoadAsync(int id, Viewer viewer)
        {
            var drawing = await _service.GetDrawingAsync(id) ?? throw new KeyNotFoundException("المخطط غير موجود");
            StudyRules.EnsureInScope(viewer, drawing.Project.BranchId);
            return drawing;
        }

        private StudyDrawingDto ToDto(ProjectDrawing d, Viewer viewer)
        {
            var dto = _kit.Mapper.Map<StudyDrawingDto>(d);
            var inScope = StudyRules.InScope(viewer, d.Project.BranchId);
            dto.CanEdit = inScope && IsOpen(d.Project) && viewer.Has(StudyPermissions.EditDrawing)
                          && StudyRules.InDepartment(viewer, d.DepartmentId) && d.Status != DrawingStatus.Approved;
            dto.CanReview = inScope && IsOpen(d.Project) && viewer.Has("ReviewStudyDrawing")
                            && d.Kind != DrawingKind.Architectural && d.Status == DrawingStatus.PendingReview;
            return dto;
        }

        private async Task<StudyDrawingDto> DetailAsync(int id, Viewer viewer) => ToDto(await LoadAsync(id, viewer), viewer);

        private Task LogAsync(int projectId, StudyEntityType type, int id, StudyLogAction action, string title, string? details = null) =>
            LogCoreAsync(projectId, type, id, action, title, details);

        private async Task LogCoreAsync(int projectId, StudyEntityType type, int id, StudyLogAction action, string title, string? details) =>
            await _kit.LogAsync(await _projects.GetRequestIdAsync(projectId), type, id, action, title, details);

        /// <summary>الأساس لغير المعماري: نسخة معمارية معتمدة من المشروع نفسه</summary>
        private async Task EnsureBasisAsync(DrawingKind kind, int? versionId, int projectId)
        {
            if (versionId is not int id) return;
            if (kind == DrawingKind.Architectural)
                throw new InvalidOperationException("المخطط المعماري لا يُبنى على نسخة أخرى");
            var version = await _service.GetVersionAsync(id) ?? throw new KeyNotFoundException("النسخة المعمارية غير موجودة");
            if (version.Drawing.ProjectId != projectId)
                throw new InvalidOperationException("النسخة المعمارية من مشروع آخر");
            if (version.Status != DrawingStatus.Approved)
                throw new InvalidOperationException("يُبنى المخطط على نسخة معمارية معتمدة فقط");
        }

        private async Task EnsureOfficeAsync(int? officeId, int departmentId)
        {
            if (officeId is not int id) return;
            var office = await _offices.GetByIdAsync(id) ?? throw new KeyNotFoundException("المكتب غير موجود");
            if (office.DepartmentId != departmentId) throw new InvalidOperationException("المكتب ليس من القسم المُصدِر");
        }

        // ════════════════════ المخططات ════════════════════

        public async Task<List<StudyDrawingDto>> Handle(GetStudyDrawingsQuery request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var project = await _projects.GetByIdAsync(request.ProjectId) ?? throw new KeyNotFoundException("المشروع غير موجود");
            StudyRules.EnsureInScope(viewer, project.BranchId);
            var kind = request.Kind is int k && Enum.IsDefined(typeof(DrawingKind), k) ? (DrawingKind?)k : null;
            return (await _service.GetByProjectAsync(project.Id, kind)).Select(d => ToDto(d, viewer)).ToList();
        }

        public async Task<StudyDrawingDto> Handle(GetStudyDrawingQuery request, CancellationToken ct) =>
            await DetailAsync(request.Id, await _kit.ViewerAsync());

        public async Task<StudyDrawingDto> Handle(CreateStudyDrawingCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var project = await _projects.GetByIdAsync(request.Dto.ProjectId) ?? throw new KeyNotFoundException("المشروع غير موجود");
            StudyRules.EnsureInScope(viewer, project.BranchId);
            EnsureOpen(project);

            var departmentId = viewer.IsOrganizationWide && request.Dto.DepartmentId is int chosen ? chosen
                : viewer.User.DepartmentId ?? throw new InvalidOperationException("حسابك غير مرتبط بقسم — المخطط يصدر باسم قسم");
            if (!await _departments.ExistsAsync(departmentId)) throw new KeyNotFoundException("القسم المُصدِر غير موجود");
            if (!await _projects.DepartmentParticipatesAsync(project.Id, departmentId))
                throw new InvalidOperationException("القسم المُصدِر غير مشارك في المشروع");
            var kind = (DrawingKind)request.Dto.Kind;
            await EnsureBasisAsync(kind, request.Dto.BasedOnVersionId, project.Id);
            await EnsureOfficeAsync(request.Dto.OfficeId, departmentId);
            var number = request.Dto.DrawingNumber.Trim();
            if (await _service.DrawingNumberExistsAsync(project.Id, number))
                throw new InvalidOperationException("رقم المخطط مستخدم في المشروع");

            var drawing = ProjectDrawing.Create(kind);
            drawing.ProjectId = project.Id;
            drawing.DepartmentId = departmentId;
            drawing.OfficeId = request.Dto.OfficeId;
            drawing.PreparedByUserId = viewer.Id;
            drawing.BasedOnVersionId = request.Dto.BasedOnVersionId;
            drawing.Name = request.Dto.Name.Trim();
            drawing.DrawingNumber = number;
            drawing.DrawingDate = request.Dto.DrawingDate.Date;
            drawing.Description = StudyRequestHandler.Clean(request.Dto.Description);
            drawing.Status = DrawingStatus.InPreparation;
            drawing.CreatedAt = DateTime.Now;

            await _service.AddDrawingAsync(drawing);
            await LogAsync(project.Id, StudyEntityType.Drawing, drawing.Id, StudyLogAction.Created,
                $"أضاف المخطط {StudyRules.Name(kind)} {drawing.DrawingNumber} — {drawing.Name}");
            return await DetailAsync(drawing.Id, viewer);
        }

        public async Task<StudyDrawingDto> Handle(UpdateStudyDrawingCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var drawing = await LoadAsync(request.Id, viewer);
            EnsureOpen(drawing.Project);
            EnsureOwnDepartment(viewer, drawing);
            if (drawing.Status == DrawingStatus.Approved) throw new InvalidOperationException("المخطط معتمد — لا يُعدَّل");
            await EnsureBasisAsync(drawing.Kind, request.Dto.BasedOnVersionId, drawing.ProjectId);
            await EnsureOfficeAsync(request.Dto.OfficeId, drawing.DepartmentId);
            var number = request.Dto.DrawingNumber.Trim();
            if (await _service.DrawingNumberExistsAsync(drawing.ProjectId, number, drawing.Id))
                throw new InvalidOperationException("رقم المخطط مستخدم في المشروع");

            drawing.Name = request.Dto.Name.Trim();
            drawing.DrawingNumber = number;
            drawing.DrawingDate = request.Dto.DrawingDate.Date;
            drawing.Description = StudyRequestHandler.Clean(request.Dto.Description);
            drawing.OfficeId = request.Dto.OfficeId;
            drawing.BasedOnVersionId = request.Dto.BasedOnVersionId;
            // حالة المعماري تتبع نسخه واعتماداتها؛ غيره يرسله المعدّ للمراجعة
            if (drawing.Kind != DrawingKind.Architectural && request.Dto.Status is int status)
                drawing.Status = (DrawingStatus)status;
            drawing.UpdatedAt = DateTime.Now;
            await _service.UpdateDrawingAsync(drawing, request.Dto.RowVersion);
            await LogAsync(drawing.ProjectId, StudyEntityType.Drawing, drawing.Id, StudyLogAction.Edited,
                $"عدّل المخطط {drawing.DrawingNumber}", StudyRules.Name(drawing.Status));
            return await DetailAsync(drawing.Id, viewer);
        }

        public async Task<Unit> Handle(DeleteStudyDrawingCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var drawing = await LoadAsync(request.Id, viewer);
            EnsureOpen(drawing.Project);
            EnsureOwnDepartment(viewer, drawing);
            if (drawing.Status == DrawingStatus.Approved) throw new InvalidOperationException("المخطط معتمد — لا يُحذف");
            if (await _service.HasVersionsAsync(drawing.Id)) throw new InvalidOperationException("للمخطط نسخ — احذفها أولاً");

            var (projectId, number) = (drawing.ProjectId, drawing.DrawingNumber);
            await _service.DeleteDrawingAsync(drawing);   // مرفقاته تُحذف معه
            await LogAsync(projectId, StudyEntityType.Drawing, request.Id, StudyLogAction.Deleted, $"حذف المخطط {number}");
            return Unit.Value;
        }

        public async Task<StudyDrawingDto> Handle(ReviewStudyDrawingCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var drawing = await LoadAsync(request.Id, viewer);
            EnsureOpen(drawing.Project);
            if (drawing.Kind == DrawingKind.Architectural)
                throw new InvalidOperationException("المخطط المعماري يُعتمد باعتماد المخططات الأولي (CDA)");
            if (drawing.Status != DrawingStatus.PendingReview)
                throw new InvalidOperationException("المخطط ليس بانتظار المراجعة");

            drawing.Status = request.Dto.Approve ? DrawingStatus.Approved : DrawingStatus.Rejected;
            drawing.UpdatedAt = DateTime.Now;
            await _service.UpdateDrawingAsync(drawing);
            await LogAsync(drawing.ProjectId, StudyEntityType.Drawing, drawing.Id,
                request.Dto.Approve ? StudyLogAction.Approved : StudyLogAction.Rejected,
                $"{(request.Dto.Approve ? "اعتمد" : "رفض")} المخطط {drawing.DrawingNumber}", request.Dto.Note?.Trim());
            return await DetailAsync(drawing.Id, viewer);
        }

        // ════════════════════ نسخ المعماري ════════════════════

        private async Task<ArchitecturalVersion> LoadVersionForEditAsync(int id, Viewer viewer)
        {
            var version = await _service.GetVersionAsync(id) ?? throw new KeyNotFoundException("النسخة غير موجودة");
            StudyRules.EnsureInScope(viewer, version.Drawing.Project.BranchId);
            EnsureOpen(version.Drawing.Project);
            EnsureOwnDepartment(viewer, version.Drawing);
            if (version.DesignApproval != null)
                throw new InvalidOperationException("صدر للنسخة اعتماد مخططات — لا تُعدَّل ولا تُحذف (أصدر نسخة جديدة)");
            return version;
        }

        public async Task<StudyDrawingDto> Handle(CreateArchitecturalVersionCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var drawing = await LoadAsync(request.DrawingId, viewer);
            if (drawing is not ArchitecturalDrawing architectural)
                throw new InvalidOperationException("النسخ للمخطط المعماري فقط");
            EnsureOpen(drawing.Project);
            EnsureOwnDepartment(viewer, drawing);
            if (architectural.Versions.Any(v => v.Status is DrawingStatus.InPreparation or DrawingStatus.PendingReview))
                throw new InvalidOperationException("للمخطط نسخة قيد الإعداد أو الاعتماد — أكملها قبل إصدار نسخة جديدة");

            var version = new ArchitecturalVersion
            {
                DrawingId = drawing.Id,
                CreatedByUserId = viewer.Id,
                VersionNumber = await _service.NextVersionNumberAsync(drawing.Id),
                VersionDate = request.Dto.VersionDate.Date,
                Content = StudyRequestHandler.Clean(request.Dto.Content),
                Status = DrawingStatus.InPreparation,
                CreatedAt = DateTime.Now
            };
            if (drawing.Status != DrawingStatus.Approved) drawing.Status = DrawingStatus.InPreparation;
            await _service.AddVersionAsync(version);
            await LogAsync(drawing.ProjectId, StudyEntityType.Version, version.Id, StudyLogAction.Created,
                $"أصدر النسخة {StudyRules.VersionLabel(version.VersionNumber)} من المخطط {drawing.DrawingNumber}");
            return await DetailAsync(drawing.Id, viewer);
        }

        public async Task<StudyDrawingDto> Handle(UpdateArchitecturalVersionCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var version = await LoadVersionForEditAsync(request.Id, viewer);
            version.VersionDate = request.Dto.VersionDate.Date;
            version.Content = StudyRequestHandler.Clean(request.Dto.Content);
            await _service.UpdateVersionAsync(version, request.Dto.RowVersion);
            await LogAsync(version.Drawing.ProjectId, StudyEntityType.Version, version.Id, StudyLogAction.Edited,
                $"عدّل النسخة {StudyRules.VersionLabel(version.VersionNumber)} من المخطط {version.Drawing.DrawingNumber}");
            return await DetailAsync(version.DrawingId, viewer);
        }

        public async Task<StudyDrawingDto> Handle(DeleteArchitecturalVersionCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var version = await LoadVersionForEditAsync(request.Id, viewer);
            if (await _service.VersionUsedAsBasisAsync(version.Id))
                throw new InvalidOperationException("مخططات أخرى مبنية على هذه النسخة");
            var (drawingId, projectId, label) = (version.DrawingId, version.Drawing.ProjectId, StudyRules.VersionLabel(version.VersionNumber));
            await _service.DeleteVersionAsync(version);
            await LogAsync(projectId, StudyEntityType.Version, request.Id, StudyLogAction.Deleted, $"حذف النسخة {label}");
            return await DetailAsync(drawingId, viewer);
        }

        // ════════════════════ اعتماد المخططات الأولي (CDA) ════════════════════

        private async Task<(DesignApproval Cda, int RequestId)> LoadCdaAsync(int id, Viewer viewer)
        {
            var cda = await _service.GetDesignApprovalAsync(id) ?? throw new KeyNotFoundException("اعتماد المخططات غير موجود");
            StudyRules.EnsureInScope(viewer, cda.Version.Drawing.Project.BranchId);
            return (cda, await _projects.GetRequestIdAsync(cda.Version.Drawing.ProjectId));
        }

        private async Task<DesignApprovalDto> CdaDetailAsync(int id, Viewer viewer)
        {
            var (cda, requestId) = await LoadCdaAsync(id, viewer);
            var dto = _kit.Mapper.Map<DesignApprovalDto>(cda);
            dto.Approvals = await _kit.ApprovalsAsync(ApprovalOwner.Of(cda, requestId));
            dto.CanEdit = viewer.Has("EditDesignApproval") && IsOpen(cda.Version.Drawing.Project)
                          && dto.Approvals.All(a => a.Decision == (int)StudyDecision.Pending);
            return dto;
        }

        private async Task EnsureNoDecisionsAsync(DesignApproval cda)
        {
            var rows = await _approvals.GetForOwnerAsync(StudyEntityType.DesignApproval, cda.Id);
            if (rows.Any(r => r.Decision != StudyDecision.Pending))
                throw new InvalidOperationException("بدأ التوقيع على الاعتماد — لا يُعدَّل ولا يُحذف");
        }

        public async Task<DesignApprovalDto> Handle(GetDesignApprovalQuery request, CancellationToken ct) =>
            await CdaDetailAsync(request.Id, await _kit.ViewerAsync());

        public async Task<DesignApprovalDto> Handle(CreateDesignApprovalCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var version = await _service.GetVersionAsync(request.Dto.VersionId) ?? throw new KeyNotFoundException("النسخة غير موجودة");
            StudyRules.EnsureInScope(viewer, version.Drawing.Project.BranchId);
            EnsureOpen(version.Drawing.Project);
            if (version.DesignApproval != null) throw new InvalidOperationException("للنسخة اعتماد مخططات مسبقاً");
            if (version.Status != DrawingStatus.InPreparation)
                throw new InvalidOperationException("يُصدر الاعتماد لنسخة قيد الإعداد");

            var year = DateTime.Today.Year;
            var cda = new DesignApproval
            {
                VersionId = version.Id,
                CreatedByUserId = viewer.Id,
                CdaNumber = StudyRules.FormatNumber(StudyDocumentType.DesignApproval, year,
                    await _numbers.NextAsync(StudyDocumentType.DesignApproval, year)),
                CdaDate = request.Dto.CdaDate.Date,
                Content = StudyRequestHandler.Clean(request.Dto.Content),
                TechnicalNotes = StudyRequestHandler.Clean(request.Dto.TechnicalNotes),
                Decision = StudyDecision.Pending,
                CreatedAt = DateTime.Now
            };
            version.Status = DrawingStatus.PendingReview;
            version.Drawing.Status = version.Drawing.Status == DrawingStatus.Approved ? DrawingStatus.Approved : DrawingStatus.PendingReview;
            await _service.AddDesignApprovalAsync(cda);   // يحفظ حالة النسخة والمخطط معه

            await _kit.ResetApprovalsAsync(StudyEntityType.DesignApproval, cda.Id);
            await _service.SaveAsync();

            var requestId = await _projects.GetRequestIdAsync(version.Drawing.ProjectId);
            await _kit.LogAsync(requestId, StudyEntityType.DesignApproval, cda.Id, StudyLogAction.Submitted,
                $"أصدر اعتماد المخططات {cda.CdaNumber} للنسخة {StudyRules.VersionLabel(version.VersionNumber)} من {version.Drawing.DrawingNumber}");
            var (loaded, _) = await LoadCdaAsync(cda.Id, viewer);
            await _kit.NotifyNextSignersAsync(ApprovalOwner.Of(loaded, requestId));
            return await CdaDetailAsync(cda.Id, viewer);
        }

        public async Task<DesignApprovalDto> Handle(UpdateDesignApprovalCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var (cda, requestId) = await LoadCdaAsync(request.Id, viewer);
            EnsureOpen(cda.Version.Drawing.Project);
            await EnsureNoDecisionsAsync(cda);

            cda.CdaDate = request.Dto.CdaDate.Date;
            cda.Content = StudyRequestHandler.Clean(request.Dto.Content);
            cda.TechnicalNotes = StudyRequestHandler.Clean(request.Dto.TechnicalNotes);
            cda.UpdatedAt = DateTime.Now;
            await _service.UpdateDesignApprovalAsync(cda, request.Dto.RowVersion);
            await _kit.LogAsync(requestId, StudyEntityType.DesignApproval, cda.Id, StudyLogAction.Edited, $"عدّل اعتماد المخططات {cda.CdaNumber}");
            return await CdaDetailAsync(cda.Id, viewer);
        }

        public async Task<Unit> Handle(DeleteDesignApprovalCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var (cda, requestId) = await LoadCdaAsync(request.Id, viewer);
            EnsureOpen(cda.Version.Drawing.Project);
            await EnsureNoDecisionsAsync(cda);

            // النسخة تعود قيد الإعداد، والمخطط معها ما لم يكن له نسخة معتمدة
            cda.Version.Status = DrawingStatus.InPreparation;
            if (cda.Version.Drawing.Status != DrawingStatus.Approved) cda.Version.Drawing.Status = DrawingStatus.InPreparation;
            var number = cda.CdaNumber;
            await _service.DeleteDesignApprovalAsync(cda);   // سطور التوقيع تُحذف معه
            await _kit.LogAsync(requestId, StudyEntityType.DesignApproval, request.Id, StudyLogAction.Deleted, $"حذف اعتماد المخططات {number}");
            return Unit.Value;
        }
    }
}
