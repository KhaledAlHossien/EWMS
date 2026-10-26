using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Interfaces;
using MediatR;

namespace Application.Features.Attendance
{
    public record SearchAttendancePunchesQuery(AttendancePunchSearchDto Dto) : IRequest<PagedResultDto<AttendancePunchDto>>;
    /// <summary>بصماتي (ViewMyAttendance)</summary>
    public record GetMyPunchesQuery(DateTime? From, DateTime? To, int Page, int PageSize) : IRequest<PagedResultDto<AttendancePunchDto>>;
    public record GetUnknownPinsQuery(int? AreaId) : IRequest<List<UnknownPinDto>>;
    public record GetPunchPhotoQuery(long PunchId) : IRequest<PunchPhotoFileDto>;

    public record PunchPhotoFileDto(string FileName, Stream Content);

    /// <summary>
    /// البصمات الخام: كما وصلت من الأجهزة، بحدود صلاحيات العرض التي يملكها المستخدم (AttendanceRules.ViewScope).
    /// صورة لحظة البصم بصلاحيتها (ViewAttendancePhotos) وللبصمات التي يراها فقط.
    /// </summary>
    public class AttendancePunchHandler :
        IRequestHandler<SearchAttendancePunchesQuery, PagedResultDto<AttendancePunchDto>>,
        IRequestHandler<GetMyPunchesQuery, PagedResultDto<AttendancePunchDto>>,
        IRequestHandler<GetUnknownPinsQuery, List<UnknownPinDto>>,
        IRequestHandler<GetPunchPhotoQuery, PunchPhotoFileDto>
    {
        /// <summary>أطول فترة في بحث واحد</summary>
        public const int MaxRangeDays = 366;

        private readonly IAttendancePunchService _punches;
        private readonly IAttendanceSettings _settings;
        private readonly IUserService _users;
        private readonly IUserPermissionService _permissions;
        private readonly IFileStore _files;

        public AttendancePunchHandler(IAttendancePunchService punches, IAttendanceSettings settings, IUserService users,
            IUserPermissionService permissions, IFileStore files)
        {
            _punches = punches;
            _settings = settings;
            _users = users;
            _permissions = permissions;
            _files = files;
        }

        /// <summary>الفترة بأيام كاملة بتوقيت الأجهزة: الافتراضي اليوم</summary>
        private (DateTime From, DateTime To) Range(DateTime? from, DateTime? to)
        {
            var today = DateTime.UtcNow.AddHours(_settings.TimeZoneHours).Date;
            var start = (from ?? to ?? today).Date;
            var end = (to ?? from ?? today).Date;
            if (start > end) (start, end) = (end, start);
            if ((end - start).TotalDays > MaxRangeDays) throw new InvalidOperationException($"أطول فترة للبحث {MaxRangeDays} يوماً");
            return (start, end.AddDays(1));
        }

        private async Task<PagedResultDto<AttendancePunchDto>> PageAsync(AttendanceScope scope, PunchFilter filter, int page, int pageSize, Viewer viewer)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 200);
            var (items, total) = await _punches.SearchAsync(scope, filter, page, pageSize);
            var photos = viewer.IsSuperAdmin || viewer.Has(AppPermissions.ViewAttendancePhotos);
            return new PagedResultDto<AttendancePunchDto>
            {
                Page = page, PageSize = pageSize, TotalCount = total,
                Items = items.Select(r =>
                {
                    var (key, name) = AttendanceRules.Verify(r.Punch.VerifyType);
                    return new AttendancePunchDto
                    {
                        Id = r.Punch.Id, PunchTime = r.Punch.PunchTime, Pin = r.Punch.Pin, UserId = r.Punch.UserId,
                        PersonName = r.PersonName, PersonKind = r.PersonKind is { } k ? (int)k : null, DepartmentName = r.DepartmentName,
                        DeviceId = r.Punch.DeviceId, DeviceName = r.DeviceName, AreaName = r.AreaName ?? string.Empty,
                        Status = r.Punch.Status, Verify = key, VerifyName = name, Source = (int)r.Punch.Source,
                        ReceivedAt = r.Punch.ReceivedAt, HasPhoto = photos && r.HasPhoto
                    };
                }).ToList()
            };
        }

        public async Task<PagedResultDto<AttendancePunchDto>> Handle(SearchAttendancePunchesQuery request, CancellationToken ct)
        {
            var viewer = await Viewer.CurrentAsync(_users, _permissions);
            var d = request.Dto;
            var (from, to) = Range(d.From, d.To);
            var filter = new PunchFilter(from, to, d.AreaId, d.DeviceId, d.UserId, d.Query, d.UnknownOnly);
            return await PageAsync(AttendanceRules.ViewScope(viewer), filter, d.Page, d.PageSize, viewer);
        }

        public async Task<PagedResultDto<AttendancePunchDto>> Handle(GetMyPunchesQuery request, CancellationToken ct)
        {
            var viewer = await Viewer.CurrentAsync(_users, _permissions);
            var (from, to) = Range(request.From, request.To);
            var mine = new AttendanceScope(false, null, null, null, viewer.Id);
            return await PageAsync(mine, new PunchFilter(from, to, null, null, null, null, false), request.Page, request.PageSize, viewer);
        }

        public async Task<List<UnknownPinDto>> Handle(GetUnknownPinsQuery request, CancellationToken ct)
        {
            // الأرقام غير المعروفة تُعرف بأجهزتها: لمن يرى منطقته أو الكل
            var scope = AttendanceRules.ViewScope(await Viewer.CurrentAsync(_users, _permissions));
            var limit = scope.All ? AreaLimit.Everything : scope.AreaId is int a ? new AreaLimit(false, a) : AreaLimit.Nothing;
            if (limit.IsNothing) return [];
            return (await _punches.UnknownPinsAsync(limit, request.AreaId)).Select(r => new UnknownPinDto
            {
                Pin = r.Pin, Punches = r.Punches, FirstAt = r.FirstAt, LastAt = r.LastAt, Devices = string.Join("، ", r.Devices)
            }).ToList();
        }

        public async Task<PunchPhotoFileDto> Handle(GetPunchPhotoQuery request, CancellationToken ct)
        {
            var viewer = await Viewer.CurrentAsync(_users, _permissions);
            if (!viewer.IsSuperAdmin && !viewer.Has(AppPermissions.ViewAttendancePhotos))
                throw new UnauthorizedAccessException("لا تملك عرض صور لحظة البصم");
            if (!await _punches.InScopeAsync(AttendanceRules.ViewScope(viewer), request.PunchId))
                throw new KeyNotFoundException("البصمة غير موجودة");
            var photo = await _punches.PhotoForAsync(request.PunchId) ?? throw new KeyNotFoundException("لا صورة لهذه البصمة");
            var content = await StoredFiles.OpenAsync(_files, photo.StorageKey, () => _punches.PhotoDataAsync(photo.Id));
            return new PunchPhotoFileDto($"punch-{request.PunchId}.jpg", content);
        }
    }
}
