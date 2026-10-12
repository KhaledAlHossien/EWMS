using Application.Common;
using Application.DTOs.Response;
using Application.Features.Maintenance;
using Application.Features.Maintenance.SpareParts;
using Application.Interfaces;
using Domain.Entities.Maintenance;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using static Application.Features.Reports.ReportFilters;
using Ct = Application.DTOs.Response.ReportColumnType;

namespace Infrastructure.Reports
{
    /// <summary>
    /// أساس تقارير طلبات الصيانة: حدّ MaintenanceRules (طلباتي، وطلبات قسمي مع ViewDepartmentMaintenance، والكل لمدير النظام)
    /// محسوب بصلاحية التقرير نفسها، وفلاتر الفترة والفرع/القسم (لمدير النظام فقط، فغيره لا يرى إلا قسمه).
    /// </summary>
    public abstract class MaintenanceReportBase : ReportBase
    {
        protected MaintenanceReportBase(DataContext db) : base(db) { }

        public override string Group => "الصيانة وقطع الغيار";

        protected MaintenanceRules.Boundary? BoundaryFor(Viewer viewer) => MaintenanceRules.ViewBoundary(viewer, Permission);

        protected async Task<List<ReportFilterDto>> CommonFiltersAsync(Viewer viewer, CancellationToken ct)
        {
            var filters = Period(YearStart, DateTime.Today).ToList();
            filters.AddRange(await UnitFiltersAsync(BoundaryFor(viewer)?.All == true, null, ct));
            return filters;
        }

        protected IQueryable<MaintenanceRequest> Requests(ReportRun run)
        {
            var f = run.Filters;
            var query = Db.MaintenanceRequests.AsNoTracking().Where(MaintenanceRules.RequestFilter(BoundaryFor(run.Viewer)));
            if (f.Date(From) is DateTime from) query = query.Where(r => r.CreatedAt >= from);
            if (f.EndOfDay(To) is DateTime to) query = query.Where(r => r.CreatedAt <= to);
            if (f.Int(Branch) is int branch) query = query.Where(r => r.Department != null && r.Department.BranchId == branch);
            if (f.Int(Department) is int department) query = query.Where(r => r.DepartmentId == department);
            return query;
        }

        protected static decimal? RepairDays(DateTime? started, DateTime? completed) =>
            started is DateTime s && completed is DateTime c && c >= s ? Math.Round((decimal)(c - s).TotalDays, 1) : null;
    }

    /// <summary>طلبات الصيانة المسجّلة في فترة</summary>
    public class MaintenanceRequestsReport : MaintenanceReportBase
    {
        private const string Status = "status", Technician = "technician", DeviceType = "deviceType", Damage = "damage", Client = "client";

        public MaintenanceRequestsReport(DataContext db) : base(db) { }

        public override string Key => "maintenance-requests";
        public override string Title => "طلبات الصيانة";
        public override string Description => "طلبات الصيانة المسجّلة في فترة: العميل والجهاز والعطل والحالة والفني ومدة الإصلاح وكلفة القطع";
        public override string Permission => "ExportMaintenanceRequestsReport";
        public override bool Landscape => true;

        public override async Task<List<ReportFilterDto>> GetFiltersAsync(Viewer viewer, CancellationToken ct)
        {
            var filters = await CommonFiltersAsync(viewer, ct);
            var statuses = await Db.MaintenanceRequestStatuses.AsNoTracking().OrderBy(s => s.Stage).ThenBy(s => s.Id)
                .Select(s => new ReportOptionDto(s.Id.ToString(), s.Name, null)).ToListAsync(ct);
            filters.Add(Select(Status, "الحالة", statuses));

            var boundary = BoundaryFor(viewer);
            if (boundary != null && (boundary.All || boundary.DepartmentId != null))
            {
                var technicians = await Db.MaintenanceRequests.AsNoTracking().Where(MaintenanceRules.RequestFilter(boundary))
                    .Select(r => new { r.UserId, r.User.FullName }).Distinct().OrderBy(t => t.FullName).ToListAsync(ct);
                filters.Add(Select(Technician, "الفني", technicians.Select(t => new ReportOptionDto(t.UserId.ToString(), t.FullName))));
            }

            var types = await Db.DeviceTypes.AsNoTracking().OrderBy(t => t.Name)
                .Select(t => new ReportOptionDto(t.Id.ToString(), t.Name, null)).ToListAsync(ct);
            filters.Add(Select(DeviceType, "نوع الجهاز", types));
            var damages = await Db.DamageTypes.AsNoTracking().OrderBy(t => t.Name)
                .Select(t => new ReportOptionDto(t.Id.ToString(), t.Name, null)).ToListAsync(ct);
            filters.Add(Select(Damage, "نوع العطل", damages));
            filters.Add(Text(Client, "العميل", "جزء من الاسم"));
            return filters;
        }

        public override Task<ReportTableDto> RunAsync(ReportRun run, CancellationToken ct)
        {
            var f = run.Filters;
            var query = Requests(run);
            if (f.Int(Status) is int status) query = query.Where(r => r.MaintenanceRequestStatusId == status);
            if (f.Int(Technician) is int technician) query = query.Where(r => r.UserId == technician);
            if (f.Int(DeviceType) is int type) query = query.Where(r => r.DeviceMaintenance.DeviceTypeId == type);
            if (f.Int(Damage) is int damage) query = query.Where(r => r.DamageTypeId == damage);
            if (Blank(f.Text(Client)) is string client) query = query.Where(r => r.ClientName.Contains(client));

            var rows = query.OrderByDescending(r => r.CreatedAt).Select(r => new
            {
                r.Id, r.CreatedAt, r.ClientName, Device = r.DeviceMaintenance.Name, r.DeviceMaintenance.SerialNumber, r.DeviceMaintenance.Model,
                Type = r.DeviceMaintenance.DeviceType.Name, Company = r.DeviceMaintenance.DeviceCompany.Name, Damage = r.DamageType.Name,
                Status = r.MaintenanceRequestStatus.Name, Technician = r.User.FullName, Department = r.Department != null ? r.Department.Name : null,
                r.StartedAt, r.CompletedAt, r.DeliveredAt,
                Parts = Db.MaintenanceRequestParts.Where(p => p.MaintenanceRequestId == r.Id).Sum(p => (decimal?)(p.Quantity * p.UnitCost)) ?? 0m
            });

            ReportColumnDto[] columns =
            [
                Col("رقم الطلب"), Col("تاريخ التسجيل", Ct.Date), Col("العميل"), Col("الجهاز"), Col("الرقم التسلسلي"),
                Col("الموديل", print: false), Col("النوع", print: false), Col("الشركة", print: false), Col("العطل"), Col("الحالة"), Col("الفني"),
                Col("القسم", print: false), Col("البدء", Ct.Date, print: false), Col("الإنجاز", Ct.Date, print: false),
                Col("أيام الإصلاح", Ct.Decimal), Col("كلفة القطع", Ct.Money, sum: true), Col("التسليم", Ct.Date, print: false)
            ];
            return QueryAsync(rows, run, columns, x => new object?[]
            {
                MaintenanceRules.RequestNumber(x.Id, x.CreatedAt), x.CreatedAt.Date, x.ClientName, x.Device, x.SerialNumber, x.Model,
                x.Type, x.Company, x.Damage, x.Status, x.Technician, x.Department, x.StartedAt?.Date, x.CompletedAt?.Date,
                RepairDays(x.StartedAt, x.CompletedAt), x.Parts, x.DeliveredAt?.Date
            }, ct);
        }
    }

    /// <summary>أداء الفنيين: طلبات كل فني المسجّلة في الفترة حسب مرحلتها، ومتوسط مدة الإصلاح وكلفة القطع</summary>
    public class TechniciansReport : MaintenanceReportBase
    {
        public TechniciansReport(DataContext db) : base(db) { }

        public override string Key => "technicians";
        public override string Title => "أداء الفنيين";
        public override string Description => "لكل فني: طلبات الفترة، الجديدة أو قيد العمل، الجاهزة أو المسلّمة، غير القابلة للصيانة، متوسط مدة الإصلاح وكلفة القطع";
        public override string Permission => "ExportTechniciansReport";

        public override Task<List<ReportFilterDto>> GetFiltersAsync(Viewer viewer, CancellationToken ct) => CommonFiltersAsync(viewer, ct);

        public override async Task<ReportTableDto> RunAsync(ReportRun run, CancellationToken ct)
        {
            var requests = await Requests(run).Select(r => new
            {
                r.UserId, Technician = r.User.FullName, Department = r.User.Department != null ? r.User.Department.Name : null,
                r.MaintenanceRequestStatus.Stage, r.StartedAt, r.CompletedAt,
                Parts = Db.MaintenanceRequestParts.Where(p => p.MaintenanceRequestId == r.Id).Sum(p => (decimal?)(p.Quantity * p.UnitCost)) ?? 0m
            }).ToListAsync(ct);

            var rows = requests.GroupBy(r => new { r.UserId, r.Technician, r.Department })
                .Select(g =>
                {
                    var durations = g.Select(r => RepairDays(r.StartedAt, r.CompletedAt)).Where(d => d != null).Select(d => d!.Value).ToList();
                    return new
                    {
                        g.Key.Technician,
                        Row = new object?[]
                        {
                            g.Key.Technician, g.Key.Department, g.Count(),
                            g.Count(r => r.Stage is MaintenanceStage.New or MaintenanceStage.InProgress),
                            g.Count(r => r.Stage is MaintenanceStage.Ready or MaintenanceStage.Delivered),
                            g.Count(r => r.Stage == MaintenanceStage.NotRepairable),
                            durations.Count == 0 ? null : Math.Round(durations.Average(), 1),
                            g.Sum(r => r.Parts)
                        }
                    };
                })
                .OrderByDescending(x => (int)x.Row[2]!).ThenBy(x => x.Technician)
                .Select(x => x.Row).ToList();

            ReportColumnDto[] columns =
            [
                Col("الفني"), Col("القسم"), Col("الطلبات", Ct.Number, sum: true), Col("جديدة أو قيد العمل", Ct.Number, sum: true),
                Col("جاهزة أو مسلّمة", Ct.Number, sum: true), Col("غير قابلة للصيانة", Ct.Number, sum: true),
                Col("متوسط أيام الإصلاح", Ct.Decimal), Col("كلفة القطع", Ct.Money, sum: true)
            ];
            return Slice(columns, rows, run);
        }
    }

    /// <summary>
    /// أساس تقارير قطع الغيار: حدّ المخزون كما في SparePartRules — قطع قسمي، ومدير النظام كل الأقسام (مع فلتر القسم).
    /// حساب بلا قسم (غير مدير النظام) لا يرى شيئاً.
    /// </summary>
    public abstract class SparePartReportBase : ReportBase
    {
        protected const string Part = "part";

        protected SparePartReportBase(DataContext db) : base(db) { }

        public override string Group => "الصيانة وقطع الغيار";

        /// <summary>(الكل، قسمي) — قسم null وليس الكل = لا شيء</summary>
        protected static (bool All, int? Department) Stock(Viewer viewer) => (viewer.IsSuperAdmin, viewer.User.DepartmentId);

        protected IQueryable<SparePart> Parts(Viewer viewer)
        {
            var (all, own) = Stock(viewer);
            return Db.SpareParts.AsNoTracking().Where(p => all || (own != null && p.DepartmentId == own));
        }

        protected async Task<List<ReportFilterDto>> DepartmentFilterAsync(Viewer viewer, CancellationToken ct)
        {
            if (!Stock(viewer).All) return [];
            var departments = await Db.SpareParts.AsNoTracking().Select(p => new { p.DepartmentId, p.Department.Name, Branch = p.Department.Branch.Name })
                .Distinct().OrderBy(d => d.Name).ToListAsync(ct);
            return [Select(Department, "القسم", departments.Select(d => new ReportOptionDto(d.DepartmentId.ToString(), $"{d.Name} — {d.Branch}")))];
        }
    }

    /// <summary>حركات المخزون في فترة (إدخال/صرف/إرجاع/تسوية)</summary>
    public class SparePartMovementsReport : SparePartReportBase
    {
        private const string Type = "type";

        public SparePartMovementsReport(DataContext db) : base(db) { }

        public override string Key => "spare-part-movements";
        public override string Title => "حركات قطع الغيار";
        public override string Description => "إدخال وصرف وإرجاع وتسوية قطع الغيار في فترة: الكمية والكلفة والرصيد بعد الحركة والطلب";
        public override string Permission => "ExportSparePartMovementsReport";
        public override bool Landscape => true;

        public override async Task<List<ReportFilterDto>> GetFiltersAsync(Viewer viewer, CancellationToken ct)
        {
            var filters = Period(MonthStart, DateTime.Today).ToList();
            filters.AddRange(await DepartmentFilterAsync(viewer, ct));
            var all = Stock(viewer).All;
            var parts = await Parts(viewer).OrderBy(p => p.Name)
                .Select(p => new ReportOptionDto(p.Id.ToString(), all ? p.Name + " — " + p.Department.Name : p.Name, all ? p.DepartmentId.ToString() : null))
                .ToListAsync(ct);
            filters.Add(Select(Part, "القطعة", parts, dependsOn: all ? Department : null));
            filters.Add(Select(Type, "الحركة", Enum.GetValues<SparePartMovementType>()
                .Select(t => new ReportOptionDto(((int)t).ToString(), SparePartRules.MovementAr(t)))));
            return filters;
        }

        public override Task<ReportTableDto> RunAsync(ReportRun run, CancellationToken ct)
        {
            var f = run.Filters;
            var (all, own) = Stock(run.Viewer);
            var query = Db.SparePartMovements.AsNoTracking().Where(m => all || (own != null && m.SparePart.DepartmentId == own));
            if (f.Date(From) is DateTime from) query = query.Where(m => m.Date >= from);
            if (f.EndOfDay(To) is DateTime to) query = query.Where(m => m.Date <= to);
            if (f.Int(Department) is int department) query = query.Where(m => m.SparePart.DepartmentId == department);
            if (f.Int(Part) is int part) query = query.Where(m => m.SparePartId == part);
            if (f.Int(Type) is int type) query = query.Where(m => (int)m.Type == type);

            var rows = query.OrderByDescending(m => m.Date).ThenByDescending(m => m.Id).Select(m => new
            {
                m.Date, Part = m.SparePart.Name, m.SparePart.PartNumber, Department = m.SparePart.Department.Name, m.Type,
                m.Quantity, m.UnitCost, m.BalanceAfter, m.MaintenanceRequestId,
                RequestCreated = m.MaintenanceRequest != null ? m.MaintenanceRequest.CreatedAt : (DateTime?)null,
                User = m.User.FullName, m.Note
            });

            ReportColumnDto[] columns =
            [
                Col("التاريخ", Ct.Date), Col("القطعة"), Col("رقم القطعة", print: false), Col("القسم", print: false), Col("الحركة"),
                Col("الكمية", Ct.Decimal), Col("كلفة الوحدة", Ct.Money), Col("القيمة", Ct.Money), Col("الرصيد بعد", Ct.Decimal),
                Col("الطلب"), Col("المستخدم"), Col("ملاحظة", print: false)
            ];
            return QueryAsync(rows, run, columns, x => new object?[]
            {
                x.Date.Date, x.Part, Blank(x.PartNumber), x.Department, SparePartRules.MovementAr(x.Type),
                x.Quantity, x.UnitCost, Math.Abs(x.Quantity) * x.UnitCost, x.BalanceAfter,
                x.MaintenanceRequestId is int id && x.RequestCreated is DateTime created ? MaintenanceRules.RequestNumber(id, created) : null,
                x.User, Blank(x.Note)
            }, ct);
        }
    }

    /// <summary>رصيد كل قطعة الآن وقيمته</summary>
    public class SparePartStockReport : SparePartReportBase
    {
        private const string Below = "below", Name = "name";

        public SparePartStockReport(DataContext db) : base(db) { }

        public override string Key => "spare-part-stock";
        public override string Title => "مخزون قطع الغيار";
        public override string Description => "رصيد كل قطعة الآن: الكمية والحد الأدنى ومتوسط الكلفة والقيمة، مع ما نزل تحت حده";
        public override string Permission => "ExportSparePartStockReport";

        public override async Task<List<ReportFilterDto>> GetFiltersAsync(Viewer viewer, CancellationToken ct)
        {
            var filters = await DepartmentFilterAsync(viewer, ct);
            filters.Add(Select(Below, "تحت الحد الأدنى", YesOnly("تحت الحد الأدنى فقط")));
            filters.Add(Text(Name, "القطعة", "جزء من الاسم أو رقم القطعة"));
            return filters;
        }

        public override Task<ReportTableDto> RunAsync(ReportRun run, CancellationToken ct)
        {
            var f = run.Filters;
            var query = Parts(run.Viewer);
            if (f.Int(Department) is int department) query = query.Where(p => p.DepartmentId == department);
            if (f.Text(Below) == "1") query = query.Where(p => p.MinQuantity > 0 && p.Quantity < p.MinQuantity);
            if (Blank(f.Text(Name)) is string name) query = query.Where(p => p.Name.Contains(name) || p.PartNumber.Contains(name));

            var rows = query.OrderBy(p => p.Name).Select(p => new
            {
                p.Name, p.PartNumber, Department = p.Department.Name, p.Unit, p.Quantity, p.MinQuantity, p.AverageCost
            });

            ReportColumnDto[] columns =
            [
                Col("القطعة"), Col("رقم القطعة"), Col("القسم", print: false), Col("الوحدة"), Col("الكمية", Ct.Decimal),
                Col("الحد الأدنى", Ct.Decimal), Col("متوسط الكلفة", Ct.Money), Col("القيمة", Ct.Money, sum: true), Col("تحت الحد")
            ];
            return QueryAsync(rows, run, columns, x => new object?[]
            {
                x.Name, Blank(x.PartNumber), x.Department, Blank(x.Unit), x.Quantity, x.MinQuantity, x.AverageCost,
                Math.Round(x.Quantity * x.AverageCost, 2), x.MinQuantity > 0 && x.Quantity < x.MinQuantity ? "نعم" : null
            }, ct);
        }
    }
}
