using System.Collections.Concurrent;
using Application.Interfaces;

namespace Infrastructure.Attendance
{
    /// <summary>
    /// ذاكرة مؤقتة لأجهزة البصمة (مفردة في الخادم): الجهاز يستطلع كل بضع ثوانٍ، فلا يُقرأ سجله ولا يُكتب «آخر اتصال» في
    /// القاعدة مع كل طلب (مرة في الدقيقة)، ولا يُسأل عن أوامره إلا إن وُضع له أمر منذ آخر فحص فارغ (أو مرّت دقيقة).
    /// أي تعديل على جهاز من صفحة الأجهزة يُسقط لقطته فتُقرأ من جديد.
    /// </summary>
    public sealed class AttendanceDeviceCache
    {
        public sealed record Entry(PushDevice Device, DateTime WrittenAtUtc, string? Ip);

        private static readonly TimeSpan CommandRecheck = TimeSpan.FromMinutes(1);
        private readonly ConcurrentDictionary<string, Entry> _devices = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<int, long> _queuedAt = new();
        private readonly ConcurrentDictionary<int, long> _emptyAt = new();

        public bool TryGet(string serialNumber, out Entry entry) => _devices.TryGetValue(serialNumber, out entry!);
        public void Set(string serialNumber, Entry entry) => _devices[serialNumber] = entry;
        public void Invalidate(string serialNumber) => _devices.TryRemove(serialNumber, out _);

        /// <summary>وُضع للجهاز أمر جديد (بعد حفظه)</summary>
        public void CommandsQueued(int deviceId) => _queuedAt[deviceId] = DateTime.UtcNow.Ticks;

        /// <summary>قد تكون للجهاز أوامر منتظرة: لم يُفحص بعد، أو وُضع له أمر بعد آخر فحص فارغ، أو مرّت دقيقة</summary>
        public bool MaybeHasCommands(int deviceId)
        {
            if (!_emptyAt.TryGetValue(deviceId, out var empty)) return true;
            if (_queuedAt.TryGetValue(deviceId, out var queued) && queued >= empty) return true;
            return DateTime.UtcNow.Ticks - empty > CommandRecheck.Ticks;
        }

        /// <summary>فحص بدأ في checkStartTicks لم يجد أوامر (أمر وُضع بعد بدء الفحص يُبقي العلامة)</summary>
        public void NoCommands(int deviceId, long checkStartTicks) => _emptyAt[deviceId] = checkStartTicks;
    }
}
