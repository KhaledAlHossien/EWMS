using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using MediatR;

namespace Application.Features.Projects
{
    // المشاريع: الصلاحية (في المتحكم) تفتح العملية، وحدّ السجلات في ProjectRules.Visible. كل كتابة تحفظ أثرها في سجل التدقيق بمعاملتها.
    public record SearchProjectsQuery(string? Search, int? BranchId, int? DepartmentId, int? SiteId, int? Stage, int Page, int PageSize)
        : IRequest<PagedResultDto<ProjectResponseDto>>;
    public record GetProjectQuery(int Id) : IRequest<ProjectResponseDto>;
    public record GetProjectAuditQuery(int Id, string? EntityType, int Page, int PageSize) : IRequest<PagedResultDto<AuditLogResponseDto>>;
    public record GetProjectMemberCandidatesQuery(int Id, string? Search) : IRequest<List<ProjectMemberCandidateDto>>;
    public record CreateProjectCommand(ProjectRequestDto Dto) : IRequest<ProjectResponseDto>;
    public record UpdateProjectCommand(int Id, ProjectRequestDto Dto) : IRequest<ProjectResponseDto>;
    public record AddProjectMemberCommand(int Id, AddProjectMemberRequestDto Dto) : IRequest<ProjectResponseDto>;
    public record RemoveProjectMemberCommand(int Id, int UserId) : IRequest<ProjectResponseDto>;

    // ════════════════════ التحقق من المدخلات ════════════════════

    public class ProjectRequestDtoValidator : AbstractValidator<ProjectRequestDto>
    {
        public ProjectRequestDtoValidator()
        {
            RuleFor(x => x.Name).NotEmpty().WithMessage("اسم المشروع مطلوب").MaximumLength(200).WithMessage("اسم المشروع لا يتجاوز 200 حرف");
            RuleFor(x => x.ReferenceNumber).MaximumLength(50).WithMessage("الرقم المرجعي لا يتجاوز 50 حرفاً");
            RuleFor(x => x.Beneficiary).MaximumLength(200).WithMessage("الجهة المستفيدة لا تتجاوز 200 حرف");
            RuleFor(x => x.Description).MaximumLength(4000).WithMessage("الوصف لا يتجاوز 4000 حرف");
            RuleFor(x => x.Stage).Must(s => s == null || ProjectRules.IsValidStage(s.Value)).WithMessage("مرحلة المشروع غير صحيحة");
        }
    }

    public class CreateProjectCommandValidator : AbstractValidator<CreateProjectCommand>
    {
        public CreateProjectCommandValidator() => RuleFor(x => x.Dto).SetValidator(new ProjectRequestDtoValidator());
    }

    public class UpdateProjectCommandValidator : AbstractValidator<UpdateProjectCommand>
    {
        public UpdateProjectCommandValidator() => RuleFor(x => x.Dto).SetValidator(new ProjectRequestDtoValidator());
    }

    public class AddProjectMemberCommandValidator : AbstractValidator<AddProjectMemberCommand>
    {
        public AddProjectMemberCommandValidator()
        {
            RuleFor(x => x.Dto.UserId).GreaterThan(0).WithMessage("اختر الموظف");
            RuleFor(x => x.Dto.Title).MaximumLength(100).WithMessage("الصفة لا تتجاوز 100 حرف");
        }
    }

    // ════════════════════ المعالج ════════════════════

    public class ProjectHandler :
        IRequestHandler<SearchProjectsQuery, PagedResultDto<ProjectResponseDto>>,
        IRequestHandler<GetProjectQuery, ProjectResponseDto>,
        IRequestHandler<GetProjectAuditQuery, PagedResultDto<AuditLogResponseDto>>,
        IRequestHandler<GetProjectMemberCandidatesQuery, List<ProjectMemberCandidateDto>>,
        IRequestHandler<CreateProjectCommand, ProjectResponseDto>,
        IRequestHandler<UpdateProjectCommand, ProjectResponseDto>,
        IRequestHandler<AddProjectMemberCommand, ProjectResponseDto>,
        IRequestHandler<RemoveProjectMemberCommand, ProjectResponseDto>
    {
        private readonly IProjectService _projects;
        private readonly IAuditLogService _audit;
        private readonly IUserService _users;
        private readonly IUserPermissionService _permissions;
        private readonly IDepartmentService _departments;
        private readonly ISiteService _sites;
        private readonly INotificationService _notifications;

        public ProjectHandler(IProjectService projects, IAuditLogService audit, IUserService users, IUserPermissionService permissions,
            IDepartmentService departments, ISiteService sites, INotificationService notifications)
        {
            _projects = projects;
            _audit = audit;
            _users = users;
            _permissions = permissions;
            _departments = departments;
            _sites = sites;
            _notifications = notifications;
        }

        private Task<Viewer> ViewerAsync() => Viewer.CurrentAsync(_users, _permissions);

        /// <summary>المشروع المحمَّل بتفاصيله بعد التحقق من أنه ضمن حدّ المستخدم</summary>
        private async Task<Project> LoadVisibleAsync(Viewer viewer, int id)
        {
            var project = await _projects.GetByIdAsync(id) ?? throw new KeyNotFoundException("المشروع غير موجود");
            ProjectRules.EnsureView(viewer, project);
            return project;
        }

        // ───────── القراءة ─────────

        public async Task<PagedResultDto<ProjectResponseDto>> Handle(SearchProjectsQuery request, CancellationToken ct)
        {
            var viewer = await ViewerAsync();
            var page = Math.Max(1, request.Page);
            var pageSize = Math.Clamp(request.PageSize, 1, 100);
            ProjectStage? stage = request.Stage is int s && ProjectRules.IsValidStage(s) ? (ProjectStage)s : null;

            var (items, total) = await _projects.SearchAsync(ProjectRules.Visible(viewer),
                new ProjectFilter(request.Search, request.BranchId, request.DepartmentId, request.SiteId, stage), page, pageSize);

            return new PagedResultDto<ProjectResponseDto>
            {
                Items = items.Select(p => ToDto(p, viewer, withMembers: false)).ToList(),
                TotalCount = total, Page = page, PageSize = pageSize
            };
        }

        public async Task<ProjectResponseDto> Handle(GetProjectQuery request, CancellationToken ct)
        {
            var viewer = await ViewerAsync();
            return ToDto(await LoadVisibleAsync(viewer, request.Id), viewer, withMembers: true);
        }

        public async Task<PagedResultDto<AuditLogResponseDto>> Handle(GetProjectAuditQuery request, CancellationToken ct)
        {
            var viewer = await ViewerAsync();
            await LoadVisibleAsync(viewer, request.Id);   // سجل مشروع لا يراه المستخدم لا يُقرأ
            var page = Math.Max(1, request.Page);
            var pageSize = Math.Clamp(request.PageSize, 1, 100);

            var (items, total) = await _audit.GetForProjectAsync(request.Id, request.EntityType, null, page, pageSize);
            return new PagedResultDto<AuditLogResponseDto>
            {
                Items = items.Select(a => new AuditLogResponseDto
                {
                    Id = a.Id, CreatedAt = a.CreatedAt, UserId = a.UserId, UserName = a.User?.FullName ?? string.Empty,
                    Action = a.Action, EntityType = a.EntityType, EntityId = a.EntityId, ProjectId = a.ProjectId,
                    Title = a.Title, Details = a.Details
                }).ToList(),
                TotalCount = total, Page = page, PageSize = pageSize
            };
        }

        public async Task<List<ProjectMemberCandidateDto>> Handle(GetProjectMemberCandidatesQuery request, CancellationToken ct)
        {
            var viewer = await ViewerAsync();
            var project = await LoadVisibleAsync(viewer, request.Id);
            var candidates = await _projects.GetMemberCandidatesAsync(project.BranchId, request.Search,
                project.Members.Select(m => m.UserId).ToList(), take: 30);
            return candidates.Select(u => new ProjectMemberCandidateDto { Id = u.Id, FullName = u.FullName, DepartmentName = u.Department?.Name }).ToList();
        }

        // ───────── الكتابة ─────────

        public async Task<ProjectResponseDto> Handle(CreateProjectCommand request, CancellationToken ct)
        {
            var viewer = await ViewerAsync();
            var dto = request.Dto;

            // الفرع: فرع المنشئ، ولنظرة المؤسسة أي فرع يختاره
            var branchId = viewer.IsOrganizationWide ? dto.BranchId ?? viewer.User.BranchId : viewer.User.BranchId;
            if (branchId is not int branch)
                throw new InvalidOperationException(viewer.IsOrganizationWide ? "اختر فرع المشروع" : "حسابك غير مرتبط بفرع، فلا تستطيع إنشاء مشروع");
            ProjectRules.EnsureCanCreateIn(viewer, branch);

            var departmentId = dto.DepartmentId ?? (OrganizationRole.InBranch(viewer.User, branch) ? viewer.User.DepartmentId : null);
            var departmentName = await ResolveDepartmentAsync(departmentId, branch);
            var siteName = await ResolveSiteAsync(dto.SiteId);
            var reference = ProjectRules.CleanReference(dto.ReferenceNumber);
            if (reference != null && await _projects.ReferenceExistsAsync(branch, reference))
                throw new InvalidOperationException("يوجد مشروع بالرقم المرجعي نفسه في هذا الفرع");

            var now = DateTime.UtcNow;
            var project = new Project
            {
                Name = dto.Name.Trim(),
                ReferenceNumber = reference,
                Beneficiary = (dto.Beneficiary ?? string.Empty).Trim(),
                Description = (dto.Description ?? string.Empty).Trim(),
                BranchId = branch,
                DepartmentId = departmentId,
                SiteId = dto.SiteId,
                Stage = ProjectStage.Study,
                CreatedByUserId = viewer.Id,
                CreatedAt = now,
                UpdatedAt = now,
                // المنشئ مشارك فيه من البداية فلا يفقد رؤيته إن غيّر قسمه لاحقاً
                Members = [new ProjectMember { UserId = viewer.Id, Title = "منشئ المشروع", AddedAt = now }]
            };

            var details = new List<string> { $"المرحلة: {ProjectRules.StageAr(project.Stage)}" };
            if (departmentName != null) details.Add($"القسم: {departmentName}");
            if (siteName != null) details.Add($"الموقع: {siteName}");
            if (reference != null) details.Add($"الرقم المرجعي: {reference}");

            await _projects.AddAsync(project, new AuditEntry(viewer.Id, AuditActions.Created, AuditEntities.Project, null,
                $"إنشاء المشروع «{project.Name}»", string.Join("\n", details)));

            return ToDto(await LoadVisibleAsync(viewer, project.Id), viewer, withMembers: true);
        }

        public async Task<ProjectResponseDto> Handle(UpdateProjectCommand request, CancellationToken ct)
        {
            var viewer = await ViewerAsync();
            var project = await LoadVisibleAsync(viewer, request.Id);
            var dto = request.Dto;

            var oldStage = project.Stage;
            var newStage = dto.Stage is int s ? (ProjectStage)s : oldStage;
            // مغلق أو ملغى يبقى كما هو: إعادة الفتح بتغيير المرحلة إلى غير نهائية أولاً، ثم يُعدَّل
            if (ProjectRules.IsFinished(oldStage) && ProjectRules.IsFinished(newStage))
                ProjectRules.EnsureOpen(project);

            var departmentName = await ResolveDepartmentAsync(dto.DepartmentId, project.BranchId);
            var siteName = await ResolveSiteAsync(dto.SiteId);
            var reference = ProjectRules.CleanReference(dto.ReferenceNumber);
            if (reference != null && await _projects.ReferenceExistsAsync(project.BranchId, reference, project.Id))
                throw new InvalidOperationException("يوجد مشروع بالرقم المرجعي نفسه في هذا الفرع");

            var changes = new List<string>();
            void Track(string label, string? from, string? to)
            {
                if (!string.Equals(from ?? string.Empty, to ?? string.Empty, StringComparison.Ordinal))
                    changes.Add($"{label}: {(string.IsNullOrEmpty(from) ? "—" : from)} ← {(string.IsNullOrEmpty(to) ? "—" : to)}");
            }

            var name = dto.Name.Trim();
            var beneficiary = (dto.Beneficiary ?? string.Empty).Trim();
            var description = (dto.Description ?? string.Empty).Trim();
            Track("الاسم", project.Name, name);
            Track("الرقم المرجعي", project.ReferenceNumber, reference);
            Track("الجهة المستفيدة", project.Beneficiary, beneficiary);
            if (project.Description != description) changes.Add("الوصف: تغيّر");
            Track("قسم المعالجة", project.Department?.Name, departmentName);
            Track("الموقع", project.Site?.Name, siteName);
            Track("المرحلة", ProjectRules.StageAr(oldStage), ProjectRules.StageAr(newStage));

            if (changes.Count == 0) return ToDto(project, viewer, withMembers: true);

            project.Name = name;
            project.ReferenceNumber = reference;
            project.Beneficiary = beneficiary;
            project.Description = description;
            project.DepartmentId = dto.DepartmentId;
            project.Department = null;
            project.SiteId = dto.SiteId;
            project.Site = null;
            project.Stage = newStage;

            var action = oldStage != newStage ? AuditActions.Stage : AuditActions.Updated;
            await _projects.UpdateAsync(project, dto.RowVersion, new AuditEntry(viewer.Id, action, AuditEntities.Project, project.Id,
                $"{(action == AuditActions.Stage ? "تغيير مرحلة" : "تعديل")} المشروع «{project.Name}»", string.Join("\n", changes)));

            return ToDto(await LoadVisibleAsync(viewer, project.Id), viewer, withMembers: true);
        }

        public async Task<ProjectResponseDto> Handle(AddProjectMemberCommand request, CancellationToken ct)
        {
            var viewer = await ViewerAsync();
            var project = await LoadVisibleAsync(viewer, request.Id);
            ProjectRules.EnsureOpen(project);

            var user = await _users.GetByIdAsync(request.Dto.UserId) ?? throw new KeyNotFoundException("الموظف غير موجود");
            ProjectRules.EnsureCanBeMember(project, user);

            var member = new ProjectMember
            {
                ProjectId = project.Id, UserId = user.Id, Title = (request.Dto.Title ?? string.Empty).Trim(), AddedAt = DateTime.UtcNow
            };
            await _projects.AddMemberAsync(member, new AuditEntry(viewer.Id, AuditActions.MemberAdded, AuditEntities.Project, project.Id,
                $"إضافة مشارك إلى «{project.Name}»", $"{user.FullName}{(member.Title.Length > 0 ? $" — {member.Title}" : string.Empty)}"));

            if (user.Id != viewer.Id)
                await _notifications.AddAsync(new Notification
                {
                    UserId = user.Id,
                    Title = "أُضفت إلى مشروع",
                    Message = $"أضافك {viewer.User.FullName} مشاركاً في المشروع {ProjectRules.Number(project.Id, project.CreatedAt)} — {project.Name}",
                    Type = NotificationType.ProjectMemberAdded,
                    RelatedEntityType = AuditEntities.Project,
                    RelatedEntityId = project.Id,
                    CreatedAt = DateTime.UtcNow
                });

            return ToDto(await LoadVisibleAsync(viewer, project.Id), viewer, withMembers: true);
        }

        public async Task<ProjectResponseDto> Handle(RemoveProjectMemberCommand request, CancellationToken ct)
        {
            var viewer = await ViewerAsync();
            var project = await LoadVisibleAsync(viewer, request.Id);
            ProjectRules.EnsureOpen(project);

            var member = project.Members.FirstOrDefault(m => m.UserId == request.UserId)
                ?? throw new KeyNotFoundException("هذا الموظف ليس مشاركاً في المشروع");

            await _projects.RemoveMemberAsync(member, new AuditEntry(viewer.Id, AuditActions.MemberRemoved, AuditEntities.Project, project.Id,
                $"إزالة مشارك من «{project.Name}»", member.User?.FullName ?? string.Empty));

            // قد يفقد المستخدم رؤية المشروع بإزالة نفسه: يُعاد المشروع بما يراه، ولا خطأ إن خرج من حدّه
            var reloaded = await _projects.GetByIdAsync(project.Id) ?? project;
            if (!ProjectRules.CanView(viewer, reloaded))
                throw new InvalidOperationException("أُزيلت مشاركتك، ولم يعد المشروع ضمن صلاحياتك");
            return ToDto(reloaded, viewer, withMembers: true);
        }

        // ───────── مساعدات ─────────

        /// <summary>القسم المختار يتبع فرع المشروع، ويعيد اسمه للأثر (null إن لم يُختر)</summary>
        private async Task<string?> ResolveDepartmentAsync(int? departmentId, int branchId)
        {
            if (departmentId is not int id) return null;
            var department = await _departments.GetByIdAsync(id) ?? throw new KeyNotFoundException("القسم غير موجود");
            if (department.BranchId != branchId)
                throw new InvalidOperationException("القسم المختار يتبع فرعاً آخر غير فرع المشروع");
            return department.Name;
        }

        private async Task<string?> ResolveSiteAsync(int? siteId)
        {
            if (siteId is not int id) return null;
            var site = await _sites.GetByIdAsync(id) ?? throw new KeyNotFoundException("الموقع غير موجود");
            return site.Name;
        }

        private static ProjectResponseDto ToDto(Project p, Viewer viewer, bool withMembers) => new()
        {
            Id = p.Id,
            Number = ProjectRules.Number(p.Id, p.CreatedAt),
            ReferenceNumber = p.ReferenceNumber,
            Name = p.Name,
            Beneficiary = p.Beneficiary,
            Description = p.Description,
            BranchId = p.BranchId,
            BranchName = p.Branch?.Name ?? string.Empty,
            DepartmentId = p.DepartmentId,
            DepartmentName = p.Department?.Name,
            SiteId = p.SiteId,
            SiteName = p.Site?.Name,
            Stage = (int)p.Stage,
            StageName = ProjectRules.StageAr(p.Stage),
            IsFinished = ProjectRules.IsFinished(p.Stage),
            CreatedByUserId = p.CreatedByUserId,
            CreatedByName = p.CreatedByUser?.FullName ?? string.Empty,
            CreatedAt = p.CreatedAt,
            UpdatedAt = p.UpdatedAt,
            RowVersion = p.RowVersion,
            MembersCount = p.Members.Count,
            CanEdit = viewer.Has(AppPermissions.EditProject),
            Members = withMembers
                ? p.Members.OrderBy(m => m.AddedAt).Select(m => new ProjectMemberResponseDto
                {
                    UserId = m.UserId, FullName = m.User?.FullName ?? string.Empty, DepartmentName = m.User?.Department?.Name,
                    Title = m.Title, AddedAt = m.AddedAt
                }).ToList()
                : null
        };
    }
}
