using Application.Interfaces;
using Domain.Entities;
using Domain.Entities.Attendance;
using Domain.Enums;
using Infrastructure.Attendance;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    public class AttendancePeopleService : IAttendancePeopleService
    {
        private readonly DataContext _context;
        private readonly AttendanceDeviceCache _cache;

        public AttendancePeopleService(DataContext context, AttendanceDeviceCache cache)
        {
            _context = context;
            _cache = cache;
        }

        /// <summary>الأشخاص ضمن حدّ العرض: اتحاد ما يملكه المستخدم (المنطقة = منطقة الشخص أو إحدى مناطقه الإضافية)</summary>
        private IQueryable<User> InScope(AttendanceScope scope)
        {
            var users = _context.Users.AsNoTracking();
            if (scope.All) return users;
            if (scope.IsEmpty) return users.Where(u => false);
            var (self, department, branch, area) = (scope.SelfId, scope.DepartmentId, scope.BranchId, scope.AreaId);
            return users.Where(u =>
                (self != null && u.Id == self)
                || (department != null && u.DepartmentId == department)
                || (branch != null && u.BranchId == branch)
                || (area != null && ((_context.AttendanceProfiles.Where(p => p.UserId == u.Id).Select(p => p.AreaId).FirstOrDefault()
                        ?? u.Branch!.AttendanceAreaId) == area
                    || _context.AttendanceProfileAreas.Any(a => a.UserId == u.Id && a.AreaId == area))));
        }

        /// <summary>صف القائمة كما يُقرأ من القاعدة (تهيئة أعضاء: تترجمها EF مع القائمة الفرعية)، ثم يُحوَّل إلى PersonRow</summary>
        private sealed class Projection
        {
            public int UserId { get; set; }
            public string FullName { get; set; } = string.Empty;
            public UserKind Kind { get; set; }
            public bool IsActive { get; set; }
            public string? PersonalIdNumber { get; set; }
            public string? PhoneNumber { get; set; }
            public int? BranchId { get; set; }
            public string? BranchName { get; set; }
            public int? BranchAreaId { get; set; }
            public int? DepartmentId { get; set; }
            public string? DepartmentName { get; set; }
            public int? OfficeId { get; set; }
            public string? OfficeName { get; set; }
            public string? AttendanceNumber { get; set; }
            public string? CardNumber { get; set; }
            public bool IsDeviceAdmin { get; set; }
            public int? WorkerAreaId { get; set; }
            public DateTime? ActiveFrom { get; set; }
            public DateTime? ActiveTo { get; set; }
            public List<int> ExtraAreaIds { get; set; } = [];
            public int Faces { get; set; }
            public int Fingers { get; set; }
            public bool HasBioPhoto { get; set; }
            public int OnDevices { get; set; }

            public PersonRow ToRow() => new(UserId, FullName, Kind, IsActive, PersonalIdNumber, PhoneNumber,
                BranchId, BranchName, DepartmentId, DepartmentName, OfficeId, OfficeName,
                AttendanceNumber, CardNumber, IsDeviceAdmin, WorkerAreaId, ActiveFrom, ActiveTo,
                WorkerAreaId ?? BranchAreaId, ExtraAreaIds, Faces, Fingers, HasBioPhoto, OnDevices);
        }

        private IQueryable<Projection> Rows(IQueryable<User> users) =>
            users.Select(u => new Projection
            {
                UserId = u.Id, FullName = u.FullName, Kind = u.Kind, IsActive = u.IsActive,
                PersonalIdNumber = u.PersonalIdNumber, PhoneNumber = u.PhoneNumber,
                BranchId = u.BranchId, BranchName = u.Branch != null ? u.Branch.Name : null, BranchAreaId = u.Branch != null ? u.Branch.AttendanceAreaId : null,
                DepartmentId = u.DepartmentId, DepartmentName = u.Department != null ? u.Department.Name : null,
                OfficeId = u.OfficeId, OfficeName = u.Office != null ? u.Office.Name : null,
                AttendanceNumber = _context.AttendanceProfiles.Where(p => p.UserId == u.Id).Select(p => p.AttendanceNumber).FirstOrDefault(),
                CardNumber = _context.AttendanceProfiles.Where(p => p.UserId == u.Id).Select(p => p.CardNumber).FirstOrDefault(),
                IsDeviceAdmin = _context.AttendanceProfiles.Any(p => p.UserId == u.Id && p.IsDeviceAdmin),
                WorkerAreaId = _context.AttendanceProfiles.Where(p => p.UserId == u.Id).Select(p => p.AreaId).FirstOrDefault(),
                ActiveFrom = _context.AttendanceProfiles.Where(p => p.UserId == u.Id).Select(p => p.ActiveFrom).FirstOrDefault(),
                ActiveTo = _context.AttendanceProfiles.Where(p => p.UserId == u.Id).Select(p => p.ActiveTo).FirstOrDefault(),
                ExtraAreaIds = _context.AttendanceProfileAreas.Where(a => a.UserId == u.Id).Select(a => a.AreaId).ToList(),
                Faces = _context.AttendanceBiometrics.Count(b => b.UserId == u.Id && (b.Type == AttendanceBiometricType.VisibleFace || b.Type == AttendanceBiometricType.Face)),
                Fingers = _context.AttendanceBiometrics.Count(b => b.UserId == u.Id && b.Type == AttendanceBiometricType.Finger),
                HasBioPhoto = _context.AttendanceBioPhotos.Any(p => p.UserId == u.Id),
                OnDevices = _context.AttendanceDeviceUsers.Count(d => d.UserId == u.Id && d.Managed)
            });

        public async Task<(List<PersonRow> Items, int Total)> SearchAsync(AttendanceScope scope, PeopleFilter f, int page, int pageSize)
        {
            var users = InScope(scope);
            if (f.Kind != null) users = users.Where(u => u.Kind == f.Kind);
            if (f.Active != null) users = users.Where(u => u.IsActive == f.Active);
            if (f.BranchId != null) users = users.Where(u => u.BranchId == f.BranchId);
            if (f.DepartmentId != null) users = users.Where(u => u.DepartmentId == f.DepartmentId);
            if (f.AreaId is int area)
                users = users.Where(u => (_context.AttendanceProfiles.Where(p => p.UserId == u.Id).Select(p => p.AreaId).FirstOrDefault()
                        ?? u.Branch!.AttendanceAreaId) == area
                    || _context.AttendanceProfileAreas.Any(a => a.UserId == u.Id && a.AreaId == area));
            if (!string.IsNullOrWhiteSpace(f.Query))
            {
                var q = f.Query.Trim();
                var id = q.ToUpperInvariant();
                users = users.Where(u => u.FullName.Contains(q) || u.PersonalIdNumber == id
                    || _context.AttendanceProfiles.Any(p => p.UserId == u.Id && (p.AttendanceNumber == q || p.CardNumber == q)));
            }
            users = f.Missing switch
            {
                "number" => users.Where(u => !_context.AttendanceProfiles.Any(p => p.UserId == u.Id && p.AttendanceNumber != null)),
                "face" => users.Where(u => !_context.AttendanceBiometrics.Any(b => b.UserId == u.Id
                    && (b.Type == AttendanceBiometricType.VisibleFace || b.Type == AttendanceBiometricType.Face))),
                "finger" => users.Where(u => !_context.AttendanceBiometrics.Any(b => b.UserId == u.Id && b.Type == AttendanceBiometricType.Finger)),
                "templates" => users.Where(u => !_context.AttendanceBiometrics.Any(b => b.UserId == u.Id)),
                _ => users
            };

            var total = await users.CountAsync();
            var items = await Rows(users.OrderBy(u => u.FullName).ThenBy(u => u.Id).Skip((page - 1) * pageSize).Take(pageSize)).ToListAsync();
            return (items.Select(i => i.ToRow()).ToList(), total);
        }

        public async Task<PersonRow?> GetRowAsync(int userId) =>
            (await Rows(_context.Users.AsNoTracking().Where(u => u.Id == userId)).FirstOrDefaultAsync())?.ToRow();

        public Task<User?> GetUserAsync(int userId) =>
            _context.Users.Include(u => u.Branch).Include(u => u.Department).Include(u => u.Office).FirstOrDefaultAsync(u => u.Id == userId);

        public Task<AttendanceProfile?> GetProfileAsync(int userId) =>
            _context.AttendanceProfiles.Include(p => p.ExtraAreas).FirstOrDefaultAsync(p => p.UserId == userId);

        public Task<int?> NumberOwnerAsync(string number) =>
            _context.AttendanceProfiles.Where(p => p.AttendanceNumber == number).Select(p => (int?)p.UserId).FirstOrDefaultAsync();

        public Task<int?> CardOwnerAsync(string card) =>
            _context.AttendanceProfiles.Where(p => p.CardNumber == card).Select(p => (int?)p.UserId).FirstOrDefaultAsync();

        private void SetExtraAreas(AttendanceProfile profile, IReadOnlyCollection<int> extraAreaIds)
        {
            foreach (var area in profile.ExtraAreas.Where(a => !extraAreaIds.Contains(a.AreaId)).ToList())
                profile.ExtraAreas.Remove(area);
            foreach (var id in extraAreaIds.Where(id => profile.ExtraAreas.All(a => a.AreaId != id)))
                profile.ExtraAreas.Add(new AttendanceProfileArea { UserId = profile.UserId, AreaId = id });
        }

        public async Task SaveProfileAsync(AttendanceProfile profile, bool isNew, IReadOnlyCollection<int> extraAreaIds)
        {
            if (isNew) _context.AttendanceProfiles.Add(profile);
            SetExtraAreas(profile, extraAreaIds);
            await DeviceInventorySave.SaveAsync(_context, "رقم البصمة أو رقم البطاقة مسجّل لشخص آخر");
        }

        public async Task AddPersonAsync(User user, AttendanceProfile profile, IReadOnlyCollection<int> extraAreaIds)
        {
            profile.User = user;
            _context.Users.Add(user);
            _context.AttendanceProfiles.Add(profile);
            foreach (var id in extraAreaIds) profile.ExtraAreas.Add(new AttendanceProfileArea { Profile = profile, AreaId = id });
            await DeviceInventorySave.SaveAsync(_context, "رقم البصمة أو رقم البطاقة أو الرقم الذاتي مسجّل لشخص آخر");
        }

        public Task SaveChangesAsync() => DeviceInventorySave.SaveAsync(_context, "رقم البصمة أو رقم البطاقة أو الرقم الذاتي مسجّل لشخص آخر");

        public async Task DeleteUserAsync(User user)
        {
            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
        }

        public async Task<string?> DeleteBlockerAsync(int userId)
        {
            if (await _context.AttendancePunches.AnyAsync(p => p.UserId == userId))
                return "لهذا الشخص بصمات مسجّلة — عطّله (أو حدّد نهاية عمله) بدل حذفه، فتبقى بصماته في السجل";
            if (await _context.AttendanceDeviceLogs.AnyAsync(l => l.UserId == userId)
                || await _context.AttendanceDeviceCommands.AnyAsync(c => c.CreatedByUserId == userId)
                || await _context.AttendanceDevices.AnyAsync(d => d.ApprovedByUserId == userId))
                return "لهذا الموظف آثار في سجل أجهزة البصمة — عطّل حسابه بدلاً من حذفه";
            if (await _context.AttendanceCorrections.AnyAsync(c => c.UserId == userId || c.CreatedByUserId == userId || c.CancelledByUserId == userId))
                return "لهذا الشخص تصحيحات حضور (له أو أجراها) — عطّله بدلاً من حذفه";
            if (await _context.AttendanceSchedules.AnyAsync(s => s.CreatedByUserId == userId || s.UpdatedByUserId == userId)
                || await _context.AttendanceTemporarySchedules.AnyAsync(s => s.CreatedByUserId == userId))
                return "لهذا الموظف إسنادات ورديات أجراها — عطّل حسابه بدلاً من حذفه";
            return null;
        }

        public Task<int> LinkUnknownPunchesAsync(string number, int userId) =>
            _context.AttendancePunches.Where(p => p.UserId == null && p.Pin == number)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.UserId, userId));

        public async Task ClearBiometricsAsync(int userId)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            await _context.AttendanceBiometrics.Where(b => b.UserId == userId).ExecuteDeleteAsync();
            await _context.AttendanceBioPhotos.Where(p => p.UserId == userId).ExecuteDeleteAsync();

            // يُحذف من كل جهاز وضعناه عليه (فتُمحى قوالبه هناك) ثم تعيد المزامنة وضعه بلا قوالب
            var rows = await _context.AttendanceDeviceUsers.Where(u => u.UserId == userId && u.Managed).ToListAsync();
            var now = DateTime.UtcNow;
            foreach (var row in rows)
            {
                _context.AttendanceDeviceCommands.Add(new AttendanceDeviceCommand
                {
                    DeviceId = row.DeviceId, Kind = AttendanceCommandKind.DeleteUser, Text = Application.Features.Attendance.Push.PushProtocol.DeleteUser(row.Pin),
                    Summary = $"حذف قوالب {row.Name} ({row.Pin}) لإعادة التسجيل", UserId = userId, CreatedAt = now
                });
                row.SyncedHash = null;
                row.BiometricHash = null;
            }
            await _context.SaveChangesAsync();
            var deviceIds = rows.Select(r => r.DeviceId).Distinct().ToList();
            await _context.AttendanceDevices.Where(d => deviceIds.Contains(d.Id)).ExecuteUpdateAsync(s => s.SetProperty(d => d.NeedsSync, true));
            await transaction.CommitAsync();
            foreach (var id in deviceIds) _cache.CommandsQueued(id);
        }

        public Task<Dictionary<int, string>> AreaNamesAsync() =>
            _context.AttendanceAreas.AsNoTracking().ToDictionaryAsync(a => a.Id, a => a.Name);

        public async Task<Dictionary<int, int>> DevicesPerAreaAsync() =>
            await _context.AttendanceDevices.AsNoTracking()
                .Where(d => d.State == AttendanceDeviceState.Approved && d.AreaId != null)
                .GroupBy(d => d.AreaId!.Value).Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.Key, g => g.Count);
    }
}
