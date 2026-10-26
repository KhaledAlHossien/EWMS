namespace Application.DTOs.Response
{
    public class IdNameDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class AttendanceAreaDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<IdNameDto> Branches { get; set; } = [];
        /// <summary>الأجهزة المقبولة في المنطقة، والمتصل منها الآن</summary>
        public int Devices { get; set; }
        public int OnlineDevices { get; set; }
        /// <summary>الأشخاص الفعّالون على أجهزة المنطقة (لهم رقم بصمة)</summary>
        public int People { get; set; }
        public byte[] RowVersion { get; set; } = [];
    }

    public class AttendanceDeviceDto
    {
        public int Id { get; set; }
        public string SerialNumber { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        /// <summary>1 بانتظار الموافقة، 2 مقبول، 3 مرفوض، 4 مُزال</summary>
        public int State { get; set; }
        public string StateName { get; set; } = string.Empty;
        public int? AreaId { get; set; }
        public string AreaName { get; set; } = string.Empty;
        public string? Model { get; set; }
        public string? Firmware { get; set; }
        public string? PushVersion { get; set; }
        public string? FingerAlgorithm { get; set; }
        public string? FaceAlgorithm { get; set; }
        public string? IpAddress { get; set; }
        /// <summary>كما يبلّغ بها الجهاز</summary>
        public int? UserCount { get; set; }
        public int? FingerCount { get; set; }
        public int? FaceCount { get; set; }
        public int? PunchCount { get; set; }
        /// <summary>من وضعناهم نحن على الجهاز</summary>
        public int ManagedUsers { get; set; }
        public int PendingCommands { get; set; }
        public bool NeedsSync { get; set; }
        public DateTime FirstSeenAt { get; set; }
        public DateTime? LastSeenAt { get; set; }
        /// <summary>اتصل خلال Attendance:OfflineMinutes الأخيرة</summary>
        public bool Online { get; set; }
        public string Notes { get; set; } = string.Empty;
        public string? ApprovedByName { get; set; }
        public DateTime? ApprovedAt { get; set; }
        // ما يستطيعه المستخدم الحالي على هذا الجهاز
        public bool CanCommand { get; set; }
        public bool CanApprove { get; set; }
        public bool CanEdit { get; set; }
        public bool CanRemove { get; set; }
    }

    public class AttendanceDeviceCommandDto
    {
        public long Id { get; set; }
        public int Kind { get; set; }
        public string Summary { get; set; } = string.Empty;
        public int State { get; set; }
        public string StateName { get; set; } = string.Empty;
        public int? ReturnCode { get; set; }
        /// <summary>فارغ = الخدمة الخلفية (المزامنة التلقائية)</summary>
        public string? CreatedByName { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? SentAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }

    public class AttendanceDeviceLogDto
    {
        public long Id { get; set; }
        public int Event { get; set; }
        public string EventName { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public string? UserName { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>شخص على الجهاز: من وضعناه (Managed) أو من وُجد عليه وليس عندنا (UserId فارغ)</summary>
    public class AttendanceDeviceUserDto
    {
        public string Pin { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int? UserId { get; set; }
        public string? PersonName { get; set; }
        public bool Managed { get; set; }
        public DateTime? SyncedAt { get; set; }
    }

    public class AttendancePunchDto
    {
        public long Id { get; set; }
        public DateTime PunchTime { get; set; }
        public string Pin { get; set; } = string.Empty;
        public int? UserId { get; set; }
        public string? PersonName { get; set; }
        public int? PersonKind { get; set; }
        public string? DepartmentName { get; set; }
        public int DeviceId { get; set; }
        public string DeviceName { get; set; } = string.Empty;
        public string AreaName { get; set; } = string.Empty;
        public int Status { get; set; }
        /// <summary>finger / face / card / password / palm / other</summary>
        public string Verify { get; set; } = string.Empty;
        public string VerifyName { get; set; } = string.Empty;
        public int Source { get; set; }
        public DateTime ReceivedAt { get; set; }
        /// <summary>لها صورة لحظة البصم ويملك المستخدم عرضها</summary>
        public bool HasPhoto { get; set; }
    }

    /// <summary>رقم بصمة يصل من الأجهزة ولا صاحب له في النظام</summary>
    public class UnknownPinDto
    {
        public string Pin { get; set; } = string.Empty;
        public int Punches { get; set; }
        public DateTime FirstAt { get; set; }
        public DateTime LastAt { get; set; }
        public string Devices { get; set; } = string.Empty;
    }

    /// <summary>فرع مع منطقته الحالية (لاختيار فروع المنطقة)</summary>
    public class AreaBranchOptionDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int? AreaId { get; set; }
        public string AreaName { get; set; } = string.Empty;
    }
}
