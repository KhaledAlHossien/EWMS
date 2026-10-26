using System.Globalization;
using Application.Common;
using Application.DTOs.Response;
using Application.Features.Users;
using Application.Interfaces;
using Domain.Entities;
using Domain.Entities.Attendance;
using Domain.Enums;
using MediatR;
using static Application.Common.AppPermissions;

namespace Application.Features.Attendance
{
    public record GetPeopleImportTemplateQuery : IRequest<byte[]>;
    public record ImportAttendancePeopleCommand(Stream File, bool DryRun) : IRequest<PeopleImportReportDto>;

    /// <summary>
    /// استيراد الأشخاص من Excel (المرحلة الأولى): الموظف الموجود (برقمه الذاتي) يُسند له رقم البصمة والبطاقة (EditAttendanceProfile
    /// وحد منطقته)، والجديد يُضاف موظفاً بلا دخول (CreateUser وحد فرعه) أو عامل مياومة (CreateDailyWorker وحد منطقته).
    /// المعاينة لا تحفظ شيئاً؛ التأكيد يحفظ الأسطر الصالحة فقط، سطراً سطراً.
    /// </summary>
    public class AttendancePeopleImportHandler :
        IRequestHandler<GetPeopleImportTemplateQuery, byte[]>,
        IRequestHandler<ImportAttendancePeopleCommand, PeopleImportReportDto>
    {
        public const int MaxRows = 2000;
        private const string NoLoginKind = "موظف بلا دخول";
        private const string WorkerKind = "عامل مياومة";
        private const string UpdateKind = "تحديث";

        private readonly IAttendancePeopleSpreadsheet _sheet;
        private readonly IAttendancePeopleService _people;
        private readonly IAttendanceSyncService _sync;
        private readonly IUserService _users;
        private readonly IUserPermissionService _permissions;
        private readonly IBranchService _branches;
        private readonly IDepartmentService _departments;
        private readonly IOfficeService _offices;
        private readonly IRoleService _roles;

        public AttendancePeopleImportHandler(IAttendancePeopleSpreadsheet sheet, IAttendancePeopleService people, IAttendanceSyncService sync,
            IUserService users, IUserPermissionService permissions, IBranchService branches, IDepartmentService departments, IOfficeService offices,
            IRoleService roles)
        {
            _sheet = sheet;
            _people = people;
            _sync = sync;
            _users = users;
            _permissions = permissions;
            _branches = branches;
            _departments = departments;
            _offices = offices;
            _roles = roles;
        }

        public Task<byte[]> Handle(GetPeopleImportTemplateQuery request, CancellationToken ct) => Task.FromResult(_sheet.ImportTemplate());

        /// <summary>سطر مفهوم وجاهز للحفظ</summary>
        private sealed record Plan(PeopleSheetRow Row, string Action, User? Existing, User? NewUser, AttendanceProfile Profile);

        public async Task<PeopleImportReportDto> Handle(ImportAttendancePeopleCommand request, CancellationToken ct)
        {
            var viewer = await Viewer.CurrentAsync(_users, _permissions);
            var rows = _sheet.Read(request.File, MaxRows);
            var report = new PeopleImportReportDto { DryRun = request.DryRun, Total = rows.Count };

            var areas = (await _people.AreaNamesAsync()).ToDictionary(a => a.Value.Trim(), a => a.Key);
            var branches = await _branches.GetAllAsync();
            var departments = await _departments.GetAllAsync();
            var offices = await _offices.GetAllAsync();
            var allUsers = await _users.GetAllAsync();   // الكادر (والعمال بالرقم الذاتي يُبحث عنهم من قائمة الأشخاص)
            var manage = AttendanceRules.ManageLimit(viewer);
            var numbersInFile = new HashSet<string>();
            var plans = new List<Plan>();

            foreach (var row in rows)
            {
                var result = new PeopleImportRowDto { Row = row.Row, Name = row.FullName, AttendanceNumber = row.AttendanceNumber };
                report.Rows.Add(result);
                try
                {
                    var number = AttendanceRules.NormalizeNumber(row.AttendanceNumber);
                    var card = AttendanceRules.NormalizeCard(row.CardNumber);
                    if (number != null && !numbersInFile.Add(number)) throw new InvalidOperationException($"رقم البصمة {number} مكرر في الملف");
                    result.AttendanceNumber = number ?? string.Empty;
                    var personalId = UserRules.NormalizePersonalIdNumber(row.PersonalIdNumber);
                    var from = Date(row.ActiveFrom, "بداية العمل");
                    var to = Date(row.ActiveTo, "نهاية العمل");
                    if (from != null && to != null && from > to) throw new InvalidOperationException("بداية العمل بعد نهايته");

                    var existing = personalId == null ? null
                        : allUsers.FirstOrDefault(u => u.PersonalIdNumber == personalId)
                          ?? (await _people.SearchAsync(AttendanceScope.Everything, new PeopleFilter(UserKind.DailyWorker, null, null, null, personalId, null, null), 1, 1))
                              .Items.Where(p => p.PersonalIdNumber == personalId).Select(p => new User { Id = p.UserId, FullName = p.FullName, Kind = p.Kind }).FirstOrDefault();

                    if (number != null && await _people.NumberOwnerAsync(number) is int owner && owner != existing?.Id)
                        throw new InvalidOperationException($"رقم البصمة {number} مسجّل لشخص آخر");
                    if (card != null && await _people.CardOwnerAsync(card) is int cardOwner && cardOwner != existing?.Id)
                        throw new InvalidOperationException($"رقم البطاقة {card} مسجّل لشخص آخر");

                    var profile = new AttendanceProfile { AttendanceNumber = number, CardNumber = card, ActiveFrom = from, ActiveTo = to, UpdatedAt = DateTime.UtcNow };

                    if (existing != null)
                    {
                        // موجود: يُحدَّث رقم البصمة والبطاقة فقط
                        if (number == null && card == null) throw new InvalidOperationException("لا رقم بصمة ولا بطاقة لإسنادهما");
                        if (!viewer.IsSuperAdmin && !viewer.Has(EditAttendanceProfile)) throw new UnauthorizedAccessException("لا تملك تعديل بيانات الحضور");
                        var row0 = await _people.GetRowAsync(existing.Id) ?? throw new KeyNotFoundException("الشخص غير موجود");
                        AttendanceRules.EnsureManage(manage, row0.HomeAreaId);
                        result.Name = row0.FullName;
                        result.Action = UpdateKind;
                        plans.Add(new Plan(row, UpdateKind, existing, null, profile));
                    }
                    else if (row.Kind.Trim() == NoLoginKind)
                    {
                        result.Action = "جديد: " + NoLoginKind;
                        if (string.IsNullOrWhiteSpace(row.FullName)) throw new InvalidOperationException("الاسم مطلوب");
                        if (personalId == null) throw new InvalidOperationException("الرقم الذاتي مطلوب للموظف الرسمي");
                        if (!viewer.IsSuperAdmin && !viewer.Has("CreateUser")) throw new UnauthorizedAccessException("لا تملك إضافة الموظفين");
                        var branch = Find(branches.Select(b => (b.Id, b.Name)), row.Branch, "الفرع");
                        var department = Find(departments.Select(d => (d.Id, d.Name)), row.Department, "القسم");
                        var office = Find(offices.Select(o => (o.Id, o.Name)), row.Office, "المكتب");
                        var place = await UserRules.EnsureUserReferencesAsync(_branches, _departments, _offices, _roles, branch, department, office, null);
                        // حدّ إضافة الموظفين: فرعي، أو الكل لنظرة المؤسسة
                        StructureScope.EnsureIncludes(viewer, place.BranchId, "يمكنك إضافة موظفي فرعك فقط");
                        if (!PhoneRules.IsValid(row.Phone)) throw new InvalidOperationException("رقم الهاتف غير صحيح");
                        plans.Add(new Plan(row, NoLoginKind, null, new User
                        {
                            Kind = UserKind.NoLogin, FullName = row.FullName.Trim(), PersonalIdNumber = personalId, PhoneNumber = PhoneRules.ToStored(row.Phone),
                            BranchId = place.BranchId, DepartmentId = place.DepartmentId, OfficeId = place.OfficeId, IsActive = true, CreatedAt = DateTime.UtcNow
                        }, profile));
                    }
                    else if (row.Kind.Trim() == WorkerKind)
                    {
                        result.Action = "جديد: " + WorkerKind;
                        if (string.IsNullOrWhiteSpace(row.FullName)) throw new InvalidOperationException("الاسم مطلوب");
                        if (!viewer.IsSuperAdmin && !viewer.Has(CreateDailyWorker)) throw new UnauthorizedAccessException("لا تملك إضافة عمال المياومة");
                        if (!areas.TryGetValue(row.Area.Trim(), out var areaId)) throw new InvalidOperationException($"المنطقة «{row.Area}» غير موجودة");
                        AttendanceRules.EnsureManage(manage, areaId, "يمكنك إضافة عمال منطقتك فقط");
                        var branch = Find(branches.Select(b => (b.Id, b.Name)), row.Branch, "الفرع");
                        var department = Find(departments.Select(d => (d.Id, d.Name)), row.Department, "القسم");
                        var office = Find(offices.Select(o => (o.Id, o.Name)), row.Office, "المكتب");
                        var place = await UserRules.EnsureUserReferencesAsync(_branches, _departments, _offices, _roles, branch, department, office, null);
                        if (place.BranchId is int b && branches.First(x => x.Id == b).AttendanceAreaId != areaId)
                            throw new InvalidOperationException("الفرع لا يتبع منطقة عمل العامل");
                        if (!PhoneRules.IsValid(row.Phone)) throw new InvalidOperationException("رقم الهاتف غير صحيح");
                        profile.AreaId = areaId;
                        plans.Add(new Plan(row, WorkerKind, null, new User
                        {
                            Kind = UserKind.DailyWorker, FullName = row.FullName.Trim(), PersonalIdNumber = personalId, PhoneNumber = PhoneRules.ToStored(row.Phone),
                            BranchId = place.BranchId, DepartmentId = place.DepartmentId, OfficeId = place.OfficeId, IsActive = true, CreatedAt = DateTime.UtcNow
                        }, profile));
                    }
                    else if (row.Kind.Trim() == UpdateKind)
                        throw new InvalidOperationException(personalId == null ? "الرقم الذاتي مطلوب للتحديث" : $"لا يوجد شخص بالرقم الذاتي {personalId}");
                    else
                        throw new InvalidOperationException($"النوع «{row.Kind}» غير معروف: {NoLoginKind} أو {WorkerKind} أو {UpdateKind}");

                    result.Ok = true;
                }
                catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException or UnauthorizedAccessException)
                {
                    result.Ok = false;
                    result.Message = ex.Message;
                }
            }
            report.Valid = report.Rows.Count(r => r.Ok);
            if (request.DryRun) return report;

            var touched = new List<int>();
            foreach (var plan in plans)
            {
                var result = report.Rows.First(r => r.Row == plan.Row.Row);
                try
                {
                    if (plan.Existing != null)
                    {
                        var profile = await _people.GetProfileAsync(plan.Existing.Id);
                        var isNew = profile == null;
                        profile ??= new AttendanceProfile { UserId = plan.Existing.Id };
                        profile.AttendanceNumber = plan.Profile.AttendanceNumber ?? profile.AttendanceNumber;
                        profile.CardNumber = plan.Profile.CardNumber ?? profile.CardNumber;
                        profile.UpdatedAt = DateTime.UtcNow;
                        await _people.SaveProfileAsync(profile, isNew, profile.ExtraAreas.Select(a => a.AreaId).ToList());
                        touched.Add(plan.Existing.Id);
                    }
                    else
                    {
                        await _people.AddPersonAsync(plan.NewUser!, plan.Profile, []);
                        touched.Add(plan.NewUser!.Id);
                    }
                    report.Imported++;
                }
                catch (InvalidOperationException ex)
                {
                    result.Ok = false;
                    result.Message = ex.Message;
                }
            }
            if (touched.Count > 0) await _sync.MarkUsersAsync(touched);
            report.Valid = report.Rows.Count(r => r.Ok);
            return report;
        }

        private static int? Find(IEnumerable<(int Id, string Name)> items, string name, string label)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            var match = items.FirstOrDefault(i => i.Name.Trim() == name.Trim());
            return match.Id > 0 ? match.Id : throw new InvalidOperationException($"{label} «{name}» غير موجود");
        }

        private static DateTime? Date(string text, string label)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            return DateTime.TryParseExact(text.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
                ? d : throw new InvalidOperationException($"{label}: التاريخ بصيغة 2026-11-01");
        }
    }
}
