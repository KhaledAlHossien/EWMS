using System.Security.Cryptography;
using System.Text;
using Application.Features.Attendance.Push;
using Application.Interfaces;
using Domain.Entities.Attendance;
using Infrastructure.Attendance;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    public class AttendanceSyncService : IAttendanceSyncService
    {
        /// <summary>أمر أُرسل ولم يرد عليه الجهاز خلال هذه المدة تنتهي مهلته</summary>
        private static readonly TimeSpan SentTimeout = TimeSpan.FromMinutes(30);
        private const int CommandRetentionDays = 90;

        private readonly DataContext _context;
        private readonly AttendanceDeviceCache _cache;
        private readonly IAttendanceTemplateProtector _protector;
        private readonly IAttendanceSettings _settings;

        public AttendanceSyncService(DataContext context, AttendanceDeviceCache cache, IAttendanceTemplateProtector protector, IAttendanceSettings settings)
        {
            _context = context;
            _cache = cache;
            _protector = protector;
            _settings = settings;
        }

        /// <summary>شخص يجب أن يكون على الجهاز</summary>
        private sealed record Wanted(int UserId, string Pin, string Name, string? Card, bool Admin);

        public async Task<int> SyncAsync(bool all, CancellationToken ct = default)
        {
            var devices = await _context.AttendanceDevices.AsNoTracking()
                .Where(d => d.State == AttendanceDeviceState.Approved && d.AreaId != null && (all || d.NeedsSync))
                .Select(d => new { d.Id, AreaId = d.AreaId!.Value, d.FaceAlgorithm, d.FingerAlgorithm })
                .ToListAsync(ct);
            var queued = 0;
            foreach (var device in devices)
            {
                ct.ThrowIfCancellationRequested();
                // العلامة تُزال قبل الحساب: ما يتغير أثناءه يعلّمه من جديد فيُحسب في الدورة التالية
                await _context.AttendanceDevices.Where(d => d.Id == device.Id).ExecuteUpdateAsync(s => s.SetProperty(d => d.NeedsSync, false), ct);
                queued += await SyncDeviceAsync(device.Id, device.AreaId, device.FaceAlgorithm, device.FingerAlgorithm, ct);
            }
            return queued;
        }

        private async Task<int> SyncDeviceAsync(int deviceId, int areaId, string? faceAlgorithm, string? fingerAlgorithm, CancellationToken ct)
        {
            var today = DateTime.UtcNow.AddHours(_settings.TimeZoneHours).Date;
            var wanted = await _context.AttendanceProfiles.AsNoTracking()
                .Where(p => p.AttendanceNumber != null && p.User.IsActive
                    && (p.ActiveFrom == null || p.ActiveFrom <= today) && (p.ActiveTo == null || p.ActiveTo >= today)
                    && ((p.AreaId ?? p.User.Branch!.AttendanceAreaId) == areaId || p.ExtraAreas.Any(a => a.AreaId == areaId)))
                .Select(p => new Wanted(p.UserId, p.AttendanceNumber!, p.User.FullName, p.CardNumber, p.IsDeviceAdmin))
                .ToListAsync(ct);
            var wantedByPin = wanted.ToDictionary(w => w.Pin);
            var rows = await _context.AttendanceDeviceUsers.Where(u => u.DeviceId == deviceId).ToListAsync(ct);
            var rowsByPin = rows.ToDictionary(r => r.Pin);
            var now = DateTime.UtcNow;

            var userCommands = new List<AttendanceDeviceCommand>();
            var templateCommands = new List<AttendanceDeviceCommand>();

            // من وضعناه ولم يعد مطلوباً (خرج من المنطقة، عُطّل، تغيّر رقمه، انتهت فترته): يُحذف من الجهاز
            foreach (var row in rows.Where(r => r.Managed && !wantedByPin.ContainsKey(r.Pin)))
            {
                userCommands.Add(Command(deviceId, AttendanceCommandKind.DeleteUser, PushProtocol.DeleteUser(row.Pin), $"حذف {row.Name} ({row.Pin})", row.UserId, now));
                _context.AttendanceDeviceUsers.Remove(row);
            }

            // بيانات القوالب وصور التسجيل للمطلوبين بلا محتواها: المحتوى يُقرأ لمن تغيّرت قوالبه فقط
            var userIds = wanted.Select(w => w.UserId).ToList();
            var templates = new List<TemplateMeta>();
            var photos = new List<PhotoMeta>();
            foreach (var chunk in userIds.Chunk(500))
            {
                templates.AddRange(await _context.AttendanceBiometrics.AsNoTracking().Where(b => chunk.Contains(b.UserId) && b.IsValid)
                    .Select(b => new TemplateMeta(b.Id, b.UserId, b.Type, b.No, b.Index, b.IsValid, b.Duress, b.MajorVersion, b.MinorVersion, b.Format, b.Hash))
                    .ToListAsync(ct));
                photos.AddRange(await _context.AttendanceBioPhotos.AsNoTracking().Where(p => chunk.Contains(p.UserId))
                    .Select(p => new PhotoMeta(p.UserId, p.Hash)).ToListAsync(ct));
            }
            var templatesByUser = templates.ToLookup(t => t.UserId);
            var photoByUser = photos.ToDictionary(p => p.UserId);
            var templateSends = new List<(Wanted Person, TemplateMeta Template)>();
            var photoSends = new List<Wanted>();

            foreach (var person in wanted)
            {
                var userHash = Hash($"{person.Pin}|{person.Name}|{person.Card}|{person.Admin}");
                rowsByPin.TryGetValue(person.Pin, out var row);
                if (row == null)
                {
                    row = new AttendanceDeviceUser { DeviceId = deviceId, Pin = person.Pin };
                    _context.AttendanceDeviceUsers.Add(row);
                }
                if (!row.Managed || row.UserId != person.UserId || row.SyncedHash != userHash)
                {
                    userCommands.Add(Command(deviceId, AttendanceCommandKind.SyncUser,
                        PushProtocol.UpdateUser(person.Pin, person.Name, person.Admin, person.Card), $"إضافة {person.Name} ({person.Pin})", person.UserId, now));
                    row.Managed = true;
                    row.UserId = person.UserId;
                    row.Name = PushProtocol.CleanName(person.Name);
                    row.SyncedHash = userHash;
                    row.SyncedAt = now;
                }

                // القالب يُرسل للجهاز ذي الخوارزمية نفسها؛ وجه بخوارزمية مختلفة يُستبدل بصورة التسجيل (يستخرج منها الجهاز قالبه)
                var send = templatesByUser[person.UserId].Where(t => Compatible(t, faceAlgorithm, fingerAlgorithm)).ToList();
                var hasFace = send.Any(t => t.Type is AttendanceBiometricType.VisibleFace or AttendanceBiometricType.Face);
                photoByUser.TryGetValue(person.UserId, out var photo);
                var sendPhoto = !hasFace && photo != null && faceAlgorithm != null;
                var bioHash = Hash(string.Join(";", send.OrderBy(t => t.Type).ThenBy(t => t.No).ThenBy(t => t.Index)
                    .Select(t => $"{(int)t.Type}.{t.No}.{t.Index}:{t.Hash}")) + (sendPhoto ? $";photo:{photo!.Hash}" : ""));
                if (row.BiometricHash == bioHash) continue;
                templateSends.AddRange(send.Select(t => (person, t)));
                if (sendPhoto) photoSends.Add(person);
                row.BiometricHash = bioHash;
            }

            // محتوى ما سيُرسل فقط: يُفك ويوضع في الأوامر
            var data = new Dictionary<long, byte[]>();
            foreach (var chunk in templateSends.Select(s => s.Template.Id).Chunk(500))
                foreach (var row in await _context.AttendanceBiometrics.AsNoTracking().Where(b => chunk.Contains(b.Id))
                             .Select(b => new { b.Id, b.Data }).ToListAsync(ct))
                    data[row.Id] = row.Data;
            foreach (var (person, t) in templateSends)
            {
                if (!data.TryGetValue(t.Id, out var stored)) continue;
                var template = Encoding.ASCII.GetString(_protector.Unprotect(stored));
                templateCommands.Add(Command(deviceId, AttendanceCommandKind.SyncBiometric,
                    PushProtocol.UpdateTemplate(person.Pin, (int)t.Type, t.No, t.Index, t.IsValid, t.Duress, t.MajorVersion, t.MinorVersion, t.Format, template),
                    $"{(t.Type == AttendanceBiometricType.Finger ? "قالب إصبع" : "قالب وجه")} {person.Name} ({person.Pin})", person.UserId, now));
            }
            foreach (var person in photoSends)
            {
                var stored = await _context.AttendanceBioPhotos.AsNoTracking().Where(p => p.UserId == person.UserId).Select(p => p.Data).FirstOrDefaultAsync(ct);
                if (stored == null) continue;
                templateCommands.Add(Command(deviceId, AttendanceCommandKind.SyncBioPhoto,
                    PushProtocol.UpdateBioPhoto(person.Pin, _protector.Unprotect(stored)), $"صورة وجه {person.Name} ({person.Pin})", person.UserId, now));
            }

            // الأشخاص أولاً ثم القوالب في حفظ لاحق: ترتيب الأوامر = ترتيب أرقامها، والقالب لا يُقبل قبل صاحبه
            _context.AttendanceDeviceCommands.AddRange(userCommands);
            await _context.SaveChangesAsync(ct);
            if (templateCommands.Count > 0)
            {
                _context.AttendanceDeviceCommands.AddRange(templateCommands);
                await _context.SaveChangesAsync(ct);
            }
            _context.ChangeTracker.Clear();

            var total = userCommands.Count + templateCommands.Count;
            if (total > 0) _cache.CommandsQueued(deviceId);
            return total;
        }

        private sealed record TemplateMeta(long Id, int UserId, AttendanceBiometricType Type, int No, int Index, bool IsValid, bool Duress,
            string MajorVersion, string MinorVersion, int Format, string Hash);
        private sealed record PhotoMeta(int UserId, string Hash);

        /// <summary>الخوارزمية غير معروفة (من أي طرف) = يُرسل؛ معروفة من الطرفين ومختلفة = لا يُرسل</summary>
        private static bool Compatible(TemplateMeta t, string? faceAlgorithm, string? fingerAlgorithm)
        {
            var device = t.Type == AttendanceBiometricType.Finger ? fingerAlgorithm : faceAlgorithm;
            return string.IsNullOrWhiteSpace(device) || string.IsNullOrWhiteSpace(t.MajorVersion)
                || string.Equals(device.Trim(), t.MajorVersion.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private static AttendanceDeviceCommand Command(int deviceId, AttendanceCommandKind kind, string text, string summary, int? userId, DateTime now) =>
            new() { DeviceId = deviceId, Kind = kind, Text = text, Summary = summary.Length <= 300 ? summary : summary[..300], UserId = userId, CreatedAt = now };

        private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

        public async Task MarkUsersAsync(IReadOnlyCollection<int> userIds, IReadOnlyCollection<int>? previousBranchIds = null,
            IReadOnlyCollection<int>? previousAreaIds = null, CancellationToken ct = default)
        {
            var areas = await AttendanceSyncMarks.AreasOfAsync(_context, userIds, ct);
            if (previousBranchIds is { Count: > 0 })
                areas.AddRange(await _context.Branches.AsNoTracking()
                    .Where(b => previousBranchIds.Contains(b.Id) && b.AttendanceAreaId != null)
                    .Select(b => b.AttendanceAreaId!.Value).ToListAsync(ct));
            if (previousAreaIds != null) areas.AddRange(previousAreaIds);
            await AttendanceSyncMarks.MarkAreasAsync(_context, areas.Distinct().ToList(), ct);
        }

        public async Task<List<DeviceStatusChange>> DetectStatusChangesAsync(DateTime offlineBeforeUtc, CancellationToken ct = default)
        {
            var now = DateTime.UtcNow;
            var wentOffline = await _context.AttendanceDevices.AsNoTracking()
                .Where(d => d.State == AttendanceDeviceState.Approved && d.OfflineNotifiedAt == null && d.LastSeenAt < offlineBeforeUtc)
                .Select(d => new DeviceStatusChange(d.Id, d.Name, d.AreaId, false)).ToListAsync(ct);
            var cameBack = await _context.AttendanceDevices.AsNoTracking()
                .Where(d => d.State == AttendanceDeviceState.Approved && d.OfflineNotifiedAt != null && d.LastSeenAt > d.OfflineNotifiedAt)
                .Select(d => new DeviceStatusChange(d.Id, d.Name, d.AreaId, true)).ToListAsync(ct);

            foreach (var change in wentOffline.Concat(cameBack))
            {
                await _context.AttendanceDevices.Where(d => d.Id == change.DeviceId)
                    .ExecuteUpdateAsync(s => s.SetProperty(d => d.OfflineNotifiedAt, change.Online ? null : now), ct);
                _context.AttendanceDeviceLogs.Add(new AttendanceDeviceLog
                {
                    DeviceId = change.DeviceId, CreatedAt = now,
                    Event = change.Online ? AttendanceDeviceEvent.CameOnline : AttendanceDeviceEvent.WentOffline,
                    Details = change.Online ? "عاد للاتصال" : "انقطع عن الخادم"
                });
            }
            if (wentOffline.Count + cameBack.Count > 0) await _context.SaveChangesAsync(ct);
            return [.. wentOffline, .. cameBack];
        }

        public async Task<int> CleanupAsync(DateTime photosBefore, CancellationToken ct = default)
        {
            var now = DateTime.UtcNow;
            // الصور: السجل يُحذف (ومحتواه في القاعدة بالحذف المتتالي)، وملف المجلد يحذفه منظّف المرفقات لاحقاً
            var photos = 0;
            while (true)
            {
                var batch = await _context.AttendancePunchPhotos.Where(p => p.PunchTime < photosBefore)
                    .OrderBy(p => p.Id).Take(1000).Select(p => p.Id).ToListAsync(ct);
                if (batch.Count == 0) break;
                await _context.AttendancePunchPhotoContents.Where(c => batch.Contains(c.PhotoId)).ExecuteDeleteAsync(ct);
                photos += await _context.AttendancePunchPhotos.Where(p => batch.Contains(p.Id)).ExecuteDeleteAsync(ct);
            }

            var sentBefore = now - SentTimeout;
            await _context.AttendanceDeviceCommands
                .Where(c => c.State == AttendanceCommandState.Sent && c.SentAt < sentBefore)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.State, AttendanceCommandState.Expired).SetProperty(c => c.CompletedAt, now), ct);

            var keepAfter = now.AddDays(-CommandRetentionDays);
            await _context.AttendanceDeviceCommands
                .Where(c => c.State != AttendanceCommandState.Queued && c.State != AttendanceCommandState.Sent && c.CompletedAt < keepAfter)
                .ExecuteDeleteAsync(ct);
            return photos;
        }
    }
}
