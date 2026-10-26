using Application.Common;
using Application.Interfaces;
using Domain.Entities;
using Domain.Entities.Attendance;
using Domain.Enums;
using static Application.Common.AppPermissions;

namespace Application.Features.Attendance
{
    /// <summary>
    /// حدود صلاحيات الحضور (قرارات المستخدم 2026-10-26) — مصدر واحد لكل الـ handlers:
    /// - «منطقتي» = منطقة الدوام للفرع الذي يتبع له المستخدم (Branch.AttendanceAreaId)؛ من لا منطقة لفرعه لا منطقة له.
    /// - منطقة الشخص = منطقة عامل المياومة المحددة له، وإلا منطقة فرعه.
    /// - صلاحيات العرض تتجمّع (من يملك قسمي وفرعي يرى الاثنين)، وViewOrganizationAttendance (ومدير النظام) = الكل.
    /// - إدارة الأشخاص (بيانات الحضور والعمال): منطقتي، أو كل المناطق مع ViewOrganizationAttendance.
    /// - الأجهزة: عرض وأوامر لمنطقتي أو لكل الأجهزة حسب الصلاحية؛ القبول والتعديل والإزالة لكل الأجهزة (المركز).
    /// </summary>
    public static class AttendanceRules
    {
        /// <summary>رقم البصمة: أرقام فقط حتى 9 خانات (حد الأجهزة الشائع)</summary>
        public const int AttendanceNumberMaxDigits = 9;
        public const int CardNumberMaxLength = 20;
        public const int MaxExtraAreas = 10;

        /// <summary>الوقت المحلي (توقيت الأجهزة والبصمات)</summary>
        public static DateTime LocalNow(IAttendanceSettings settings) => DateTime.UtcNow.AddHours(settings.TimeZoneHours);

        public static int? MyAreaOf(Viewer viewer) => viewer.User.Branch?.AttendanceAreaId;

        private static bool SeesAll(Viewer viewer) => viewer.IsSuperAdmin || viewer.Has(ViewOrganizationAttendance);

        /// <summary>حدّ عرض الحضور والبصمات: اتحاد حدود صلاحيات العرض التي يملكها المستخدم</summary>
        public static AttendanceScope ViewScope(Viewer viewer)
        {
            if (SeesAll(viewer)) return AttendanceScope.Everything;
            return new AttendanceScope(false,
                viewer.Has(ViewAreaAttendance) ? MyAreaOf(viewer) : null,
                viewer.Has(ViewBranchAttendance) ? viewer.User.BranchId : null,
                viewer.Has(ViewDepartmentAttendance) ? viewer.User.DepartmentId : null,
                viewer.Has(ViewMyAttendance) ? viewer.Id : null);
        }

        /// <summary>حدّ إدارة الأشخاص (بيانات الحضور، العمال، الاستيراد): منطقتي أو الكل</summary>
        public static AreaLimit ManageLimit(Viewer viewer) =>
            SeesAll(viewer) ? AreaLimit.Everything
            : MyAreaOf(viewer) is int area ? new AreaLimit(false, area) : AreaLimit.Nothing;

        /// <summary>حدّ عرض الأجهزة: كل الأجهزة لمن يديرها أو يرى كلها، وأجهزة منطقتي لمراقبها</summary>
        public static AreaLimit DeviceViewLimit(Viewer viewer)
        {
            if (viewer.IsSuperAdmin || viewer.Has(ViewAttendanceDevices) || viewer.Has(SendAttendanceDeviceCommand)
                || viewer.Has(ApproveAttendanceDevice) || viewer.Has(EditAttendanceDevice) || viewer.Has(DeleteAttendanceDevice))
                return AreaLimit.Everything;
            if (viewer.Has(ViewAreaAttendanceDevices) || viewer.Has(SendAreaAttendanceDeviceCommand))
                return MyAreaOf(viewer) is int area ? new AreaLimit(false, area) : AreaLimit.Nothing;
            return AreaLimit.Nothing;
        }

        /// <summary>حدّ إرسال الأوامر للأجهزة</summary>
        public static AreaLimit CommandLimit(Viewer viewer)
        {
            if (viewer.IsSuperAdmin || viewer.Has(SendAttendanceDeviceCommand)) return AreaLimit.Everything;
            if (viewer.Has(SendAreaAttendanceDeviceCommand) && MyAreaOf(viewer) is int area) return new AreaLimit(false, area);
            return AreaLimit.Nothing;
        }

        /// <summary>منطقة الشخص: منطقة العامل المحددة، وإلا منطقة فرعه</summary>
        public static int? HomeAreaOf(User user, AttendanceProfile? profile) =>
            profile?.AreaId ?? user.Branch?.AttendanceAreaId;

        public static void EnsureManage(AreaLimit limit, int? personArea, string message = "هذا الشخص يتبع منطقة دوام أخرى")
        {
            if (!limit.Includes(personArea)) throw new UnauthorizedAccessException(message);
        }

        /// <summary>رقم البصمة: أرقام فقط بلا أصفار بادئة (الجهاز يعامل 0012 و12 رقماً واحداً)، والفارغ = null</summary>
        public static string? NormalizeNumber(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var digits = value.Trim();
            if (digits.Length > AttendanceNumberMaxDigits || !digits.All(char.IsAsciiDigit))
                throw new InvalidOperationException($"رقم البصمة أرقام فقط، حتى {AttendanceNumberMaxDigits} خانات");
            digits = digits.TrimStart('0');
            if (digits.Length == 0) throw new InvalidOperationException("رقم البصمة لا يكون صفراً");
            return digits;
        }

        /// <summary>رقم كما وصل من الجهاز بصيغة رقم البصمة المحفوظة (بلا أصفار بادئة) لمطابقته بصاحبه</summary>
        public static string PinKey(string pin)
        {
            var trimmed = pin.Trim();
            if (trimmed.Length == 0 || !trimmed.All(char.IsAsciiDigit)) return trimmed;
            var digits = trimmed.TrimStart('0');
            return digits.Length == 0 ? trimmed : digits;
        }

        /// <summary>طريقة التحقق في سطر البصمة: مفتاح أيقونة الواجهة واسمها</summary>
        public static (string Key, string Name) Verify(int code) => code switch
        {
            1 => ("finger", "إصبع"),
            15 => ("face", "وجه"),
            2 or 4 => ("card", "بطاقة"),
            0 or 3 => ("password", "كلمة سر"),
            25 => ("palm", "كف"),
            _ => ("other", "أخرى")
        };

        public static string? NormalizeCard(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var card = value.Trim();
            if (card.Length > CardNumberMaxLength || !card.All(char.IsAsciiDigit))
                throw new InvalidOperationException($"رقم البطاقة أرقام فقط، حتى {CardNumberMaxLength} خانة");
            return card;
        }

        public static string KindName(UserKind kind) => kind switch
        {
            UserKind.Account => "حساب نظام",
            UserKind.NoLogin => "موظف بلا دخول",
            UserKind.DailyWorker => "عامل مياومة",
            _ => ""
        };

        public static string DeviceStateName(AttendanceDeviceState state) => state switch
        {
            AttendanceDeviceState.Pending => "بانتظار الموافقة",
            AttendanceDeviceState.Approved => "مقبول",
            AttendanceDeviceState.Rejected => "مرفوض",
            AttendanceDeviceState.Removed => "مُزال",
            _ => ""
        };
    }
}
