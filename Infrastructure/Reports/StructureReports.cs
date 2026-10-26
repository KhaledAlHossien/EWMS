using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using Infrastructure.Persistence.Data;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using static Application.Features.Reports.ReportFilters;
using Ct = Application.DTOs.Response.ReportColumnType;

namespace Infrastructure.Reports
{
    /// <summary>الموظفون — حدّ StructureScope (فرعي، والمؤسسة كلها لنظرة المؤسسة)</summary>
    public class UsersReport : ReportBase
    {
        private const string Office = "office", Role = "role", Status = "status", Name = "name";

        public UsersReport(DataContext db) : base(db) { }

        public override string Key => "users";
        public override string Title => "الموظفون";
        public override string Group => "الموظفون والهيكل";
        public override string Description => "الموظفون حسب الفرع والقسم والمكتب: الدور والهاتف والرقم الذاتي والحالة";
        public override string Permission => "ExportUsersReport";
        public override bool Landscape => true;

        public override async Task<List<ReportFilterDto>> GetFiltersAsync(Viewer viewer, CancellationToken ct)
        {
            var limit = StructureScope.BranchOf(viewer);
            if (limit == 0) return [];

            var filters = await UnitFiltersAsync(limit == null, limit, ct);
            var offices = await Db.Offices.AsNoTracking()
                .Where(o => limit == null || o.Department.BranchId == limit)
                .OrderBy(o => o.Name)
                .Select(o => new ReportOptionDto(o.Id.ToString(), o.Name + " — " + o.Department.Name, o.DepartmentId.ToString()))
                .ToListAsync(ct);
            filters.Add(Select(Office, "المكتب", offices, dependsOn: Department));
            // الأدوار التي يحملها موظفو حدّي فقط (لا أدوار الفروع الأخرى)، وكلها لنظرة المؤسسة
            var roles = limit == null
                ? await Db.Roles.AsNoTracking().OrderBy(r => r.Name).Select(r => new ReportOptionDto(r.Id.ToString(), r.Name, null)).ToListAsync(ct)
                : (await Db.Users.AsNoTracking().Where(u => u.BranchId == limit && u.RoleId != null).Select(u => new { u.RoleId, u.Role!.Name }).Distinct().ToListAsync(ct))
                    .OrderBy(r => r.Name).Select(r => new ReportOptionDto($"{r.RoleId}", r.Name)).ToList();
            filters.Add(Select(Role, "الدور", roles));
            filters.Add(Select(Status, "الحالة", [new("1", "نشط"), new("0", "موقوف")]));
            filters.Add(Text(Name, "الموظف", "الاسم أو الرقم الذاتي"));
            return filters;
        }

        public override Task<ReportTableDto> RunAsync(ReportRun run, CancellationToken ct)
        {
            var f = run.Filters;
            var limit = StructureScope.BranchOf(run.Viewer);
            // الموظفون الرسميون (بدخول وبلا دخول)؛ عمال المياومة في تقارير الحضور
            var query = Db.Users.AsNoTracking().Where(u => u.Kind != UserKind.DailyWorker && (limit == null || u.BranchId == limit));
            if (f.Int(Branch) is int branch) query = query.Where(u => u.BranchId == branch);
            if (f.Int(Department) is int department) query = query.Where(u => u.DepartmentId == department);
            if (f.Int(Office) is int office) query = query.Where(u => u.OfficeId == office);
            if (f.Int(Role) is int role) query = query.Where(u => u.RoleId == role);
            if (f.Int(Status) is int status) query = query.Where(u => u.IsActive == (status == 1));
            if (Blank(f.Text(Name)) is string name)
            {
                var id = name.ToUpperInvariant();
                query = query.Where(u => u.FullName.Contains(name) || u.PersonalIdNumber == id);
            }

            var rows = query
                .OrderBy(u => u.Branch!.Name).ThenBy(u => u.Department!.Name).ThenBy(u => u.FullName)
                .Select(u => new
                {
                    u.FullName, u.Email, u.PersonalIdNumber, u.PhoneNumber, Role = u.Role != null ? u.Role.Name : "بلا دخول",
                    Branch = u.Branch != null ? u.Branch.Name : null,
                    Department = u.Department != null ? u.Department.Name : null,
                    Office = u.Office != null ? u.Office.Name : null,
                    u.IsActive, u.CreatedAt
                });

            ReportColumnDto[] columns =
            [
                Col("الاسم"), Col("البريد", print: false), Col("الرقم الذاتي"), Col("الهاتف"), Col("الدور"),
                Col("الفرع"), Col("القسم"), Col("المكتب"), Col("الحالة"), Col("تاريخ الإضافة", Ct.Date, print: false)
            ];
            return QueryAsync(rows, run, columns, x => new object?[]
            {
                x.FullName, x.Email, x.PersonalIdNumber, x.PhoneNumber, x.Role, x.Branch, x.Department, x.Office,
                x.IsActive ? "نشط" : "موقوف", x.CreatedAt.Date
            }, ct);
        }
    }

    /// <summary>
    /// الهيكل التنظيمي: صف لكل فرع وقسم ومكتب مع عدد موظفيه النشطين والموقوفين — حدّ StructureScope.
    /// موظفو الفرع بلا قسم (والقسم بلا مكتب) في صف «مباشرة» كي يساوي مجموع الصفوف عدد الموظفين.
    /// </summary>
    public class StructureReport : ReportBase
    {
        public StructureReport(DataContext db) : base(db) { }

        public override string Key => "structure";
        public override string Title => "الهيكل التنظيمي";
        public override string Group => "الموظفون والهيكل";
        public override string Description => "الفروع وأقسامها ومكاتبها مع عدد الموظفين النشطين والموقوفين في كل وحدة";
        public override string Permission => "ExportStructureReport";

        public override async Task<List<ReportFilterDto>> GetFiltersAsync(Viewer viewer, CancellationToken ct)
        {
            if (StructureScope.BranchOf(viewer) != null) return [];
            var branches = await Db.Branches.AsNoTracking().OrderBy(b => b.Name)
                .Select(b => new ReportOptionDto(b.Id.ToString(), b.Name, null)).ToListAsync(ct);
            return [Select(Branch, "الفرع", branches)];
        }

        public override async Task<ReportTableDto> RunAsync(ReportRun run, CancellationToken ct)
        {
            var limit = StructureScope.BranchOf(run.Viewer) ?? run.Filters.Int(Branch);
            var branches = await Db.Branches.AsNoTracking().Where(b => limit == null || b.Id == limit)
                .OrderBy(b => b.Name).Select(b => new { b.Id, b.Name }).ToListAsync(ct);
            var departments = await Db.Departments.AsNoTracking().Where(d => limit == null || d.BranchId == limit)
                .OrderBy(d => d.Name).Select(d => new { d.Id, d.Name, d.BranchId }).ToListAsync(ct);
            var offices = await Db.Offices.AsNoTracking().Where(o => limit == null || o.Department.BranchId == limit)
                .OrderBy(o => o.Name).Select(o => new { o.Id, o.Name, o.DepartmentId }).ToListAsync(ct);
            var counts = await Db.Users.AsNoTracking().Where(u => u.Kind != UserKind.DailyWorker && u.BranchId != null && (limit == null || u.BranchId == limit))
                .GroupBy(u => new { u.BranchId, u.DepartmentId, u.OfficeId, u.IsActive })
                .Select(g => new { g.Key.BranchId, g.Key.DepartmentId, g.Key.OfficeId, g.Key.IsActive, Count = g.Count() })
                .ToListAsync(ct);

            object?[] Row(string branch, string department, string office, Func<int?, int?, int?, bool> match)
            {
                var active = counts.Where(c => c.IsActive && match(c.BranchId, c.DepartmentId, c.OfficeId)).Sum(c => c.Count);
                var inactive = counts.Where(c => !c.IsActive && match(c.BranchId, c.DepartmentId, c.OfficeId)).Sum(c => c.Count);
                return [branch, department, office, active, inactive, active + inactive];
            }

            var rows = new List<object?[]>();
            foreach (var b in branches)
            {
                var direct = Row(b.Name, "— مباشرة على الفرع —", "—", (br, d, _) => br == b.Id && d == null);
                if ((int)direct[5]! > 0) rows.Add(direct);
                foreach (var d in departments.Where(x => x.BranchId == b.Id))
                {
                    var inDepartment = Row(b.Name, d.Name, "— مباشرة على القسم —", (_, dep, o) => dep == d.Id && o == null);
                    var departmentOffices = offices.Where(o => o.DepartmentId == d.Id).ToList();
                    if ((int)inDepartment[5]! > 0 || departmentOffices.Count == 0) rows.Add(inDepartment);
                    foreach (var o in departmentOffices)
                        rows.Add(Row(b.Name, d.Name, o.Name, (_, _, office) => office == o.Id));
                }
            }

            ReportColumnDto[] columns =
            [
                Col("الفرع"), Col("القسم"), Col("المكتب"),
                Col("نشطون", Ct.Number, sum: true), Col("موقوفون", Ct.Number, sum: true), Col("المجموع", Ct.Number, sum: true)
            ];
            return Slice(columns, rows, run);
        }
    }
}
