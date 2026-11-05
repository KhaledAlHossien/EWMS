using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities;
using Domain.Entities.CentralStudiesBranch;
using Domain.Enums;

namespace Application.Features.CentralStudies
{
    /// <summary>
    /// مستند يُعتمد بالتواقيع كما يراه سير الاعتماد: الفرع، القسم المعني بصفته (المتابع/المختص/المرسل)، وهل ينتظر التواقيع.
    /// </summary>
    public sealed record ApprovalOwner(
        StudyEntityType Type, int Id, int RequestId, int BranchId, int? RoleDepartmentId,
        bool Pending, string Number, int CreatedByUserId)
    {
        public static ApprovalOwner Of(StudyAssignment a) =>
            new(StudyEntityType.Assignment, a.Id, a.RequestId, a.Request.BranchId, null,
                a.Status == StudyDocumentStatus.PendingApproval, a.AssignmentNumber, a.CreatedByUserId);

        public static ApprovalOwner Of(StudyAssignmentResponse r) =>
            new(StudyEntityType.Response, r.Id, r.Assignment.RequestId, r.Assignment.Request.BranchId, r.Assignment.FollowingDepartmentId,
                r.Status == StudyDocumentStatus.PendingApproval, $"رد على {r.Assignment.AssignmentNumber}", r.CreatedByUserId);

        public static ApprovalOwner Of(StudyStartOrder o) =>
            new(StudyEntityType.StartOrder, o.Id, o.Response.Assignment.RequestId, o.Response.Assignment.Request.BranchId, o.CompetentDepartmentId,
                o.Status == StudyDocumentStatus.PendingApproval, o.OrderNumber, o.CreatedByUserId);

        public static ApprovalOwner Of(DesignApproval d, int requestId) =>
            new(StudyEntityType.DesignApproval, d.Id, requestId, d.Version.Drawing.Project.BranchId, d.Version.Drawing.DepartmentId,
                d.Decision == StudyDecision.Pending, d.CdaNumber, d.CreatedByUserId);

        /// <summary>المشروع «ينتظر» ما دام جارياً أو معلّقاً (طلب الإنجاز يُنشئ سطور التوقيع)</summary>
        public static ApprovalOwner Of(StudyProject p, int requestId) =>
            new(StudyEntityType.Project, p.Id, requestId, p.BranchId, null,
                p.Status is StudyProjectStatus.Running or StudyProjectStatus.Suspended, p.Code, p.CreatedByUserId);

        /// <summary>من سطر محمَّل مع مستنده (IStudyApprovalService.GetWithOwnerAsync / GetPendingAsync)</summary>
        public static ApprovalOwner Of(StudyApproval row) =>
            row.Assignment != null ? Of(row.Assignment)
            : row.Response != null ? Of(row.Response)
            : row.StartOrder != null ? Of(row.StartOrder)
            : row.DesignApproval != null ? Of(row.DesignApproval, row.DesignApproval.Version.Drawing.Project.StartOrder.Response.Assignment.RequestId)
            : row.Project != null ? Of(row.Project, row.Project.StartOrder.Response.Assignment.RequestId)
            : throw new InvalidOperationException("سطر توقيع بلا مستند");
    }

    /// <summary>صاحب مرفق: حدوده وصلاحيتا العرض والتعديل عليه</summary>
    public sealed record AttachmentOwner(
        StudyEntityType Type, int Id, int RequestId, int BranchId,
        string ViewPermission, string EditPermission, bool Closed, int? EditDepartmentId);

    /// <summary>
    /// أدوات مشتركة لمعالجات فرع الدراسات (خدمة لكل طلب HTTP): المستخدم الحالي وصلاحياته، سجل الدراسة،
    /// سطور التواقيع مع «يستطيع التوقيع الآن»، إشعارات الموقّعين والمنشئ، وحلّ صاحب المرفق.
    /// </summary>
    public sealed class StudyKit
    {
        private readonly IUserService _users;
        private readonly IUserPermissionService _permissions;
        private readonly IStudyActivityLogService _log;
        private readonly IStudyApprovalService _approvals;
        private readonly INotificationService _notifications;
        private readonly IStudyRequestService _requests;
        private readonly IStudyAssignmentService _assignments;
        private readonly IStudyResponseService _responses;
        private readonly IStudyStartOrderService _startOrders;
        private readonly IStudyProjectService _projects;
        private readonly IStudyDrawingService _drawings;
        private Viewer? _viewer;

        public IMapper Mapper { get; }

        public StudyKit(
            IUserService users, IUserPermissionService permissions, IStudyActivityLogService log, IStudyApprovalService approvals,
            INotificationService notifications, IStudyRequestService requests, IStudyAssignmentService assignments,
            IStudyResponseService responses, IStudyStartOrderService startOrders, IStudyProjectService projects,
            IStudyDrawingService drawings, IMapper mapper)
        {
            _users = users;
            _permissions = permissions;
            _log = log;
            _approvals = approvals;
            _notifications = notifications;
            _requests = requests;
            _assignments = assignments;
            _responses = responses;
            _startOrders = startOrders;
            _projects = projects;
            _drawings = drawings;
            Mapper = mapper;
        }

        public async Task<Viewer> ViewerAsync() => _viewer ??= await Viewer.CurrentAsync(_users, _permissions);

        public Task<StudyChainDto> ChainAsync(int requestId) => _requests.GetChainAsync(requestId);

        // ════════════════════ سجل الدراسة ════════════════════

        public async Task LogAsync(int requestId, StudyEntityType type, int entityId, StudyLogAction action, string title, string? details = null)
        {
            var viewer = await ViewerAsync();
            await _log.AddAsync(new StudyActivityLog
            {
                RequestId = requestId,
                UserId = viewer.Id,
                EntityType = type,
                EntityId = entityId,
                Action = action,
                Title = title.Length > 200 ? title[..200] : title,
                Details = string.IsNullOrWhiteSpace(details) ? null : details,
                CreatedAt = DateTime.Now
            });
        }

        // ════════════════════ التواقيع ════════════════════

        /// <summary>هل يستطيع المستخدم التوقيع على هذا السطر الآن (صلاحية الصفة وحدّها، الترتيب، لا توقيعين لشخص واحد)</summary>
        public static bool CanDecide(Viewer viewer, StudyApproval row, IReadOnlyCollection<StudyApproval> siblings, ApprovalOwner owner)
        {
            if (!owner.Pending || row.Decision != StudyDecision.Pending) return false;
            if (!viewer.Has(StudyRules.SignPermission(row.SignerRole))) return false;
            if (!StudyRules.InScope(viewer, owner.BranchId)) return false;
            if (StudyRules.IsDepartmentRole(row.SignerRole) && !StudyRules.InDepartment(viewer, owner.RoleDepartmentId)) return false;
            if (siblings.Any(s => s.SortOrder < row.SortOrder && !StudyRules.IsPositive(s.Decision))) return false;
            if (!viewer.IsSuperAdmin && siblings.Any(s => s.Id != row.Id && s.SignerUserId == viewer.Id)) return false;
            return true;
        }

        public async Task<List<StudyApprovalDto>> ApprovalsAsync(ApprovalOwner owner)
        {
            var viewer = await ViewerAsync();
            var rows = await _approvals.GetForOwnerAsync(owner.Type, owner.Id);
            return rows.Select(row =>
            {
                var dto = Mapper.Map<StudyApprovalDto>(row);
                dto.CanDecide = CanDecide(viewer, row, rows, owner);
                return dto;
            }).ToList();
        }

        /// <summary>يستبدل سطور التوقيع بسطور جديدة معلّقة (يحفظها حفظ المستند التالي — السياق نفسه)</summary>
        public Task ResetApprovalsAsync(StudyEntityType type, int ownerId) =>
            _approvals.ReplaceForOwnerAsync(type, ownerId, StudyRules.BuildApprovalRows(type, ownerId));

        // ════════════════════ الإشعارات ════════════════════

        private static string RelatedType(StudyEntityType type) => "Study" + type;

        /// <summary>لحاملي صلاحية أول صفة لم توقّع بعد، ضمن حدّها (الفرع أو القسم المعني)</summary>
        public async Task NotifyNextSignersAsync(ApprovalOwner owner)
        {
            var rows = await _approvals.GetForOwnerAsync(owner.Type, owner.Id);
            var next = rows.Where(r => r.Decision == StudyDecision.Pending).OrderBy(r => r.SortOrder).FirstOrDefault();
            if (next == null) return;

            var permission = StudyRules.SignPermission(next.SignerRole);
            var recipients = StudyRules.IsDepartmentRole(next.SignerRole)
                ? (owner.RoleDepartmentId is int dept ? await _permissions.GetUsersWithPermissionAsync(permission, departmentId: dept) : [])
                : await _permissions.GetUsersWithPermissionAsync(permission, branchId: owner.BranchId);
            var signed = rows.Where(r => r.SignerUserId != null).Select(r => r.SignerUserId!.Value).ToHashSet();
            var now = DateTime.Now;
            var notes = recipients.Where(u => !signed.Contains(u.Id)).Select(u => new Notification
            {
                UserId = u.Id,
                Title = "مستند دراسات بانتظار توقيعك",
                Message = $"{StudyRules.Name(owner.Type)} {owner.Number} — بصفة {StudyRules.Name(next.SignerRole)}",
                Type = NotificationType.StudyApprovalRequested,
                RelatedEntityType = RelatedType(owner.Type),
                RelatedEntityId = owner.Id,
                CreatedAt = now
            }).ToList();
            if (notes.Count > 0) await _notifications.AddRangeAsync(notes);
        }

        /// <summary>للمنشئ: اعتُمد مستنده نهائياً أو رُفض</summary>
        public async Task NotifyDecisionAsync(ApprovalOwner owner, bool approved, string? note)
        {
            var viewer = await ViewerAsync();
            if (owner.CreatedByUserId == viewer.Id) return;
            await _notifications.AddAsync(new Notification
            {
                UserId = owner.CreatedByUserId,
                Title = approved ? "اعتُمد مستند دراسات" : "رُفض مستند دراسات",
                Message = $"{StudyRules.Name(owner.Type)} {owner.Number}" + (approved ? " — اعتُمد نهائياً" : $" — رفضه {viewer.User.FullName}: {note}"),
                Type = NotificationType.StudyDocumentDecided,
                RelatedEntityType = RelatedType(owner.Type),
                RelatedEntityId = owner.Id,
                CreatedAt = DateTime.Now
            });
        }

        /// <summary>مهمة مرحلة جديدة: للموظف المسند إليه، أو لمن يحدّث إنجاز المهام في المكتب/القسم</summary>
        public async Task NotifyTaskAssignedAsync(StageTask task, string projectCode)
        {
            var viewer = await ViewerAsync();
            List<int> recipients = task.TargetType switch
            {
                StudyTaskTarget.User => task.AssigneeUserId is int u ? [u] : [],
                StudyTaskTarget.Office => (await _permissions.GetUsersWithPermissionAsync(StudyPermissions.UpdateTaskProgress, officeId: task.OfficeId))
                    .Select(x => x.Id).ToList(),
                _ => (await _permissions.GetUsersWithPermissionAsync(StudyPermissions.UpdateTaskProgress, departmentId: task.DepartmentId, exactUnit: true))
                    .Select(x => x.Id).ToList()
            };
            var now = DateTime.Now;
            var notes = recipients.Where(id => id != viewer.Id).Distinct().Select(id => new Notification
            {
                UserId = id,
                Title = "مهمة جديدة في مشروع دراسات",
                Message = $"{projectCode} — {task.Title}",
                Type = NotificationType.StudyTaskAssigned,
                RelatedEntityType = "StudyTask",
                RelatedEntityId = task.Id,
                CreatedAt = now
            }).ToList();
            if (notes.Count > 0) await _notifications.AddRangeAsync(notes);
        }

        // ════════════════════ صاحب المرفق ════════════════════

        /// <summary>
        /// حدود المرفق من صاحبه: العرض بصلاحية عرض المستند والتعديل بصلاحية تعديله (المخطط ونسخه لقسم المخطط)،
        /// ولا رفع ولا حذف على مستند ملغى.
        /// </summary>
        public async Task<AttachmentOwner> ResolveOwnerAsync(StudyEntityType type, int id)
        {
            const string missing = "صاحب المرفق غير موجود";
            switch (type)
            {
                case StudyEntityType.Request:
                {
                    var r = await _requests.GetByIdAsync(id) ?? throw new KeyNotFoundException(missing);
                    return new(type, id, r.Id, r.BranchId, StudyPermissions.ViewRequests, StudyPermissions.EditRequest,
                        r.Status == StudyDocumentStatus.Cancelled, null);
                }
                case StudyEntityType.Assignment:
                {
                    var a = await _assignments.GetByIdAsync(id) ?? throw new KeyNotFoundException(missing);
                    return new(type, id, a.RequestId, a.Request.BranchId, StudyPermissions.ViewAssignments, StudyPermissions.EditAssignment,
                        a.Status == StudyDocumentStatus.Cancelled, null);
                }
                case StudyEntityType.Response:
                {
                    var r = await _responses.GetByIdAsync(id) ?? throw new KeyNotFoundException(missing);
                    return new(type, id, r.Assignment.RequestId, r.Assignment.Request.BranchId, StudyPermissions.ViewResponses, StudyPermissions.EditResponse,
                        r.Status == StudyDocumentStatus.Cancelled, null);
                }
                case StudyEntityType.StartOrder:
                {
                    var o = await _startOrders.GetByIdAsync(id) ?? throw new KeyNotFoundException(missing);
                    return new(type, id, o.Response.Assignment.RequestId, o.Response.Assignment.Request.BranchId,
                        StudyPermissions.ViewStartOrders, StudyPermissions.EditStartOrder, o.Status == StudyDocumentStatus.Cancelled, null);
                }
                case StudyEntityType.Project:
                {
                    var p = await _projects.GetByIdAsync(id) ?? throw new KeyNotFoundException(missing);
                    return new(type, id, p.StartOrder.Response.Assignment.RequestId, p.BranchId, StudyPermissions.ViewProjects, StudyPermissions.EditProject,
                        p.Status == StudyProjectStatus.Cancelled, null);
                }
                case StudyEntityType.Drawing:
                {
                    var d = await _drawings.GetDrawingAsync(id) ?? throw new KeyNotFoundException(missing);
                    return new(type, id, await _projects.GetRequestIdAsync(d.ProjectId), d.Project.BranchId,
                        StudyPermissions.ViewDrawings, StudyPermissions.EditDrawing, d.Project.Status == StudyProjectStatus.Cancelled, d.DepartmentId);
                }
                case StudyEntityType.Version:
                {
                    var v = await _drawings.GetVersionAsync(id) ?? throw new KeyNotFoundException(missing);
                    return new(type, id, await _projects.GetRequestIdAsync(v.Drawing.ProjectId), v.Drawing.Project.BranchId,
                        StudyPermissions.ViewDrawings, StudyPermissions.EditVersion, v.Drawing.Project.Status == StudyProjectStatus.Cancelled, v.Drawing.DepartmentId);
                }
                default:
                    throw new ArgumentException("نوع صاحب المرفق غير مدعوم");
            }
        }
    }
}
