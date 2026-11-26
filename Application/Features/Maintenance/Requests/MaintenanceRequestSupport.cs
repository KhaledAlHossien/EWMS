using Application.Common;
using Application.DTOs.Request;
using Application.Interfaces;
using Domain.Entities;
using Domain.Entities.Maintenance;

namespace Application.Features.Maintenance.Requests
{
    /// <summary>الدوال المشتركة بين عمليات هذه الميزة</summary>
    public class MaintenanceRequestSupport
    {
        private readonly IMaintenanceRequestService _requestService;
        private readonly IDeviceMaintenanceService _deviceService;
        private readonly IDamageTypeService _damageTypeService;
        private readonly IMaintenanceRequestStatusService _statusService;
        private readonly IUserService _userService;
        private readonly INotificationService _notifications;
        private readonly IUserPermissionService _permissions;
        private readonly IUserSignatureService _signatures;

        public MaintenanceRequestSupport(IMaintenanceRequestService requestService, IDeviceMaintenanceService deviceService, IDamageTypeService damageTypeService, IMaintenanceRequestStatusService statusService, IUserService userService, INotificationService notifications, IUserPermissionService permissions, IUserSignatureService signatures)
        {
            _requestService = requestService;
            _deviceService = deviceService;
            _damageTypeService = damageTypeService;
            _statusService = statusService;
            _userService = userService;
            _notifications = notifications;
            _permissions = permissions;
            _signatures = signatures;
        }

        public Task LogAsync(MaintenanceRequest request, User actor, MaintenanceActivityType type, string text) =>
            _requestService.AddActivityAsync(new MaintenanceRequestActivity
            {
                MaintenanceRequestId = request.Id,
                UserId = actor.Id,
                Type = type,
                Text = text,
                CreatedAt = DateTime.UtcNow
            });

        public async Task EnsureReferencesAsync(SaveMaintenanceRequestDto dto)
        {
            if (!await _deviceService.ExistsAsync(dto.DeviceMaintenanceId))
                throw new KeyNotFoundException("الجهاز المحدد غير موجود — أضفه من أجهزة الصيانة أولاً");
            if (!await _damageTypeService.ExistsAsync(dto.DamageTypeId))
                throw new KeyNotFoundException("نوع العطل المحدد غير موجود");
            if (!await _statusService.ExistsAsync(dto.MaintenanceRequestStatusId))
                throw new KeyNotFoundException("حالة الطلب المحددة غير موجودة");
        }

        public Task NotifyClientStatusAsync(MaintenanceRequest r, User actor)
        {
            var number = MaintenanceRules.RequestNumber(r.Id, r.CreatedAt);
            var (title, message) = r.MaintenanceRequestStatus?.Stage switch
            {
                MaintenanceStage.Ready => ("جهازك جاهز للاستلام", $"انتهت صيانة جهازك — الطلب {number}. يمكنك استلامه من قسم الصيانة"),
                MaintenanceStage.NotRepairable => ("تعذّر إصلاح جهازك", $"الجهاز غير قابل للصيانة — الطلب {number}. راجع قسم الصيانة لاستلامه"),
                MaintenanceStage.Delivered => ("سُلّم جهازك", $"سُلّم جهازك — الطلب {number}"),
                _ => ("تحديث على جهازك في الصيانة", $"صارت حالة طلب الصيانة {number} لجهازك: {r.MaintenanceRequestStatus?.Name}")
            };
            return MaintenanceNotifier.ClientUpdateAsync(_notifications, r, actor, title, message);
        }

        /// <summary>دخل الطلب مرحلة نهائية: أي طلب تحويل معلّق لم يعد له معنى</summary>
        public async Task ClosePendingTransferIfFinalAsync(MaintenanceRequest r, User actor)
        {
            if (!MaintenanceRules.IsClosed(r)) return;
            if (await _requestService.GetPendingTransferAsync(r.Id) is { } pending
                && await _requestService.GetTransferAsync(pending.Id) is { } transfer)
            {
                transfer.Status = MaintenanceTransferStatus.Closed;
                transfer.DecidedById = actor.Id;
                transfer.DecidedAt = DateTime.UtcNow;
                transfer.DecisionNote = "أُغلق الطلب قبل البت في طلب التحويل";
                await _requestService.UpdateTransferAsync(transfer);
            }
        }

        /// <summary>
        /// العميل موظف (ClientUserId): الاسم من حسابه، والهاتف المكتوب أو هاتفه المسجّل؛ وإلا عميل خارجي بالاسم والهاتف المكتوبين
        /// </summary>
        public async Task ApplyClientAsync(MaintenanceRequest entity, SaveMaintenanceRequestDto dto)
        {
            if (dto.ClientUserId is int clientId)
            {
                var client = await MaintenanceClients.ResolveAsync(_userService, clientId);
                entity.ClientUserId = client.Id;
                entity.ClientName = client.FullName;
                if (string.IsNullOrWhiteSpace(dto.ClientPhone)) entity.ClientPhone = client.PhoneNumber ?? string.Empty;
            }
            else entity.ClientUserId = null;
        }

        /// <summary>
        /// عند دخول الطلب مرحلة «مُسلَّم»: يُثبَّت موقّع ورقة التسليم — صاحب SignMaintenanceReceipt في قسم الطلب
        /// (الأقدم إن تعدّدوا) — ونسخة توقيعه الحالية، فلا تتغير الورقة إن تغيّر التوقيع أو الموقّع لاحقاً.
        /// كل دخول جديد لحالة تسليم (بعد إعادة الجهاز للصيانة مثلاً) يُثبِّت من جديد.
        /// </summary>
        public async Task StampDeliveryAsync(MaintenanceRequest entity, int? previousStatusId)
        {
            if (entity.MaintenanceRequestStatusId == previousStatusId) return;
            var status = await _statusService.GetByIdAsync(entity.MaintenanceRequestStatusId);
            if (status is not { Stage: MaintenanceStage.Delivered }) return;

            var signer = entity.DepartmentId is int departmentId
                ? (await _permissions.GetUsersWithPermissionAsync(AppPermissions.SignMaintenanceReceipt, departmentId: departmentId))
                    .OrderBy(u => u.Id).FirstOrDefault()
                : null;

            entity.DeliveredAt = DateTime.UtcNow;
            entity.DeliverySignerId = signer?.Id;
            entity.DeliverySignatureId = signer == null ? null : await _signatures.GetCurrentIdAsync(signer.Id);
        }

        // حقول البحث تُحفظ بلا فراغات زائدة حتى يعمل "يبدأ بـ" بشكل صحيح
        public static void Trim(MaintenanceRequest r)
        {
            r.ClientName = r.ClientName.Trim();
            r.ClientPhone = MaintenanceRules.NormalizePhone(r.ClientPhone); // يُحفظ أرقاماً متصلة
            r.Accessories = r.Accessories.Trim();
            r.Description = r.Description.Trim();
        }
    }
}
