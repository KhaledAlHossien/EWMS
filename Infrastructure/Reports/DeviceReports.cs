using System.Globalization;
using System.Text.Json;
using Application.Common;
using Application.DTOs.Response;
using Application.Features.DeviceInventory;
using Application.Features.MapLayers;
using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using static Application.Features.Reports.ReportFilters;
using Ct = Application.DTOs.Response.ReportColumnType;

namespace Infrastructure.Reports
{
    internal static class GovernorateOptions
    {
        public const string Key = "governorate";

        public static ReportFilterDto Filter() =>
            Select(Key, "المحافظة", Governorates.All.OrderBy(g => g.NameAr).Select(g => new ReportOptionDto(g.Code, g.NameAr)));
    }

    /// <summary>تركيبات الأجهزة (بلا كلمات السر) — حدّ DeviceInventoryRules.ScopeFor (قسمي، فرعي مع ViewBranchDevices، أو الكل)</summary>
    public class InstallationsReport : ReportBase
    {
        private const string Site = "site", Category = "category", Status = "status", Unverified = "unverified";

        public InstallationsReport(DataContext db) : base(db) { }

        public override string Key => "installations";
        public override string Title => "التركيبات";
        public override string Group => "الأجهزة والخريطة";
        public override string Description => "تركيبات الأجهزة على المواقع (بلا كلمات السر)، ومنها المعطّلة وما لم يُتحقَّق منه منذ مدة";
        public override string Permission => "ExportInstallationsReport";
        public override bool Landscape => true;

        public override async Task<List<ReportFilterDto>> GetFiltersAsync(Viewer viewer, CancellationToken ct)
        {
            var scope = DeviceInventoryRules.ScopeFor(viewer);
            var filters = new List<ReportFilterDto> { GovernorateOptions.Filter(), Text(Site, "الموقع", "جزء من اسم الموقع") };
            filters.AddRange(await UnitFiltersAsync(scope.All, scope.BranchId, ct));
            var categories = await Db.Devices.AsNoTracking().Where(d => d.Category != "").Select(d => d.Category).Distinct().OrderBy(c => c).ToListAsync(ct);
            filters.Add(Select(Category, "فئة الجهاز", categories.Select(c => new ReportOptionDto(c, c))));
            filters.Add(Select(Status, "الحالة", Enum.GetValues<InstallationStatus>()
                .Select(s => new ReportOptionDto(((int)s).ToString(), DeviceInventoryRules.StatusAr(s)))));
            filters.Add(Number(Unverified, "لم يُتحقَّق منها منذ (يوم)", "تشمل ما لم يُتحقَّق منه أبداً"));
            return filters;
        }

        public override Task<ReportTableDto> RunAsync(ReportRun run, CancellationToken ct)
        {
            var f = run.Filters;
            var query = Db.DeviceSites.AsNoTracking().Where(DeviceInventoryRules.ScopeFor(run.Viewer).Predicate());
            if (Blank(f.Text(GovernorateOptions.Key)) is string governorate) query = query.Where(i => i.Site.GovernorateCode == governorate);
            if (Blank(f.Text(Site)) is string site) query = query.Where(i => i.Site.Name.Contains(site));
            if (f.Int(Branch) is int branch) query = query.Where(i => i.BranchId == branch);
            if (f.Int(Department) is int department) query = query.Where(i => i.DepartmentId == department);
            if (Blank(f.Text(Category)) is string category) query = query.Where(i => i.Device.Category == category);
            if (f.Int(Status) is int status) query = query.Where(i => (int)i.Status == status);
            if (f.Int(Unverified) is int days)
            {
                var since = DateTime.Today.AddDays(-days);
                query = query.Where(i => i.LastVerifiedAt == null || i.LastVerifiedAt < since);
            }

            var rows = query.OrderBy(i => i.Site.Name).ThenBy(i => i.Device.Name).Select(i => new
            {
                Site = i.Site.Name, i.Site.GovernorateCode, Device = i.Device.Name, i.Device.Model, i.Device.Category, i.SN, i.InstallLocation,
                i.Ip, i.SubnetMask, i.UserName, i.InstallDate, i.LastVerifiedAt, i.Status,
                Department = i.Department != null ? i.Department.Name : null, i.Note
            });

            ReportColumnDto[] columns =
            [
                Col("الموقع"), Col("المحافظة"), Col("الجهاز"), Col("الموديل", print: false), Col("الفئة"), Col("الرقم التسلسلي", print: false),
                Col("مكان التركيب", print: false), Col("IP"), Col("قناع الشبكة", print: false), Col("المستخدم", print: false),
                Col("تاريخ التركيب", Ct.Date, print: false), Col("آخر تحقق", Ct.Date), Col("الحالة"), Col("القسم"), Col("ملاحظات", print: false)
            ];
            return QueryAsync(rows, run, columns, x => new object?[]
            {
                x.Site, Governorates.NameOf(x.GovernorateCode), x.Device, Blank(x.Model), Blank(x.Category), Blank(x.SN), Blank(x.InstallLocation),
                x.Ip, x.SubnetMask, x.UserName, x.InstallDate?.Date, x.LastVerifiedAt?.Date, DeviceInventoryRules.StatusAr(x.Status),
                x.Department ?? "بلا قسم", Blank(x.Note)
            }, ct);
        }
    }

    /// <summary>سجل المواقع المشترك (كل المواقع)، وعدد التركيبات فيه ضمن حد تركيبات المستخدم</summary>
    public class SitesReport : ReportBase
    {
        private const string Name = "name", NoCoordinates = "noCoordinates";

        public SitesReport(DataContext db) : base(db) { }

        public override string Key => "sites";
        public override string Title => "المواقع";
        public override string Group => "الأجهزة والخريطة";
        public override string Description => "سجل المواقع المشترك: المحافظة والإحداثيات وجهة التواصل والجهة المسؤولة وعدد التركيبات التي تراها";
        public override string Permission => "ExportSitesReport";

        public override Task<List<ReportFilterDto>> GetFiltersAsync(Viewer viewer, CancellationToken ct) => Task.FromResult(new List<ReportFilterDto>
        {
            GovernorateOptions.Filter(),
            Text(Name, "الموقع", "جزء من الاسم"),
            Select(NoCoordinates, "الإحداثيات", YesOnly("بلا إحداثيات فقط"))
        });

        public override Task<ReportTableDto> RunAsync(ReportRun run, CancellationToken ct)
        {
            var f = run.Filters;
            var installations = Db.DeviceSites.Where(DeviceInventoryRules.ScopeFor(run.Viewer).Predicate());
            var query = Db.Sites.AsNoTracking();
            if (Blank(f.Text(GovernorateOptions.Key)) is string governorate) query = query.Where(s => s.GovernorateCode == governorate);
            if (Blank(f.Text(Name)) is string name) query = query.Where(s => s.Name.Contains(name));
            if (f.Text(NoCoordinates) == "1") query = query.Where(s => s.Latitude == null || s.Longitude == null);

            var rows = query.OrderBy(s => s.Name).Select(s => new
            {
                s.Name, s.GovernorateCode, s.Latitude, s.Longitude, s.ContactName, s.ContactPhone, s.ResponsibleParty, s.Description,
                Installations = installations.Count(i => i.SiteId == s.Id)
            });

            ReportColumnDto[] columns =
            [
                Col("الموقع"), Col("المحافظة"), Col("خط العرض", print: false), Col("خط الطول", print: false), Col("جهة التواصل"),
                Col("هاتفها"), Col("الجهة المسؤولة"), Col("التركيبات", Ct.Number, sum: true), Col("الوصف", print: false)
            ];
            return QueryAsync(rows, run, columns, x => new object?[]
            {
                x.Name, Governorates.NameOf(x.GovernorateCode),
                x.Latitude?.ToString("0.######", CultureInfo.InvariantCulture), x.Longitude?.ToString("0.######", CultureInfo.InvariantCulture),
                Blank(x.ContactName), Blank(x.ContactPhone), Blank(x.ResponsibleParty), x.Installations, Blank(x.Description)
            }, ct);
        }
    }

    /// <summary>
    /// سجلات طبقة يدوية: حقولها الظاهرة أعمدة (الملفات بأسمائها). الطبقات من فرعي (أو أي فرع لنظرة المؤسسة)،
    /// والسجلات بحد MapLayerRules.EntryScopeFor (قسمي، فرعي مع ViewBranchMapEntries، أو الكل).
    /// </summary>
    public class MapEntriesReport : ReportBase
    {
        private const string Layer = "layer", Site = "site";

        public MapEntriesReport(DataContext db) : base(db) { }

        public override string Key => "map-entries";
        public override string Title => "سجلات طبقة";
        public override string Group => "الأجهزة والخريطة";
        public override string Description => "سجلات طبقة يدوية على المواقع: حقولها أعمدة، مع الموقع والقسم ومن أضافها";
        public override string Permission => "ExportMapEntriesReport";
        public override bool Landscape => true;

        public override async Task<List<ReportFilterDto>> GetFiltersAsync(Viewer viewer, CancellationToken ct)
        {
            var wide = viewer.IsOrganizationWide;
            var ownBranch = viewer.User.BranchId;
            if (!wide && ownBranch == null) return [];

            var layers = await Db.MapLayers.AsNoTracking()
                .Where(l => l.Source == "" && (wide || l.BranchId == ownBranch))
                .OrderBy(l => l.Branch.Name).ThenBy(l => l.SortOrder).ThenBy(l => l.Name)
                .Select(l => new { l.Id, l.Name, l.BranchId, Branch = l.Branch.Name, l.IsActive }).ToListAsync(ct);

            var filters = new List<ReportFilterDto>();
            if (wide)
                filters.Add(Select(Branch, "الفرع", layers.GroupBy(l => new { l.BranchId, l.Branch })
                    .Select(g => new ReportOptionDto(g.Key.BranchId.ToString(), g.Key.Branch))));
            filters.Add(Select(Layer, "الطبقة", layers.Select(l => new ReportOptionDto(l.Id.ToString(),
                    (wide ? $"{l.Name} — {l.Branch}" : l.Name) + (l.IsActive ? "" : " (معطّلة)"), wide ? l.BranchId.ToString() : null)),
                dependsOn: wide ? Branch : null, required: true));

            var scope = MapLayerRules.EntryScopeFor(viewer);
            if (scope.All || scope.BranchId != null)
            {
                var departments = await Db.Departments.AsNoTracking()
                    .Where(d => scope.All || d.BranchId == scope.BranchId).OrderBy(d => d.Name)
                    .Select(d => new ReportOptionDto(d.Id.ToString(), scope.All ? d.Name + " — " + d.Branch.Name : d.Name, scope.All ? d.BranchId.ToString() : null))
                    .ToListAsync(ct);
                filters.Add(Select(Department, "القسم", departments, dependsOn: wide ? Branch : null));
            }
            filters.Add(Text(Site, "الموقع", "جزء من اسم الموقع"));
            return filters;
        }

        public override async Task<ReportTableDto> RunAsync(ReportRun run, CancellationToken ct)
        {
            var f = run.Filters;
            var layerId = f.Int(Layer) ?? throw new InvalidOperationException("اختر «الطبقة»");
            var layer = await Db.MapLayers.AsNoTracking().Include(l => l.Fields).FirstOrDefaultAsync(l => l.Id == layerId, ct)
                ?? throw new KeyNotFoundException("الطبقة غير موجودة");
            if (!MapLayerRules.CanAccessLayer(run.Viewer, layer))
                throw new UnauthorizedAccessException("هذه الطبقة تتبع فرعاً آخر");
            var fields = layer.Fields.Where(x => !x.IsHidden).OrderBy(x => x.SortOrder).ThenBy(x => x.Id).ToList();

            var query = Db.MapEntries.AsNoTracking().Where(e => e.LayerId == layerId).Where(MapLayerRules.EntryScopeFor(run.Viewer).Predicate());
            if (f.Int(Department) is int department) query = query.Where(e => e.DepartmentId == department);
            if (Blank(f.Text(Site)) is string site) query = query.Where(e => e.Site.Name.Contains(site));

            var rows = query.OrderBy(e => e.Site.Name).ThenBy(e => e.Id).Select(e => new
            {
                Site = e.Site.Name, e.Site.GovernorateCode, Department = e.Department != null ? e.Department.Name : null,
                e.Values, Creator = e.CreatedByUser.FullName, e.CreatedAt, e.UpdatedAt,
                Files = e.Files.Select(x => new { x.FieldId, x.FileName }).ToList()
            });

            var columns = new List<ReportColumnDto> { Col("الموقع"), Col("المحافظة", print: false), Col("القسم") };
            columns.AddRange(fields.Select((field, i) => Col(field.Label, field.Type switch
            {
                MapFieldType.Number => Ct.Decimal,
                MapFieldType.Date => Ct.Date,
                _ => Ct.Text
            }, print: i < 6 && field.Type is not (MapFieldType.LongText or MapFieldType.Files))));
            columns.AddRange([Col("أضافه"), Col("تاريخ الإضافة", Ct.Date), Col("آخر تعديل", Ct.Date, print: false)]);

            return await QueryAsync(rows, run, columns, x =>
            {
                var values = MapLayerRules.ParseValues(x.Values);
                var row = new List<object?> { x.Site, Governorates.NameOf(x.GovernorateCode), x.Department ?? "مستوى الفرع" };
                foreach (var field in fields)
                    row.Add(field.Type == MapFieldType.Files
                        ? string.Join("، ", x.Files.Where(file => file.FieldId == field.Id).Select(file => file.FileName)) is { Length: > 0 } names ? names : null
                        : Value(field, values.TryGetValue(field.Id.ToString(CultureInfo.InvariantCulture), out var raw) ? raw : null));
                row.AddRange([x.Creator, x.CreatedAt.Date, x.UpdatedAt?.Date]);
                return [.. row];
            }, ct);
        }

        /// <summary>قيمة حقل بنوعها (رقم/تاريخ/نعم-لا/نص) — القيم المحفوظة نظيفة (MapLayerRules.NormalizeValues)</summary>
        private static object? Value(MapLayerField field, JsonElement? raw)
        {
            if (raw is not JsonElement value || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return null;
            return field.Type switch
            {
                MapFieldType.Number when value.ValueKind == JsonValueKind.Number => value.GetDecimal(),
                MapFieldType.Date when value.ValueKind == JsonValueKind.String
                    && DateTime.TryParseExact(value.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) => date,
                MapFieldType.YesNo => value.ValueKind switch { JsonValueKind.True => "نعم", JsonValueKind.False => "لا", _ => null },
                _ => value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText()
            };
        }
    }
}
