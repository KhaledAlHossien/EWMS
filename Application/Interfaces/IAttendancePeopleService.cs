using Domain.Entities;
using Domain.Entities.Attendance;
using Domain.Enums;

namespace Application.Interfaces
{
    /// <summary>فلاتر قائمة الأشخاص (كلها ضمن حد المستخدم). Missing: number / face / finger / templates</summary>
    public sealed record PeopleFilter(UserKind? Kind, int? AreaId, int? BranchId, int? DepartmentId, string? Query, string? Missing, bool? Active);

    /// <summary>شخص مع بيانات حضوره وما سُجّل له من قوالب ومن على الأجهزة</summary>
    public sealed record PersonRow(
        int UserId, string FullName, UserKind Kind, bool IsActive, string? PersonalIdNumber, string? PhoneNumber,
        int? BranchId, string? BranchName, int? DepartmentId, string? DepartmentName, int? OfficeId, string? OfficeName,
        string? AttendanceNumber, string? CardNumber, bool IsDeviceAdmin, int? WorkerAreaId, DateTime? ActiveFrom, DateTime? ActiveTo,
        int? HomeAreaId, List<int> ExtraAreaIds, int Faces, int Fingers, bool HasBioPhoto, int OnDevices);

    public interface IAttendancePeopleService
    {
        /// <summary>الأشخاص ضمن الحد (الكل، أو أشخاص منطقة/فرع/قسم، أو نفسي) والفلاتر، بالاسم</summary>
        Task<(List<PersonRow> Items, int Total)> SearchAsync(AttendanceScope scope, PeopleFilter filter, int page, int pageSize);
        Task<PersonRow?> GetRowAsync(int userId);
        /// <summary>متتبَّع مع فرعه وقسمه ومكتبه</summary>
        Task<User?> GetUserAsync(int userId);
        /// <summary>متتبَّع مع المناطق الإضافية (null = لم تُسجَّل له بيانات حضور بعد)</summary>
        Task<AttendanceProfile?> GetProfileAsync(int userId);
        /// <summary>صاحب رقم البصمة أو رقم البطاقة إن وُجد</summary>
        Task<int?> NumberOwnerAsync(string number);
        Task<int?> CardOwnerAsync(string card);
        /// <summary>يحفظ بيانات الحضور (يضيفها إن كانت جديدة) ويضع المناطق الإضافية بالضبط</summary>
        Task SaveProfileAsync(AttendanceProfile profile, bool isNew, IReadOnlyCollection<int> extraAreaIds);
        /// <summary>شخص جديد (عامل مياومة أو موظف بلا دخول من الاستيراد): المستخدم وبيانات حضوره في حفظ واحد</summary>
        Task AddPersonAsync(User user, AttendanceProfile profile, IReadOnlyCollection<int> extraAreaIds);
        Task SaveChangesAsync();
        Task DeleteUserAsync(User user);
        /// <summary>سبب يمنع حذف الشخص (بصمات، أو آثار في سجل الأجهزة) أو null</summary>
        Task<string?> DeleteBlockerAsync(int userId);
        /// <summary>يربط البصمات التي وصلت بهذا الرقم ولا صاحب لها بهذا الشخص، ويعيد عددها</summary>
        Task<int> LinkUnknownPunchesAsync(string number, int userId);
        /// <summary>
        /// يحذف قوالب الشخص وصورة تسجيله (لإعادة التسجيل)، ويُحذف من الأجهزة التي وضعناه عليها ثم يُعاد وضعه بلا قوالب.
        /// </summary>
        Task ClearBiometricsAsync(int userId);
        Task<Dictionary<int, string>> AreaNamesAsync();
        /// <summary>عدد الأجهزة المقبولة في كل منطقة</summary>
        Task<Dictionary<int, int>> DevicesPerAreaAsync();
    }

    /// <summary>سطر من ملف استيراد الأشخاص (القيم نصوص كما في الملف)</summary>
    public sealed class PeopleSheetRow
    {
        public int Row { get; set; }
        public string Kind { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string PersonalIdNumber { get; set; } = string.Empty;
        public string AttendanceNumber { get; set; } = string.Empty;
        public string CardNumber { get; set; } = string.Empty;
        public string Area { get; set; } = string.Empty;
        public string Branch { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string Office { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string ActiveFrom { get; set; } = string.Empty;
        public string ActiveTo { get; set; } = string.Empty;
    }

    public interface IAttendancePeopleSpreadsheet
    {
        /// <summary>قالب الاستيراد: الأعمدة مع سطر مثال وورقة تعليمات</summary>
        byte[] ImportTemplate();
        /// <summary>يقرأ أسطر الملف (يتخطى الفارغة وسطر المثال) — InvalidOperationException برسالة عربية إن لم يُقرأ</summary>
        List<PeopleSheetRow> Read(Stream file, int maxRows);
    }
}
