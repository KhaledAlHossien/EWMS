using System.Security.Cryptography;
using System.Text;
using Application.Features.Attendance;
using Application.Features.Attendance.Push;
using Application.Interfaces;
using Domain.Entities.Attendance;
using Infrastructure.Attendance;
using Infrastructure.Persistence.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    public class AttendancePushStore : IAttendancePushStore
    {
        private static readonly TimeSpan WriteEvery = TimeSpan.FromMinutes(1);
        private const int PinChunk = 500;

        private readonly DataContext _context;
        private readonly AttendanceDeviceCache _cache;
        private readonly IAttendanceTemplateProtector _protector;
        private readonly IFileStore _files;
        private readonly IAttendanceRecalcQueue _recalc;

        public AttendancePushStore(DataContext context, AttendanceDeviceCache cache, IAttendanceTemplateProtector protector, IFileStore files,
            IAttendanceRecalcQueue recalc)
        {
            _recalc = recalc;
            _context = context;
            _cache = cache;
            _protector = protector;
            _files = files;
        }

        private static PushDevice Snapshot(AttendanceDevice d) =>
            new(d.Id, d.SerialNumber, d.Name, d.State, d.AreaId, d.AttLogStamp, d.OperLogStamp, d.PhotoStamp);

        // ───────── الاتصال ─────────

        public async Task<PushContact> ContactAsync(string serialNumber, string? ip, PushDeviceInfo? info, CancellationToken ct = default)
        {
            var now = DateTime.UtcNow;
            var hasInfo = info != null && info != PushDeviceInfo.Empty;
            if (!hasInfo && _cache.TryGet(serialNumber, out var cached)
                && (ip == null || ip == cached.Ip) && now - cached.WrittenAtUtc < WriteEvery)
                return new PushContact(cached.Device, false, null, cached.Ip);

            var device = await _context.AttendanceDevices.AsNoTracking().FirstOrDefaultAsync(d => d.SerialNumber == serialNumber, ct);
            if (device == null)
            {
                device = new AttendanceDevice
                {
                    SerialNumber = serialNumber, Name = serialNumber, State = AttendanceDeviceState.Pending,
                    FirstSeenAt = now, LastSeenAt = now, IpAddress = ip
                };
                ApplyInfo(device, info);
                _context.AttendanceDevices.Add(device);
                try
                {
                    await _context.SaveChangesAsync(ct);
                }
                catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
                {
                    // سجّله طلب متزامن من الجهاز نفسه: نقرؤه كما حُفظ
                    _context.ChangeTracker.Clear();
                    return await ContactAsync(serialNumber, ip, info, ct);
                }
                _context.AttendanceDeviceLogs.Add(Log(device.Id, AttendanceDeviceEvent.FirstSeen, $"أول اتصال من {ip ?? "عنوان غير معروف"}"));
                await _context.SaveChangesAsync(ct);
                _cache.Set(serialNumber, new AttendanceDeviceCache.Entry(Snapshot(device), now, ip));
                return new PushContact(Snapshot(device), true, null, ip);
            }

            // جهاز أُزيل ثم اتصل: يعود «بانتظار الموافقة»
            var returned = device.State == AttendanceDeviceState.Removed;
            var previousIp = ip != null && device.IpAddress != null && ip != device.IpAddress && device.State == AttendanceDeviceState.Approved
                ? device.IpAddress : null;
            var state = returned ? AttendanceDeviceState.Pending : device.State;
            var newIp = ip ?? device.IpAddress;
            ApplyInfo(device, info);

            await _context.AttendanceDevices.Where(d => d.Id == device.Id).ExecuteUpdateAsync(s => s
                .SetProperty(d => d.LastSeenAt, now)
                .SetProperty(d => d.IpAddress, newIp)
                .SetProperty(d => d.State, state)
                .SetProperty(d => d.Model, device.Model)
                .SetProperty(d => d.Firmware, device.Firmware)
                .SetProperty(d => d.PushVersion, device.PushVersion)
                .SetProperty(d => d.FingerAlgorithm, device.FingerAlgorithm)
                .SetProperty(d => d.FaceAlgorithm, device.FaceAlgorithm)
                .SetProperty(d => d.UserCount, device.UserCount)
                .SetProperty(d => d.FingerCount, device.FingerCount)
                .SetProperty(d => d.FaceCount, device.FaceCount)
                .SetProperty(d => d.PunchCount, device.PunchCount), ct);

            if (returned)
                _context.AttendanceDeviceLogs.Add(Log(device.Id, AttendanceDeviceEvent.FirstSeen, $"اتصل بعد إزالته من {newIp ?? "عنوان غير معروف"} — عاد بانتظار الموافقة"));
            if (previousIp != null)
                _context.AttendanceDeviceLogs.Add(Log(device.Id, AttendanceDeviceEvent.IpChanged, $"تغيّر العنوان من {previousIp} إلى {ip}"));
            if (returned || previousIp != null) await _context.SaveChangesAsync(ct);

            device.State = state;
            var snapshot = Snapshot(device);
            _cache.Set(serialNumber, new AttendanceDeviceCache.Entry(snapshot, now, newIp));
            return new PushContact(snapshot, returned, previousIp, newIp);
        }

        /// <summary>ما يبلّغ به الجهاز يحلّ محل القديم (الحقل الفارغ لا يمحو ما سبق)</summary>
        private static void ApplyInfo(AttendanceDevice d, PushDeviceInfo? info)
        {
            if (info == null) return;
            d.Model = Cut(info.Model, 100) ?? d.Model;
            d.Firmware = Cut(info.Firmware, 100) ?? d.Firmware;
            d.PushVersion = Cut(info.PushVersion, 20) ?? d.PushVersion;
            d.FingerAlgorithm = Cut(info.FingerAlgorithm, 20) ?? d.FingerAlgorithm;
            d.FaceAlgorithm = Cut(info.FaceAlgorithm, 20) ?? d.FaceAlgorithm;
            d.UserCount = info.Users ?? d.UserCount;
            d.FingerCount = info.Fingers ?? d.FingerCount;
            d.FaceCount = info.Faces ?? d.FaceCount;
            d.PunchCount = info.Punches ?? d.PunchCount;
        }

        private static string? Cut(string? s, int max) => string.IsNullOrWhiteSpace(s) ? null : s.Length <= max ? s : s[..max];

        private static AttendanceDeviceLog Log(int deviceId, AttendanceDeviceEvent e, string details) =>
            new() { DeviceId = deviceId, Event = e, Details = details, CreatedAt = DateTime.UtcNow };

        private enum Stamp { AttLog, OperLog, Photo }

        /// <summary>مؤشر آخر ما استلمناه من جدول (يعيده الخادم للجهاز في المصافحة التالية، فتُسقط لقطته)</summary>
        private async Task SetStampAsync(PushDevice device, string? stamp, Stamp kind, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(stamp) || stamp.Length > 30) return;
            var devices = _context.AttendanceDevices.Where(d => d.Id == device.Id);
            await (kind switch
            {
                Stamp.AttLog => devices.ExecuteUpdateAsync(s => s.SetProperty(d => d.AttLogStamp, stamp), ct),
                Stamp.OperLog => devices.ExecuteUpdateAsync(s => s.SetProperty(d => d.OperLogStamp, stamp), ct),
                _ => devices.ExecuteUpdateAsync(s => s.SetProperty(d => d.PhotoStamp, stamp), ct)
            });
            _cache.Invalidate(device.SerialNumber);
        }

        /// <summary>أصحاب الأرقام (بصيغتها المحفوظة بلا أصفار بادئة)</summary>
        private async Task<Dictionary<string, int>> OwnersAsync(IEnumerable<string> pins, CancellationToken ct)
        {
            var keys = pins.Select(AttendanceRules.PinKey).Distinct().ToList();
            var owners = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var chunk in keys.Chunk(PinChunk))
                foreach (var row in await _context.AttendanceProfiles.AsNoTracking()
                             .Where(p => p.AttendanceNumber != null && chunk.Contains(p.AttendanceNumber))
                             .Select(p => new { p.AttendanceNumber, p.UserId }).ToListAsync(ct))
                    owners[row.AttendanceNumber!] = row.UserId;
            return owners;
        }

        // ───────── البصمات ─────────

        public async Task<int> SavePunchesAsync(PushDevice device, IReadOnlyList<PushPunch> punches, AttendancePunchSource source, string? stamp, CancellationToken ct = default)
        {
            var added = 0;
            if (punches.Count > 0)
            {
                var distinct = punches.GroupBy(p => (p.Pin, p.Time)).Select(g => g.First()).ToList();
                var min = distinct.Min(p => p.Time);
                var max = distinct.Max(p => p.Time);
                var existing = new HashSet<(string, DateTime)>();
                foreach (var chunk in distinct.Select(p => p.Pin).Distinct().Chunk(PinChunk))
                    foreach (var row in await _context.AttendancePunches.AsNoTracking()
                                 .Where(p => p.DeviceSerial == device.SerialNumber && chunk.Contains(p.Pin) && p.PunchTime >= min && p.PunchTime <= max)
                                 .Select(p => new { p.Pin, p.PunchTime }).ToListAsync(ct))
                        existing.Add((row.Pin, row.PunchTime));

                var owners = await OwnersAsync(distinct.Select(p => p.Pin), ct);
                var now = DateTime.UtcNow;
                var rows = distinct.Where(p => !existing.Contains((p.Pin, p.Time))).Select(p => new AttendancePunch
                {
                    DeviceId = device.Id,
                    DeviceSerial = device.SerialNumber,
                    Pin = p.Pin,
                    UserId = owners.TryGetValue(AttendanceRules.PinKey(p.Pin), out var owner) ? owner : null,
                    PunchTime = p.Time,
                    Status = p.Status,
                    VerifyType = p.Verify,
                    WorkCode = p.WorkCode.Length <= 20 ? p.WorkCode : p.WorkCode[..20],
                    Source = source,
                    ReceivedAt = now
                }).ToList();

                if (rows.Count > 0)
                {
                    _context.AttendancePunches.AddRange(rows);
                    try
                    {
                        await _context.SaveChangesAsync(ct);
                        added = rows.Count;
                    }
                    catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
                    {
                        // دفعة متزامنة حفظت بعضها: واحدة واحدة، والمكرر يُتخطى
                        _context.ChangeTracker.Clear();
                        foreach (var row in rows)
                        {
                            _context.AttendancePunches.Add(row);
                            try { await _context.SaveChangesAsync(ct); added++; }
                            catch (DbUpdateException dup) when (dup.InnerException is SqlException { Number: 2601 or 2627 }) { }
                            _context.ChangeTracker.Clear();
                        }
                    }
                    // الحساب: يوم كل بصمة واليوم الذي قبله (الجدول الليلي ينتهي في اليوم التالي)
                    var touched = rows.Where(r => r.UserId != null).GroupBy(r => r.UserId!.Value)
                        .Select(g => (g.Key, g.Min(r => r.PunchTime).Date.AddDays(-1), g.Max(r => r.PunchTime).Date)).ToList();
                    if (touched.Count > 0) await _recalc.EnqueueManyAsync(touched, "بصمات جديدة");
                }
            }
            await SetStampAsync(device, stamp, Stamp.AttLog, ct);
            return added;
        }

        // ───────── المستخدمون والقوالب ─────────

        public async Task<int> SaveOperationsAsync(PushDevice device, PushOperations operations, string? stamp, CancellationToken ct = default)
        {
            var pins = operations.Users.Select(u => u.Pin)
                .Concat(operations.Templates.Select(t => t.Pin))
                .Concat(operations.BioPhotos.Select(p => p.Pin)).ToList();
            var owners = await OwnersAsync(pins, ct);
            int? OwnerOf(string pin) => owners.TryGetValue(AttendanceRules.PinKey(pin), out var id) ? id : null;
            var now = DateTime.UtcNow;

            // من على الجهاز: يُسجَّل من ليس عندنا كما هو (لا تحذفه المزامنة)
            if (operations.Users.Count > 0)
            {
                var userPins = operations.Users.Select(u => u.Pin).Distinct().ToList();
                var known = await _context.AttendanceDeviceUsers
                    .Where(u => u.DeviceId == device.Id && userPins.Contains(u.Pin)).ToDictionaryAsync(u => u.Pin, ct);
                foreach (var user in operations.Users.GroupBy(u => u.Pin).Select(g => g.Last()))
                {
                    var name = PushProtocol.CleanName(user.Name);
                    if (known.TryGetValue(user.Pin, out var row)) { row.Name = name; continue; }
                    _context.AttendanceDeviceUsers.Add(new AttendanceDeviceUser
                    {
                        DeviceId = device.Id, Pin = user.Pin, UserId = OwnerOf(user.Pin), Name = name, Managed = false
                    });
                }
                await _context.SaveChangesAsync(ct);
            }

            var changed = new HashSet<int>();

            // القوالب: قالب جديد أو متغيّر لشخص معروف يُحفظ مشفّراً (قالب شخص غير معروف لا يُحفظ)
            foreach (var t in operations.Templates.GroupBy(t => (t.Pin, t.Type, t.No, t.Index)).Select(g => g.Last()))
            {
                if (OwnerOf(t.Pin) is not int userId) continue;
                var hash = Hash(t.Template);
                var row = await _context.AttendanceBiometrics.FirstOrDefaultAsync(b =>
                    b.UserId == userId && b.Type == (AttendanceBiometricType)t.Type && b.No == t.No && b.Index == t.Index, ct);
                if (row?.Hash == hash) continue;
                row ??= _context.AttendanceBiometrics.Add(new AttendanceBiometric
                {
                    UserId = userId, Type = (AttendanceBiometricType)t.Type, No = t.No, Index = t.Index
                }).Entity;
                row.IsValid = t.Valid;
                row.Duress = t.Duress;
                row.MajorVersion = Cut(t.MajorVersion, 20) ?? string.Empty;
                row.MinorVersion = Cut(t.MinorVersion, 20) ?? string.Empty;
                row.Format = t.Format;
                row.Data = _protector.Protect(Encoding.ASCII.GetBytes(t.Template));
                row.Hash = hash;
                row.SourceDeviceId = device.Id;
                row.UpdatedAt = now;
                changed.Add(userId);
            }

            // صورة تسجيل الوجه: واحدة لكل شخص
            foreach (var photo in operations.BioPhotos.GroupBy(p => p.Pin).Select(g => g.Last()))
            {
                if (OwnerOf(photo.Pin) is not int userId || !PushProtocol.IsJpeg(photo.Content)) continue;
                var hash = Hash(photo.Content);
                var row = await _context.AttendanceBioPhotos.FirstOrDefaultAsync(p => p.UserId == userId, ct);
                if (row?.Hash == hash) continue;
                row ??= _context.AttendanceBioPhotos.Add(new AttendanceBioPhoto { UserId = userId }).Entity;
                row.Data = _protector.Protect(photo.Content);
                row.Hash = hash;
                row.Size = photo.Content.Length;
                row.SourceDeviceId = device.Id;
                row.UpdatedAt = now;
                changed.Add(userId);
            }

            if (changed.Count > 0)
            {
                await _context.SaveChangesAsync(ct);
                // تصل القوالب الجديدة إلى بقية أجهزة مناطق أصحابها
                await AttendanceSyncMarks.MarkUsersAsync(_context, changed, ct);
            }
            await SetStampAsync(device, stamp, Stamp.OperLog, ct);
            return changed.Count;
        }

        private static string Hash(string text) => Hash(Encoding.ASCII.GetBytes(text));
        private static string Hash(byte[] data) => Convert.ToHexString(SHA256.HashData(data));

        // ───────── صور لحظة البصم ─────────

        public async Task SavePunchPhotoAsync(PushDevice device, PushPhoto photo, string? stamp, CancellationToken ct = default)
        {
            var exists = await _context.AttendancePunchPhotos.AnyAsync(p =>
                p.DeviceSerial == device.SerialNumber && p.Pin == photo.Pin && p.PunchTime == photo.Time, ct);
            if (!exists)
            {
                var owners = await OwnersAsync([photo.Pin], ct);
                // المحتوى أولاً (مجلد المرفقات، أو null في وضع القاعدة) ثم السجل
                var key = await _files.SaveAsync(FileAreas.AttendancePhotos, photo.Content, ct);
                var row = new AttendancePunchPhoto
                {
                    DeviceId = device.Id, DeviceSerial = device.SerialNumber, Pin = photo.Pin,
                    UserId = owners.TryGetValue(AttendanceRules.PinKey(photo.Pin), out var owner) ? owner : null,
                    PunchTime = photo.Time, Size = photo.Content.Length, StorageKey = key, ReceivedAt = DateTime.UtcNow
                };
                if (key == null) row.Content = new AttendancePunchPhotoContent { Data = photo.Content };
                _context.AttendancePunchPhotos.Add(row);
                try { await _context.SaveChangesAsync(ct); }
                catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 }) { _context.ChangeTracker.Clear(); }
            }
            await SetStampAsync(device, stamp, Stamp.Photo, ct);
        }

        // ───────── الأوامر ─────────

        public async Task<List<(long Id, string Text)>> TakeCommandsAsync(PushDevice device, int max, CancellationToken ct = default)
        {
            if (!_cache.MaybeHasCommands(device.Id)) return [];
            var checkStart = DateTime.UtcNow.Ticks;
            var commands = await _context.AttendanceDeviceCommands.AsNoTracking()
                .Where(c => c.DeviceId == device.Id && c.State == AttendanceCommandState.Queued)
                .OrderBy(c => c.Id).Take(max)
                .Select(c => new { c.Id, c.Text }).ToListAsync(ct);
            if (commands.Count == 0)
            {
                _cache.NoCommands(device.Id, checkStart);
                return [];
            }
            var ids = commands.Select(c => c.Id).ToList();
            var now = DateTime.UtcNow;
            await _context.AttendanceDeviceCommands.Where(c => ids.Contains(c.Id))
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.State, AttendanceCommandState.Sent).SetProperty(c => c.SentAt, now), ct);
            return commands.Select(c => (c.Id, c.Text)).ToList();
        }

        public async Task CompleteCommandsAsync(PushDevice device, IReadOnlyList<PushCommandResult> results, CancellationToken ct = default)
        {
            var now = DateTime.UtcNow;
            foreach (var result in results)
            {
                var state = result.ReturnCode >= 0 ? AttendanceCommandState.Succeeded : AttendanceCommandState.Failed;
                await _context.AttendanceDeviceCommands
                    .Where(c => c.Id == result.Id && c.DeviceId == device.Id && c.State != AttendanceCommandState.Succeeded)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(c => c.State, state)
                        .SetProperty(c => c.ReturnCode, result.ReturnCode)
                        .SetProperty(c => c.CompletedAt, now), ct);
            }
        }
    }
}
