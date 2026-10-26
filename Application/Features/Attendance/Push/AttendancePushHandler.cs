using System.Globalization;
using System.Text;
using Application.Interfaces;
using Domain.Entities.Attendance;

namespace Application.Features.Attendance.Push
{
    /// <summary>رد على جهاز: رمز HTTP ونص عادي</summary>
    public sealed record PushReply(int Status, string Body)
    {
        public static PushReply Ok(string body = "OK") => new(200, body);
        /// <summary>جهاز غير مقبول: تُرفض بياناته فتبقى في ذاكرته (تصل بعد قبوله، أو تُسحب بأمر)</summary>
        public static readonly PushReply NotApproved = new(403, "ERROR: device not approved");
    }

    /// <summary>
    /// طلبات أجهزة البصمة (Push/ADMS) — المتحكم IclockController يمرّرها هنا. الجهاز يعرّف نفسه برقمه التسلسلي فقط:
    /// الجديد يُسجَّل «بانتظار الموافقة» ويُشعَر من يقبل الأجهزة، ولا تُقبل بيانات جهاز غير مقبول.
    /// </summary>
    public sealed class AttendancePushHandler
    {
        private readonly IAttendancePushStore _store;
        private readonly IAttendanceSettings _settings;
        private readonly IUserPermissionService _permissions;
        private readonly INotificationService _notifications;
        private readonly IAttendanceAreaService _areas;

        public AttendancePushHandler(IAttendancePushStore store, IAttendanceSettings settings, IUserPermissionService permissions,
            INotificationService notifications, IAttendanceAreaService areas)
        {
            _store = store;
            _settings = settings;
            _permissions = permissions;
            _notifications = notifications;
            _areas = areas;
        }

        private static bool ValidSerial(string? sn) =>
            !string.IsNullOrWhiteSpace(sn) && sn.Length <= 50 && sn.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');

        private async Task<PushContact?> ContactAsync(string? sn, string? ip, PushDeviceInfo? info, CancellationToken ct)
        {
            if (!ValidSerial(sn)) return null;
            var contact = await _store.ContactAsync(sn!.Trim(), ip, info, ct);
            if (contact.IsNew)
                await AttendanceNotifier.DevicePendingAsync(_permissions, _notifications, contact.Device.Id, contact.Device.SerialNumber, ip);
            if (contact.PreviousIp != null)
                await AttendanceNotifier.DeviceIpChangedAsync(_permissions, _notifications, _areas,
                    contact.Device.Id, contact.Device.Name, contact.Device.AreaId, contact.PreviousIp, contact.Ip);
            return contact;
        }

        /// <summary>GET cdata: المصافحة — مؤشرات آخر ما استلمناه وإعدادات الإرسال (حتى للجهاز المنتظر، كي يتصل ويظهر)</summary>
        public async Task<PushReply> HandshakeAsync(string? sn, string? ip, CancellationToken ct)
        {
            var contact = await ContactAsync(sn, ip, null, ct);
            if (contact == null) return new PushReply(400, "ERROR: SN");
            var d = contact.Device;
            if (d.State == AttendanceDeviceState.Rejected) return new PushReply(403, "ERROR: device rejected");
            return PushReply.Ok(PushProtocol.Handshake(d.SerialNumber, d.AttLogStamp, d.OperLogStamp, d.PhotoStamp,
                _settings.PollSeconds, _settings.TimeZoneHours, wantPhotos: true));
        }

        /// <summary>POST cdata/fdata: بيانات جدول (البصمات، العمليات والقوالب، الصور، معلومات الجهاز)</summary>
        public async Task<PushReply> UploadAsync(string? sn, string? ip, string? table, string? stamp, byte[] body, CancellationToken ct)
        {
            var kind = (table ?? string.Empty).Trim().ToUpperInvariant();
            PushDeviceInfo? info = kind == "OPTIONS" ? PushProtocol.ParseOptions(Text(body)) : null;
            var contact = await ContactAsync(sn, ip, info, ct);
            if (contact == null) return new PushReply(400, "ERROR: SN");
            var device = contact.Device;
            if (kind == "OPTIONS") return PushReply.Ok();
            if (!device.Accepted) return PushReply.NotApproved;

            switch (kind)
            {
                case "ATTLOG":
                {
                    var punches = PushProtocol.ParseAttLog(Text(body));
                    await _store.SavePunchesAsync(device, punches, AttendancePunchSource.Push, stamp, ct);
                    return PushReply.Ok($"OK: {punches.Count}");
                }
                case "OPERLOG":
                case "BIODATA":
                case "USERINFO":
                {
                    var text = Text(body);
                    var operations = PushProtocol.ParseOperations(text);
                    await _store.SaveOperationsAsync(device, operations, stamp, ct);
                    return PushReply.Ok($"OK: {CountLines(text)}");
                }
                case "ATTPHOTO":
                {
                    if (PushProtocol.ParsePunchPhoto(body) is { } photo)
                        await _store.SavePunchPhotoAsync(device, photo, stamp, ct);
                    return PushReply.Ok();
                }
                default:
                    // جداول لا نحتاجها (أحداث الأبواب، الأخطاء…): تُقبل كي لا يعيد الجهاز إرسالها
                    return PushReply.Ok();
            }
        }

        /// <summary>POST querydata: نتائج أوامر الاستعلام في الإصدارات الأحدث — تُقرأ بالقارئ نفسه حسب نوع الجدول</summary>
        public async Task<PushReply> QueryDataAsync(string? sn, string? ip, string? tableName, byte[] body, CancellationToken ct)
        {
            var contact = await ContactAsync(sn, ip, null, ct);
            if (contact == null) return new PushReply(400, "ERROR: SN");
            if (!contact.Device.Accepted) return PushReply.NotApproved;
            var name = (tableName ?? string.Empty).Trim().ToUpperInvariant();
            var text = Text(body);
            if (name is "ATTLOG" or "TRANSACTION")
                await _store.SavePunchesAsync(contact.Device, PushProtocol.ParseAttLog(text), AttendancePunchSource.Query, null, ct);
            else if (name is "USER" or "USERINFO" or "BIODATA" or "TEMPLATEV10" or "BIOPHOTO")
                await _store.SaveOperationsAsync(contact.Device, PushProtocol.ParseOperations(text), null, ct);
            return PushReply.Ok();
        }

        /// <summary>GET getrequest: الاستطلاع الدوري — معلومات الجهاز (INFO) ثم أوامره المنتظرة</summary>
        public async Task<PushReply> PollAsync(string? sn, string? ip, string? info, CancellationToken ct)
        {
            var parsed = PushProtocol.ParseInfo(info);
            var contact = await ContactAsync(sn, ip, parsed == PushDeviceInfo.Empty ? null : parsed, ct);
            if (contact == null) return new PushReply(400, "ERROR: SN");
            if (!contact.Device.Accepted) return PushReply.Ok();
            var commands = await _store.TakeCommandsAsync(contact.Device, _settings.CommandsPerPoll, ct);
            return PushReply.Ok(PushProtocol.FormatCommands(commands));
        }

        /// <summary>POST devicecmd: نتائج الأوامر</summary>
        public async Task<PushReply> CommandResultsAsync(string? sn, string? ip, byte[] body, CancellationToken ct)
        {
            var contact = await ContactAsync(sn, ip, null, ct);
            if (contact == null) return new PushReply(400, "ERROR: SN");
            if (contact.Device.Accepted)
                await _store.CompleteCommandsAsync(contact.Device, PushProtocol.ParseCommandResults(Text(body)), ct);
            return PushReply.Ok();
        }

        /// <summary>GET rtdata?type=time: وقت الخادم المحلي لضبط ساعة الجهاز</summary>
        public PushReply ServerTime()
        {
            var now = DateTime.UtcNow.AddHours(_settings.TimeZoneHours);
            var offset = TimeSpan.FromHours(_settings.TimeZoneHours);
            var sign = offset < TimeSpan.Zero ? "-" : "+";
            return PushReply.Ok($"DateTime={now.ToString(PushProtocol.TimeFormat, CultureInfo.InvariantCulture)},ServerTZ={sign}{offset:hhmm}");
        }

        /// <summary>النص بترميز UTF-8 (الأسماء العربية)؛ البايتات غير الصالحة تُستبدل</summary>
        private static string Text(byte[] body) => Encoding.UTF8.GetString(body);
        private static int CountLines(string text) => text.Split('\n').Count(l => l.Trim().Length > 0);
    }
}
