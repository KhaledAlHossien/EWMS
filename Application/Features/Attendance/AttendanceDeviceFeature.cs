using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.Attendance.Push;
using Application.Interfaces;
using Domain.Entities.Attendance;
using FluentValidation;
using MediatR;
using static Application.Common.AppPermissions;

namespace Application.Features.Attendance
{
    public record GetAttendanceDevicesQuery(int? State, int? AreaId) : IRequest<List<AttendanceDeviceDto>>;
    public record GetAttendanceDeviceQuery(int Id) : IRequest<AttendanceDeviceDto>;
    public record ApproveAttendanceDeviceCommand(int Id, AttendanceDeviceRequestDto Dto) : IRequest<AttendanceDeviceDto>;
    public record RejectAttendanceDeviceCommand(int Id) : IRequest<AttendanceDeviceDto>;
    public record UpdateAttendanceDeviceCommand(int Id, AttendanceDeviceRequestDto Dto) : IRequest<AttendanceDeviceDto>;
    public record RemoveAttendanceDeviceCommand(int Id) : IRequest<Unit>;
    public record SendAttendanceDeviceCommandCommand(int Id, AttendanceDeviceCommandRequestDto Dto) : IRequest<AttendanceDeviceDto>;
    public record GetAttendanceDeviceCommandsQuery(int Id, int Page, int PageSize) : IRequest<PagedResultDto<AttendanceDeviceCommandDto>>;
    public record GetAttendanceDeviceLogQuery(int Id) : IRequest<List<AttendanceDeviceLogDto>>;
    public record GetAttendanceDeviceUsersQuery(int Id) : IRequest<List<AttendanceDeviceUserDto>>;

    public class AttendanceDeviceRequestValidator : AbstractValidator<AttendanceDeviceRequestDto>
    {
        public AttendanceDeviceRequestValidator()
        {
            RuleFor(x => x.Name).NotEmpty().WithMessage("اسم الجهاز مطلوب").MaximumLength(100).WithMessage("اسم الجهاز طويل جداً");
            RuleFor(x => x.AreaId).NotNull().GreaterThan(0).WithMessage("منطقة الجهاز مطلوبة");
            RuleFor(x => x.Notes).MaximumLength(500).WithMessage("الملاحظات طويلة جداً");
        }
    }

    public class ApproveAttendanceDeviceCommandValidator : AbstractValidator<ApproveAttendanceDeviceCommand>
    {
        public ApproveAttendanceDeviceCommandValidator() => RuleFor(x => x.Dto).SetValidator(new AttendanceDeviceRequestValidator());
    }

    public class UpdateAttendanceDeviceCommandValidator : AbstractValidator<UpdateAttendanceDeviceCommand>
    {
        public UpdateAttendanceDeviceCommandValidator() => RuleFor(x => x.Dto).SetValidator(new AttendanceDeviceRequestValidator());
    }

    /// <summary>
    /// أجهزة البصمة: العرض لمنطقتي أو للكل (AttendanceRules.DeviceViewLimit)، والأوامر حسب CommandLimit،
    /// والقبول والتعديل والإزالة لكل الأجهزة (المركز). لا يوجد أمر يمسح بيانات الجهاز.
    /// </summary>
    public class AttendanceDeviceHandler :
        IRequestHandler<GetAttendanceDevicesQuery, List<AttendanceDeviceDto>>,
        IRequestHandler<GetAttendanceDeviceQuery, AttendanceDeviceDto>,
        IRequestHandler<ApproveAttendanceDeviceCommand, AttendanceDeviceDto>,
        IRequestHandler<RejectAttendanceDeviceCommand, AttendanceDeviceDto>,
        IRequestHandler<UpdateAttendanceDeviceCommand, AttendanceDeviceDto>,
        IRequestHandler<RemoveAttendanceDeviceCommand, Unit>,
        IRequestHandler<SendAttendanceDeviceCommandCommand, AttendanceDeviceDto>,
        IRequestHandler<GetAttendanceDeviceCommandsQuery, PagedResultDto<AttendanceDeviceCommandDto>>,
        IRequestHandler<GetAttendanceDeviceLogQuery, List<AttendanceDeviceLogDto>>,
        IRequestHandler<GetAttendanceDeviceUsersQuery, List<AttendanceDeviceUserDto>>
    {
        /// <summary>أطول فترة لسحب السجلات بأمر واحد</summary>
        public const int MaxQueryDays = 366;

        private readonly IAttendanceDeviceService _devices;
        private readonly IAttendanceAreaService _areas;
        private readonly IAttendanceSettings _settings;
        private readonly IUserService _users;
        private readonly IUserPermissionService _permissions;

        public AttendanceDeviceHandler(IAttendanceDeviceService devices, IAttendanceAreaService areas, IAttendanceSettings settings,
            IUserService users, IUserPermissionService permissions)
        {
            _devices = devices;
            _areas = areas;
            _settings = settings;
            _users = users;
            _permissions = permissions;
        }

        private Task<Viewer> ViewerAsync() => Viewer.CurrentAsync(_users, _permissions);
        private DateTime OnlineSince => DateTime.UtcNow.AddMinutes(-_settings.OfflineMinutes);
        /// <summary>الوقت المحلي للأجهزة (منطقتها الزمنية في الإعدادات)</summary>
        private DateTime DeviceNow => DateTime.UtcNow.AddHours(_settings.TimeZoneHours);

        private AttendanceDeviceDto ToDto(DeviceRow row, Viewer viewer)
        {
            var d = row.Device;
            var approved = d.State == AttendanceDeviceState.Approved;
            return new AttendanceDeviceDto
            {
                Id = d.Id, SerialNumber = d.SerialNumber, Name = d.Name, State = (int)d.State,
                StateName = AttendanceRules.DeviceStateName(d.State), AreaId = d.AreaId, AreaName = row.AreaName ?? string.Empty,
                Model = d.Model, Firmware = d.Firmware, PushVersion = d.PushVersion, FingerAlgorithm = d.FingerAlgorithm, FaceAlgorithm = d.FaceAlgorithm,
                IpAddress = d.IpAddress, UserCount = d.UserCount, FingerCount = d.FingerCount, FaceCount = d.FaceCount, PunchCount = d.PunchCount,
                ManagedUsers = row.ManagedUsers, PendingCommands = row.PendingCommands, NeedsSync = d.NeedsSync,
                FirstSeenAt = d.FirstSeenAt, LastSeenAt = d.LastSeenAt, Online = d.LastSeenAt >= OnlineSince,
                Notes = d.Notes, ApprovedByName = row.ApprovedByName, ApprovedAt = d.ApprovedAt,
                CanCommand = approved && AttendanceRules.CommandLimit(viewer).Includes(d.AreaId),
                CanApprove = d.State is AttendanceDeviceState.Pending or AttendanceDeviceState.Rejected
                    && (viewer.IsSuperAdmin || viewer.Has(ApproveAttendanceDevice)),
                CanEdit = approved && (viewer.IsSuperAdmin || viewer.Has(EditAttendanceDevice)),
                CanRemove = d.State != AttendanceDeviceState.Removed && (viewer.IsSuperAdmin || viewer.Has(DeleteAttendanceDevice))
            };
        }

        /// <summary>الجهاز ضمن حدّ عرض المستخدم (غير ذلك = غير موجود بالنسبة له)</summary>
        private async Task<(DeviceRow Row, Viewer Viewer)> VisibleAsync(int id)
        {
            var viewer = await ViewerAsync();
            var row = await _devices.GetRowAsync(id) ?? throw new KeyNotFoundException("الجهاز غير موجود");
            var limit = AttendanceRules.DeviceViewLimit(viewer);
            if (!limit.All && !(row.Device.State == AttendanceDeviceState.Approved && limit.Includes(row.Device.AreaId)))
                throw new KeyNotFoundException("الجهاز غير موجود");
            return (row, viewer);
        }

        private async Task<AttendanceDeviceDto> DtoAsync(int id, Viewer viewer) =>
            ToDto(await _devices.GetRowAsync(id) ?? throw new KeyNotFoundException("الجهاز غير موجود"), viewer);

        public async Task<List<AttendanceDeviceDto>> Handle(GetAttendanceDevicesQuery request, CancellationToken ct)
        {
            var viewer = await ViewerAsync();
            var limit = AttendanceRules.DeviceViewLimit(viewer);
            if (limit.IsNothing) return [];
            var state = request.State is int s && Enum.IsDefined(typeof(AttendanceDeviceState), s) ? (AttendanceDeviceState?)s : null;
            return (await _devices.GetAllAsync(limit, state, request.AreaId)).Select(r => ToDto(r, viewer)).ToList();
        }

        public async Task<AttendanceDeviceDto> Handle(GetAttendanceDeviceQuery request, CancellationToken ct)
        {
            var (row, viewer) = await VisibleAsync(request.Id);
            return ToDto(row, viewer);
        }

        private async Task<Domain.Entities.Attendance.AttendanceArea> AreaAsync(int? areaId) =>
            areaId is int id ? await _areas.GetByIdAsync(id) ?? throw new KeyNotFoundException("المنطقة غير موجودة")
                : throw new InvalidOperationException("منطقة الجهاز مطلوبة");

        private AttendanceDeviceLog Log(int deviceId, AttendanceDeviceEvent e, string details, int userId) =>
            new() { DeviceId = deviceId, Event = e, Details = details, UserId = userId, CreatedAt = DateTime.UtcNow };

        private static AttendanceDeviceCommand Command(int deviceId, AttendanceCommandKind kind, string text, string summary, int? userId) =>
            new() { DeviceId = deviceId, Kind = kind, Text = text, Summary = summary, CreatedByUserId = userId, CreatedAt = DateTime.UtcNow };

        public async Task<AttendanceDeviceDto> Handle(ApproveAttendanceDeviceCommand request, CancellationToken ct)
        {
            var viewer = await ViewerAsync();
            var device = await _devices.GetByIdAsync(request.Id) ?? throw new KeyNotFoundException("الجهاز غير موجود");
            if (device.State is not (AttendanceDeviceState.Pending or AttendanceDeviceState.Rejected))
                throw new InvalidOperationException("الجهاز ليس بانتظار الموافقة");
            var area = await AreaAsync(request.Dto.AreaId);

            device.State = AttendanceDeviceState.Approved;
            device.Name = request.Dto.Name.Trim();
            device.AreaId = area.Id;
            device.Notes = request.Dto.Notes?.Trim() ?? string.Empty;
            device.ApprovedByUserId = viewer.Id;
            device.ApprovedAt = DateTime.UtcNow;
            device.NeedsSync = true;   // الخدمة الخلفية تضع عليه أشخاص منطقته
            await _devices.SaveAsync(device, Log(device.Id, AttendanceDeviceEvent.Approved, $"قُبل في منطقة «{area.Name}» باسم «{device.Name}»", viewer.Id));

            // ما سُجّل عليه قبل قبوله (رُفض وقتها فبقي في ذاكرته)، وما عليه من مستخدمين وقوالب، ومعلوماته
            var since = device.FirstSeenAt.AddHours(_settings.TimeZoneHours).Date.AddDays(-1);
            await _devices.EnqueueAsync(
            [
                Command(device.Id, AttendanceCommandKind.RefreshInfo, PushProtocol.Info, "تحديث معلومات الجهاز", viewer.Id),
                Command(device.Id, AttendanceCommandKind.QueryPunches, PushProtocol.QueryPunches(since, DeviceNow),
                    $"سحب السجلات منذ {since:yyyy/MM/dd}", viewer.Id),
                Command(device.Id, AttendanceCommandKind.QueryUsers, PushProtocol.QueryUsers, "سحب قائمة المستخدمين", viewer.Id),
                Command(device.Id, AttendanceCommandKind.QueryBiometrics, PushProtocol.QueryTemplates(9), "سحب قوالب الوجه", viewer.Id),
                Command(device.Id, AttendanceCommandKind.QueryBiometrics, PushProtocol.QueryTemplates(1), "سحب قوالب الإصبع", viewer.Id)
            ]);
            return await DtoAsync(device.Id, viewer);
        }

        public async Task<AttendanceDeviceDto> Handle(RejectAttendanceDeviceCommand request, CancellationToken ct)
        {
            var viewer = await ViewerAsync();
            var device = await _devices.GetByIdAsync(request.Id) ?? throw new KeyNotFoundException("الجهاز غير موجود");
            if (device.State != AttendanceDeviceState.Pending)
                throw new InvalidOperationException("يُرفض الجهاز المنتظر فقط — الجهاز المقبول يُزال");
            device.State = AttendanceDeviceState.Rejected;
            await _devices.SaveAsync(device, Log(device.Id, AttendanceDeviceEvent.Rejected, "رُفض الجهاز: لا تُقبل بياناته", viewer.Id));
            return await DtoAsync(device.Id, viewer);
        }

        public async Task<AttendanceDeviceDto> Handle(UpdateAttendanceDeviceCommand request, CancellationToken ct)
        {
            var viewer = await ViewerAsync();
            var device = await _devices.GetByIdAsync(request.Id) ?? throw new KeyNotFoundException("الجهاز غير موجود");
            if (device.State != AttendanceDeviceState.Approved)
                throw new InvalidOperationException("يُعدَّل الجهاز المقبول فقط — الجهاز المنتظر يُقبل أولاً");
            var area = await AreaAsync(request.Dto.AreaId);

            var changes = new List<string>();
            var name = request.Dto.Name.Trim();
            var notes = request.Dto.Notes?.Trim() ?? string.Empty;
            if (device.Name != name) changes.Add($"الاسم: «{device.Name}» ← «{name}»");
            if (device.AreaId != area.Id)
            {
                changes.Add($"المنطقة: «{device.Area?.Name}» ← «{area.Name}»");
                device.NeedsSync = true;   // يُزال عنه أشخاص المنطقة القديمة ويوضع عليه أشخاص الجديدة
            }
            if (device.Notes != notes) changes.Add("الملاحظات");
            if (changes.Count == 0) return await DtoAsync(device.Id, viewer);

            device.Name = name;
            device.AreaId = area.Id;
            device.Notes = notes;
            await _devices.SaveAsync(device, Log(device.Id, AttendanceDeviceEvent.Edited, string.Join("، ", changes), viewer.Id));
            return await DtoAsync(device.Id, viewer);
        }

        public async Task<Unit> Handle(RemoveAttendanceDeviceCommand request, CancellationToken ct)
        {
            var viewer = await ViewerAsync();
            var device = await _devices.GetByIdAsync(request.Id) ?? throw new KeyNotFoundException("الجهاز غير موجود");
            if (device.State == AttendanceDeviceState.Removed) return Unit.Value;
            device.State = AttendanceDeviceState.Removed;
            device.NeedsSync = false;
            await _devices.SaveAsync(device, Log(device.Id, AttendanceDeviceEvent.Removed, "أُزيل الجهاز (بصماته باقية، وإن اتصل عاد بانتظار الموافقة)", viewer.Id));
            await _devices.CancelQueuedAsync(device.Id);
            return Unit.Value;
        }

        public async Task<AttendanceDeviceDto> Handle(SendAttendanceDeviceCommandCommand request, CancellationToken ct)
        {
            var viewer = await ViewerAsync();
            var device = await _devices.GetByIdAsync(request.Id) ?? throw new KeyNotFoundException("الجهاز غير موجود");
            if (!AttendanceRules.CommandLimit(viewer).Includes(device.AreaId))
                throw new UnauthorizedAccessException("لا تملك إرسال الأوامر لهذا الجهاز");
            if (device.State != AttendanceDeviceState.Approved)
                throw new InvalidOperationException("تُرسل الأوامر للجهاز المقبول فقط");

            var commands = new List<AttendanceDeviceCommand>();
            string summary;
            switch (request.Dto.Kind)
            {
                case "SyncNow":
                    await _devices.MarkNeedsSyncAsync(device.Id);
                    summary = "مزامنة الأشخاص الآن";
                    break;
                case "FullResync":
                    await _devices.RequestFullResyncAsync(device.Id);
                    summary = "إعادة مزامنة كاملة (كل الأشخاص وقوالبهم)";
                    break;
                case "QueryPunches":
                {
                    var to = (request.Dto.To ?? DeviceNow).Date.AddDays(1).AddSeconds(-1);
                    var from = (request.Dto.From ?? to.Date.AddDays(-6)).Date;
                    if (from > to) throw new InvalidOperationException("بداية الفترة بعد نهايتها");
                    if ((to - from).TotalDays > MaxQueryDays) throw new InvalidOperationException($"أطول فترة لسحب السجلات {MaxQueryDays} يوماً");
                    summary = $"سحب السجلات من {from:yyyy/MM/dd} إلى {to:yyyy/MM/dd}";
                    commands.Add(Command(device.Id, AttendanceCommandKind.QueryPunches, PushProtocol.QueryPunches(from, to), summary, viewer.Id));
                    break;
                }
                case "QueryTemplates":
                    summary = "سحب القوالب";
                    commands.Add(Command(device.Id, AttendanceCommandKind.QueryBiometrics, PushProtocol.QueryTemplates(9), "سحب قوالب الوجه", viewer.Id));
                    commands.Add(Command(device.Id, AttendanceCommandKind.QueryBiometrics, PushProtocol.QueryTemplates(1), "سحب قوالب الإصبع", viewer.Id));
                    break;
                case "QueryUsers":
                    summary = "سحب قائمة المستخدمين";
                    commands.Add(Command(device.Id, AttendanceCommandKind.QueryUsers, PushProtocol.QueryUsers, summary, viewer.Id));
                    break;
                case "RefreshInfo":
                    summary = "تحديث معلومات الجهاز";
                    commands.Add(Command(device.Id, AttendanceCommandKind.RefreshInfo, PushProtocol.Info, summary, viewer.Id));
                    break;
                case "SetTime":
                    summary = $"ضبط الوقت على وقت الخادم";
                    commands.Add(Command(device.Id, AttendanceCommandKind.SetTime, PushProtocol.SetTime(DeviceNow), summary, viewer.Id));
                    break;
                case "Reboot":
                    summary = "إعادة التشغيل";
                    commands.Add(Command(device.Id, AttendanceCommandKind.Reboot, PushProtocol.Reboot, summary, viewer.Id));
                    break;
                default:
                    throw new InvalidOperationException("أمر غير معروف");
            }

            await _devices.EnqueueAsync(commands);
            await _devices.SaveAsync(device, Log(device.Id, AttendanceDeviceEvent.Command, summary, viewer.Id));
            return await DtoAsync(device.Id, viewer);
        }

        public async Task<PagedResultDto<AttendanceDeviceCommandDto>> Handle(GetAttendanceDeviceCommandsQuery request, CancellationToken ct)
        {
            await VisibleAsync(request.Id);
            var page = Math.Max(1, request.Page);
            var size = Math.Clamp(request.PageSize, 1, 100);
            var (items, total) = await _devices.CommandsAsync(request.Id, page, size);
            return new PagedResultDto<AttendanceDeviceCommandDto>
            {
                Page = page, PageSize = size, TotalCount = total,
                Items = items.Select(c => new AttendanceDeviceCommandDto
                {
                    Id = c.Id, Kind = (int)c.Kind, Summary = c.Summary, State = (int)c.State, StateName = CommandStateName(c.State),
                    ReturnCode = c.ReturnCode, CreatedByName = c.CreatedByUser?.FullName,
                    CreatedAt = c.CreatedAt, SentAt = c.SentAt, CompletedAt = c.CompletedAt
                }).ToList()
            };
        }

        public async Task<List<AttendanceDeviceLogDto>> Handle(GetAttendanceDeviceLogQuery request, CancellationToken ct)
        {
            await VisibleAsync(request.Id);
            return (await _devices.LogAsync(request.Id, 200)).Select(l => new AttendanceDeviceLogDto
            {
                Id = l.Id, Event = (int)l.Event, EventName = EventName(l.Event), Details = l.Details,
                UserName = l.User?.FullName, CreatedAt = l.CreatedAt
            }).ToList();
        }

        public async Task<List<AttendanceDeviceUserDto>> Handle(GetAttendanceDeviceUsersQuery request, CancellationToken ct)
        {
            await VisibleAsync(request.Id);
            return (await _devices.DeviceUsersAsync(request.Id)).Select(u => new AttendanceDeviceUserDto
            {
                Pin = u.Pin, Name = u.Name, UserId = u.UserId, PersonName = u.User?.FullName, Managed = u.Managed, SyncedAt = u.SyncedAt
            }).ToList();
        }

        public static string CommandStateName(AttendanceCommandState state) => state switch
        {
            AttendanceCommandState.Queued => "بالانتظار",
            AttendanceCommandState.Sent => "أُرسل",
            AttendanceCommandState.Succeeded => "نُفّذ",
            AttendanceCommandState.Failed => "فشل",
            AttendanceCommandState.Expired => "انتهت مهلته",
            _ => ""
        };

        public static string EventName(AttendanceDeviceEvent e) => e switch
        {
            AttendanceDeviceEvent.FirstSeen => "اتصال جديد",
            AttendanceDeviceEvent.Approved => "قبول",
            AttendanceDeviceEvent.Rejected => "رفض",
            AttendanceDeviceEvent.Edited => "تعديل",
            AttendanceDeviceEvent.Removed => "إزالة",
            AttendanceDeviceEvent.IpChanged => "تغيّر العنوان",
            AttendanceDeviceEvent.WentOffline => "انقطاع",
            AttendanceDeviceEvent.CameOnline => "عودة الاتصال",
            AttendanceDeviceEvent.Command => "أمر",
            _ => ""
        };
    }
}
