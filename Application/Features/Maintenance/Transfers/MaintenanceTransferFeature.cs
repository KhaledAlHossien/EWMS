using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities;
using Domain.Entities.Maintenance;
using FluentValidation;
using MediatR;

namespace Application.Features.Maintenance.Transfers
{
    // طلب تحويل طلب صيانة (قرار المستخدم 2026-10-05):
    // الفني المسند إليه الطلب (RequestMaintenanceTransfer) يطلب التحويل مع سبب واقتراح زميل اختياري؛
    // من يملك نقل طلبات القسم (AssignMaintenanceRequest، نفس حدّ Assign/{id}) يقبل فيُنقل الطلب، أو يرفض مع ملاحظة.
    public record RequestMaintenanceTransferCommand(int RequestId, RequestMaintenanceTransferDto Dto) : IRequest<MaintenanceTransferDto>;
    public record DecideMaintenanceTransferCommand(int TransferId, DecideMaintenanceTransferDto Dto) : IRequest<MaintenanceTransferDto>;
    /// <summary>طلب التحويل المعلّق لطلب صيانة (null إن لم يوجد) — لمن يرى الطلب</summary>
    public record GetPendingTransferQuery(int RequestId) : IRequest<MaintenanceTransferDto?>;
    /// <summary>طلبات التحويل المعلّقة التي أستطيع البت فيها (طلبات قسمي)</summary>
    public record GetPendingTransfersForMeQuery : IRequest<List<MaintenanceTransferDto>>;
    /// <summary>زملاء قسمي (لاقتراح من يُحوَّل إليه الطلب)</summary>
    public record GetTransferColleaguesQuery : IRequest<List<TechnicianOptionDto>>;

    public class RequestMaintenanceTransferCommandValidator : AbstractValidator<RequestMaintenanceTransferCommand>
    {
        public RequestMaintenanceTransferCommandValidator()
        {
            RuleFor(x => x.Dto.Reason)
                .Must(r => !string.IsNullOrWhiteSpace(r)).WithMessage("اكتب سبب طلب التحويل")
                .MaximumLength(500).WithMessage("السبب لا يتجاوز 500 حرف");
            RuleFor(x => x.Dto.SuggestedUserId).GreaterThan(0).When(x => x.Dto.SuggestedUserId != null)
                .WithMessage("الزميل المقترح غير صحيح");
        }
    }

    public class DecideMaintenanceTransferCommandValidator : AbstractValidator<DecideMaintenanceTransferCommand>
    {
        public DecideMaintenanceTransferCommandValidator()
        {
            RuleFor(x => x.Dto.Note).MaximumLength(500).WithMessage("الملاحظة لا تتجاوز 500 حرف");
        }
    }

    public class MaintenanceTransferHandler :
        IRequestHandler<RequestMaintenanceTransferCommand, MaintenanceTransferDto>,
        IRequestHandler<DecideMaintenanceTransferCommand, MaintenanceTransferDto>,
        IRequestHandler<GetPendingTransferQuery, MaintenanceTransferDto?>,
        IRequestHandler<GetPendingTransfersForMeQuery, List<MaintenanceTransferDto>>,
        IRequestHandler<GetTransferColleaguesQuery, List<TechnicianOptionDto>>
    {
        private readonly IMaintenanceRequestService _requests;
        private readonly IUserService _users;
        private readonly IUserPermissionService _permissions;
        private readonly INotificationService _notifications;

        public MaintenanceTransferHandler(
            IMaintenanceRequestService requests, IUserService users, IUserPermissionService permissions, INotificationService notifications)
        {
            _requests = requests;
            _users = users;
            _permissions = permissions;
            _notifications = notifications;
        }

        private static string StatusAr(MaintenanceTransferStatus s) => s switch
        {
            MaintenanceTransferStatus.Pending => "بانتظار قرار رئيس القسم",
            MaintenanceTransferStatus.Approved => "قُبل",
            MaintenanceTransferStatus.Rejected => "رُفض",
            MaintenanceTransferStatus.Closed => "أُغلق — نُقل الطلب مباشرة",
            _ => ""
        };

        public static MaintenanceTransferDto ToDto(MaintenanceTransferRequest t) => new()
        {
            Id = t.Id,
            MaintenanceRequestId = t.MaintenanceRequestId,
            RequestNumber = t.MaintenanceRequest == null ? "" : MaintenanceRules.RequestNumber(t.MaintenanceRequest.Id, t.MaintenanceRequest.CreatedAt),
            ClientName = t.MaintenanceRequest?.ClientName ?? "",
            RequestedById = t.RequestedById,
            RequestedByName = t.RequestedBy?.FullName ?? "",
            SuggestedUserId = t.SuggestedUserId,
            SuggestedUserName = t.SuggestedUser?.FullName,
            Reason = t.Reason,
            Status = (int)t.Status,
            StatusAr = StatusAr(t.Status),
            DecidedByName = t.DecidedBy?.FullName,
            NewUserName = t.NewUser?.FullName,
            DecisionNote = t.DecisionNote,
            CreatedAt = t.CreatedAt,
            DecidedAt = t.DecidedAt
        };

        private Task LogAsync(int requestId, User actor, MaintenanceActivityType type, string text) =>
            _requests.AddActivityAsync(new MaintenanceRequestActivity
            {
                MaintenanceRequestId = requestId, UserId = actor.Id, Type = type, Text = text, CreatedAt = DateTime.UtcNow
            });

        public async Task<MaintenanceTransferDto> Handle(RequestMaintenanceTransferCommand request, CancellationToken ct)
        {
            var user = await MaintenanceRules.CurrentUserAsync(_users);
            var entity = await _requests.GetByIdAsync(request.RequestId)
                ?? throw new KeyNotFoundException("طلب الصيانة غير موجود");

            // الصلاحية (Policy) تفتح العملية، والحد: الطلب مسند إليّ أنا
            MaintenanceRules.Ensure(entity.UserId == user.Id, "يطلب التحويل الفني المسند إليه الطلب فقط");
            MaintenanceRules.EnsureOpen(entity);

            if (await _requests.GetPendingTransferAsync(entity.Id) != null)
                throw new InvalidOperationException("يوجد طلب تحويل بانتظار قرار رئيس القسم لهذا الطلب");

            User? suggested = null;
            if (request.Dto.SuggestedUserId is int suggestedId)
            {
                MaintenanceRules.Ensure(suggestedId != user.Id, "اختر زميلاً غيرك");
                suggested = await _users.GetByIdAsync(suggestedId) ?? throw new KeyNotFoundException("الزميل المقترح غير موجود");
                if (!suggested.IsActive) throw new InvalidOperationException("الزميل المقترح حسابه معطّل");
                if (suggested.DepartmentId == null || suggested.DepartmentId != entity.DepartmentId)
                    throw new InvalidOperationException("اقترح زميلاً من قسم الطلب");
            }

            var reason = request.Dto.Reason.Trim();
            var transfer = new MaintenanceTransferRequest
            {
                MaintenanceRequestId = entity.Id,
                RequestedById = user.Id,
                SuggestedUserId = suggested?.Id,
                Reason = reason,
                Status = MaintenanceTransferStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };
            await _requests.AddTransferAsync(transfer);

            await LogAsync(entity.Id, user, MaintenanceActivityType.TransferRequested,
                $"طلب تحويل الطلب{(suggested != null ? $" إلى {suggested.FullName}" : "")} — السبب: {reason}");
            await MaintenanceNotifier.TransferRequestedAsync(_notifications, _permissions, entity, user, reason);

            return ToDto((await _requests.GetTransferAsync(transfer.Id))!);
        }

        public async Task<MaintenanceTransferDto> Handle(DecideMaintenanceTransferCommand request, CancellationToken ct)
        {
            var user = await MaintenanceRules.CurrentUserAsync(_users);
            var transfer = await _requests.GetTransferAsync(request.TransferId)
                ?? throw new KeyNotFoundException("طلب التحويل غير موجود");
            if (transfer.Status != MaintenanceTransferStatus.Pending)
                throw new InvalidOperationException("بُتّ في طلب التحويل هذا مسبقاً");

            var entity = await _requests.GetByIdAsync(transfer.MaintenanceRequestId)
                ?? throw new KeyNotFoundException("طلب الصيانة غير موجود");

            // نفس حدّ Assign/{id}: طلبات قسمي فقط (SuperAdmin الكل)
            var assign = await MaintenanceRules.BoundaryAsync(_users, _permissions, "AssignMaintenanceRequest");
            MaintenanceRules.Ensure(MaintenanceRules.In(assign, entity), "لا يمكنك البت في تحويل طلب خارج قسمك");

            var note = string.IsNullOrWhiteSpace(request.Dto.Note) ? null : request.Dto.Note.Trim();
            transfer.DecidedById = user.Id;
            transfer.DecidedAt = DateTime.UtcNow;
            transfer.DecisionNote = note;

            if (!request.Dto.Approve)
            {
                transfer.Status = MaintenanceTransferStatus.Rejected;
                await _requests.UpdateTransferAsync(transfer);
                await LogAsync(entity.Id, user, MaintenanceActivityType.TransferRejected,
                    "رفض طلب التحويل" + (note == null ? "" : $" — {note}"));
                await MaintenanceNotifier.TransferRejectedAsync(_notifications, entity, user, transfer.RequestedById, note);
                return ToDto((await _requests.GetTransferAsync(transfer.Id))!);
            }

            MaintenanceRules.EnsureOpen(entity);
            var targetId = request.Dto.UserId ?? transfer.SuggestedUserId
                ?? throw new InvalidOperationException("اختر الموظف الذي يُحوَّل إليه الطلب");
            var target = await MaintenanceRules.ResolveAssigneeAsync(_users, assign, targetId);
            MaintenanceRules.Ensure(target.Id != entity.UserId, "اختر موظفاً غير الفني الحالي");

            var previousName = entity.User?.FullName ?? "";
            entity.UserId = target.Id;
            entity.DepartmentId = target.DepartmentId;
            entity.UpdatedAt = DateTime.UtcNow;
            await _requests.UpdateAsync(entity);

            transfer.Status = MaintenanceTransferStatus.Approved;
            transfer.NewUserId = target.Id;
            await _requests.UpdateTransferAsync(transfer);

            await LogAsync(entity.Id, user, MaintenanceActivityType.Reassigned,
                $"قبل طلب التحويل ونقل الطلب من {previousName} إلى {target.FullName} — السبب: {transfer.Reason}" + (note == null ? "" : $" — {note}"));
            await MaintenanceNotifier.TransferApprovedAsync(_notifications, entity, user, transfer.RequestedById, target);

            return ToDto((await _requests.GetTransferAsync(transfer.Id))!);
        }

        public async Task<MaintenanceTransferDto?> Handle(GetPendingTransferQuery request, CancellationToken ct)
        {
            var entity = await _requests.GetByIdAsync(request.RequestId)
                ?? throw new KeyNotFoundException("طلب الصيانة غير موجود");
            var scopes = await MaintenanceRules.RequestScopesAsync(_users, _permissions);
            MaintenanceRules.Ensure(MaintenanceRules.In(scopes.View, entity), "لا يمكنك عرض طلب صيانة خارج نطاقك");

            var pending = await _requests.GetPendingTransferAsync(entity.Id);
            return pending == null ? null : ToDto(pending);
        }

        public async Task<List<MaintenanceTransferDto>> Handle(GetPendingTransfersForMeQuery request, CancellationToken ct)
        {
            var assign = await MaintenanceRules.BoundaryAsync(_users, _permissions, "AssignMaintenanceRequest");
            if (assign == null) return [];
            var list = await _requests.GetPendingTransfersAsync(assign.All ? null : assign.DepartmentId);
            return list.Select(ToDto).ToList();
        }

        public async Task<List<TechnicianOptionDto>> Handle(GetTransferColleaguesQuery request, CancellationToken ct)
        {
            var user = await MaintenanceRules.CurrentUserAsync(_users);
            if (user.DepartmentId is not int departmentId) return [];
            return (await _users.GetByDepartmentAsync(departmentId))
                .Where(u => u.IsActive && u.Id != user.Id)
                .OrderBy(u => u.FullName)
                .Select(u => new TechnicianOptionDto { Id = u.Id, FullName = u.FullName })
                .ToList();
        }
    }
}
