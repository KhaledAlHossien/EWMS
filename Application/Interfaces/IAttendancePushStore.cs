using Application.Features.Attendance.Push;
using Domain.Entities.Attendance;

namespace Application.Interfaces
{
    /// <summary>لقطة جهاز لطلبات البروتوكول (تُخزَّن مؤقتاً في الذاكرة: الاستطلاع كل بضع ثوانٍ لا يقرأ القاعدة كل مرة)</summary>
    public sealed record PushDevice(int Id, string SerialNumber, string Name, AttendanceDeviceState State, int? AreaId,
        string AttLogStamp, string OperLogStamp, string PhotoStamp)
    {
        public bool Accepted => State == AttendanceDeviceState.Approved;
    }

    /// <summary>نتيجة اتصال جهاز: الجهاز، وهل ظهر الآن لأول مرة (أو عاد بعد إزالته)، وعنوانه السابق إن تغيّر</summary>
    public sealed record PushContact(PushDevice Device, bool IsNew, string? PreviousIp, string? Ip);

    /// <summary>
    /// تخزين ما يصل من الأجهزة (Push/ADMS). كل الدوال آمنة للتكرار: البصمة المكررة لا تُحفظ مرتين، والقالب نفسه لا يُعاد حفظه.
    /// </summary>
    public interface IAttendancePushStore
    {
        /// <summary>
        /// يسجّل اتصال الجهاز: جديد = «بانتظار الموافقة»، والمُزال يعود بانتظارها. يحدّث آخر اتصال وعنوانه (كتابة في القاعدة
        /// مرة في الدقيقة على الأكثر، أو فوراً إن تغيّر العنوان أو وصلت معلومات جديدة).
        /// </summary>
        Task<PushContact> ContactAsync(string serialNumber, string? ip, PushDeviceInfo? info, CancellationToken ct = default);

        /// <summary>يحفظ البصمات الجديدة فقط ويعيد عددها؛ صاحب كل رقم يُحدَّد من بيانات الحضور وقت الاستلام</summary>
        Task<int> SavePunchesAsync(PushDevice device, IReadOnlyList<PushPunch> punches, AttendancePunchSource source, string? stamp, CancellationToken ct = default);

        /// <summary>
        /// مستخدمو الجهاز وقوالبهم وصور تسجيلهم: القالب الجديد أو المتغيّر لشخص معروف يُحفظ مشفّراً، وتُعلَّم أجهزة مناطقه
        /// «تحتاج مزامنة» ليصلها. يعيد أرقام الأشخاص الذين تغيّرت قوالبهم.
        /// </summary>
        Task<int> SaveOperationsAsync(PushDevice device, PushOperations operations, string? stamp, CancellationToken ct = default);

        /// <summary>صورة لحظة البصم (تُحفظ مرة واحدة لكل بصمة)</summary>
        Task SavePunchPhotoAsync(PushDevice device, PushPhoto photo, string? stamp, CancellationToken ct = default);

        /// <summary>أوامر الجهاز المنتظرة بالترتيب (تُعلَّم «أُرسل»)</summary>
        Task<List<(long Id, string Text)>> TakeCommandsAsync(PushDevice device, int max, CancellationToken ct = default);

        Task CompleteCommandsAsync(PushDevice device, IReadOnlyList<PushCommandResult> results, CancellationToken ct = default);
    }
}
