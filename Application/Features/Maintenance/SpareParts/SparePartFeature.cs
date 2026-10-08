using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities;
using Domain.Entities.Maintenance;
using FluentValidation;
using MediatR;

namespace Application.Features.Maintenance.SpareParts
{
    // ════════════════════ القطع والمخزون ════════════════════
    public record GetSparePartDepartmentsQuery : IRequest<List<NamedRefDto>>;
    public record SearchSparePartsQuery(SparePartFilterDto Filter) : IRequest<PagedResultDto<SparePartResponseDto>>;
    public record GetSparePartByIdQuery(int Id) : IRequest<SparePartResponseDto>;
    public record CreateSparePartCommand(SparePartRequestDto Dto) : IRequest<SparePartResponseDto>;
    public record UpdateSparePartCommand(int Id, SparePartRequestDto Dto) : IRequest<SparePartResponseDto>;
    public record DeleteSparePartCommand(int Id) : IRequest<Unit>;
    public record ReceiveSparePartCommand(int Id, ReceiveSparePartDto Dto) : IRequest<SparePartResponseDto>;
    public record AdjustSparePartCommand(int Id, AdjustSparePartDto Dto) : IRequest<SparePartResponseDto>;
    public record GetSparePartMovementsQuery(int Id, int Page, int PageSize) : IRequest<PagedResultDto<SparePartMovementDto>>;
    public record GetSparePartReportQuery(int? DepartmentId, DateTime? From, DateTime? To) : IRequest<SparePartReportDto>;

    // ════════════════════ قطع طلب الصيانة ════════════════════
    public record GetRequestPartsQuery(int RequestId) : IRequest<RequestPartsDto>;
    public record GetAvailablePartsForRequestQuery(int RequestId, string? Search) : IRequest<List<SparePartResponseDto>>;
    public record IssueSparePartCommand(int RequestId, IssueSparePartDto Dto) : IRequest<RequestPartsDto>;
    public record ReturnSparePartCommand(int RequestPartId) : IRequest<RequestPartsDto>;

    /// <summary>
    /// حدود مخزون القطع (قرار المستخدم 2026-10-05): مخزون لكل قسم — يرى الموظف ويدير قطع قسمه فقط، ومدير النظام كل الأقسام.
    /// الصرف على طلب: حدّ IssueSparePart كحدّ التعديل (طلباته، وطلبات قسمه لمن يملك ViewDepartmentMaintenance)،
    /// ومن مخزون قسم الطلب حصراً، والطلب مفتوح.
    /// </summary>
    public static class SparePartRules
    {
        public const int MaxQuantity = 1_000_000_000;

        /// <summary>قسم المخزون للمستخدم: null = كل الأقسام (مدير النظام)</summary>
        public static int? Department(Viewer v) =>
            v.IsSuperAdmin ? null
            : v.User.DepartmentId ?? throw new InvalidOperationException("حسابك لا يتبع لقسم له مخزون قطع غيار");

        public static void EnsureIn(Viewer v, SparePart part)
        {
            if (!v.IsSuperAdmin && part.DepartmentId != v.User.DepartmentId)
                throw new UnauthorizedAccessException("هذه القطعة ليست في مخزون قسمك");
        }

        public static bool TwoDecimals(decimal value) => decimal.Round(value, 2) == value;

        public static bool CrossedBelowMinimum(SparePart part, StockChange change) =>
            part.MinQuantity > 0 && change.Before >= part.MinQuantity && change.After < part.MinQuantity;

        public static string MovementAr(SparePartMovementType type) => type switch
        {
            SparePartMovementType.Receive => "إدخال",
            SparePartMovementType.Issue => "صرف",
            SparePartMovementType.Return => "إرجاع",
            SparePartMovementType.Adjust => "تسوية",
            _ => type.ToString()
        };

        public static string Amount(decimal value) => value.ToString("0.##");

        public static SparePartResponseDto ToDto(SparePart p) => new()
        {
            Id = p.Id,
            DepartmentId = p.DepartmentId,
            DepartmentName = p.Department?.Name ?? string.Empty,
            Name = p.Name,
            PartNumber = p.PartNumber,
            Unit = p.Unit,
            Description = p.Description,
            Quantity = p.Quantity,
            MinQuantity = p.MinQuantity,
            AverageCost = p.AverageCost,
            StockValue = Math.Round(p.Quantity * p.AverageCost, 2),
            IsLow = p.MinQuantity > 0 && p.Quantity < p.MinQuantity,
            DeviceTypes = p.DeviceTypes.Where(t => t.DeviceType != null).Select(t => new NamedRefDto { Id = t.DeviceTypeId, Name = t.DeviceType.Name }).OrderBy(x => x.Name).ToList(),
            DeviceCompanies = p.DeviceCompanies.Where(c => c.DeviceCompany != null).Select(c => new NamedRefDto { Id = c.DeviceCompanyId, Name = c.DeviceCompany.Name }).OrderBy(x => x.Name).ToList(),
            CreatedAt = p.CreatedAt,
            UpdatedAt = p.UpdatedAt
        };
    }

    // ════════════════════ التحقق من المدخلات ════════════════════

    public class SparePartRequestDtoValidator : AbstractValidator<SparePartRequestDto>
    {
        public SparePartRequestDtoValidator()
        {
            RuleFor(x => x.Name).NotEmpty().WithMessage("اسم القطعة مطلوب").MaximumLength(200).WithMessage("اسم القطعة لا يتجاوز 200 حرف");
            RuleFor(x => x.PartNumber).MaximumLength(100).WithMessage("رقم القطعة لا يتجاوز 100 حرف");
            RuleFor(x => x.Unit).NotEmpty().WithMessage("وحدة القطعة مطلوبة (قطعة، متر…)").MaximumLength(30).WithMessage("الوحدة لا تتجاوز 30 حرفاً");
            RuleFor(x => x.Description).MaximumLength(1000).WithMessage("الوصف لا يتجاوز 1000 حرف");
            RuleFor(x => x.MinQuantity)
                .InclusiveBetween(0, SparePartRules.MaxQuantity).WithMessage("الحد الأدنى رقم موجب")
                .Must(SparePartRules.TwoDecimals).WithMessage("الحد الأدنى بخانتين عشريتين على الأكثر");
            RuleForEach(x => x.DeviceTypeIds).GreaterThan(0).WithMessage("نوع جهاز غير صحيح");
            RuleForEach(x => x.DeviceCompanyIds).GreaterThan(0).WithMessage("شركة غير صحيحة");
        }
    }

    public class CreateSparePartCommandValidator : AbstractValidator<CreateSparePartCommand>
    {
        public CreateSparePartCommandValidator() => RuleFor(x => x.Dto).SetValidator(new SparePartRequestDtoValidator());
    }

    public class UpdateSparePartCommandValidator : AbstractValidator<UpdateSparePartCommand>
    {
        public UpdateSparePartCommandValidator() => RuleFor(x => x.Dto).SetValidator(new SparePartRequestDtoValidator());
    }

    public class ReceiveSparePartCommandValidator : AbstractValidator<ReceiveSparePartCommand>
    {
        public ReceiveSparePartCommandValidator()
        {
            RuleFor(x => x.Dto.Quantity)
                .GreaterThan(0).WithMessage("الكمية المستلمة أكبر من صفر")
                .LessThanOrEqualTo(SparePartRules.MaxQuantity).WithMessage("الكمية كبيرة جداً")
                .Must(SparePartRules.TwoDecimals).WithMessage("الكمية بخانتين عشريتين على الأكثر");
            RuleFor(x => x.Dto.UnitCost)
                .GreaterThanOrEqualTo(0).WithMessage("سعر الوحدة لا يكون سالباً")
                .LessThanOrEqualTo(SparePartRules.MaxQuantity).WithMessage("السعر كبير جداً")
                .Must(SparePartRules.TwoDecimals).WithMessage("السعر بخانتين عشريتين على الأكثر");
            RuleFor(x => x.Dto.Date)
                .Must(d => d == null || d.Value.Date <= DateTime.Today).WithMessage("تاريخ الاستلام لا يكون في المستقبل");
            RuleFor(x => x.Dto.Source).MaximumLength(500).WithMessage("المصدر لا يتجاوز 500 حرف");
        }
    }

    public class AdjustSparePartCommandValidator : AbstractValidator<AdjustSparePartCommand>
    {
        public AdjustSparePartCommandValidator()
        {
            RuleFor(x => x.Dto.Delta)
                .NotEqual(0).WithMessage("فرق الكمية لا يكون صفراً")
                .InclusiveBetween(-SparePartRules.MaxQuantity, SparePartRules.MaxQuantity).WithMessage("الفرق كبير جداً")
                .Must(SparePartRules.TwoDecimals).WithMessage("الكمية بخانتين عشريتين على الأكثر");
            RuleFor(x => x.Dto.Reason).NotEmpty().WithMessage("سبب التسوية مطلوب (جرد، تالف…)").MaximumLength(500).WithMessage("السبب لا يتجاوز 500 حرف");
        }
    }

    public class IssueSparePartCommandValidator : AbstractValidator<IssueSparePartCommand>
    {
        public IssueSparePartCommandValidator()
        {
            RuleFor(x => x.Dto.SparePartId).GreaterThan(0).WithMessage("اختر القطعة");
            RuleFor(x => x.Dto.Quantity)
                .GreaterThan(0).WithMessage("الكمية أكبر من صفر")
                .LessThanOrEqualTo(SparePartRules.MaxQuantity).WithMessage("الكمية كبيرة جداً")
                .Must(SparePartRules.TwoDecimals).WithMessage("الكمية بخانتين عشريتين على الأكثر");
        }
    }

    public class GetSparePartReportQueryValidator : AbstractValidator<GetSparePartReportQuery>
    {
        public GetSparePartReportQueryValidator() =>
            RuleFor(x => x).Must(x => x.From == null || x.To == null || x.From.Value.Date <= x.To.Value.Date)
                .WithMessage("بداية الفترة لا تكون بعد نهايتها");
    }

    // ════════════════════ المعالج ════════════════════

    public class SparePartHandler :
        IRequestHandler<GetSparePartDepartmentsQuery, List<NamedRefDto>>,
        IRequestHandler<SearchSparePartsQuery, PagedResultDto<SparePartResponseDto>>,
        IRequestHandler<GetSparePartByIdQuery, SparePartResponseDto>,
        IRequestHandler<CreateSparePartCommand, SparePartResponseDto>,
        IRequestHandler<UpdateSparePartCommand, SparePartResponseDto>,
        IRequestHandler<DeleteSparePartCommand, Unit>,
        IRequestHandler<ReceiveSparePartCommand, SparePartResponseDto>,
        IRequestHandler<AdjustSparePartCommand, SparePartResponseDto>,
        IRequestHandler<GetSparePartMovementsQuery, PagedResultDto<SparePartMovementDto>>,
        IRequestHandler<GetSparePartReportQuery, SparePartReportDto>,
        IRequestHandler<GetRequestPartsQuery, RequestPartsDto>,
        IRequestHandler<GetAvailablePartsForRequestQuery, List<SparePartResponseDto>>,
        IRequestHandler<IssueSparePartCommand, RequestPartsDto>,
        IRequestHandler<ReturnSparePartCommand, RequestPartsDto>
    {
        private readonly ISparePartService _service;
        private readonly IMaintenanceRequestService _requestService;
        private readonly IDepartmentService _departmentService;
        private readonly IDeviceTypeService _deviceTypeService;
        private readonly IDeviceCompanyService _companyService;
        private readonly IUserService _userService;
        private readonly IUserPermissionService _permissions;
        private readonly INotificationService _notifications;

        public SparePartHandler(
            ISparePartService service,
            IMaintenanceRequestService requestService,
            IDepartmentService departmentService,
            IDeviceTypeService deviceTypeService,
            IDeviceCompanyService companyService,
            IUserService userService,
            IUserPermissionService permissions,
            INotificationService notifications)
        {
            _service = service;
            _requestService = requestService;
            _departmentService = departmentService;
            _deviceTypeService = deviceTypeService;
            _companyService = companyService;
            _userService = userService;
            _permissions = permissions;
            _notifications = notifications;
        }

        private Task<Viewer> ViewerAsync() => Viewer.CurrentAsync(_userService, _permissions);

        private async Task<SparePart> LoadAsync(int id) =>
            await _service.GetByIdAsync(id) ?? throw new KeyNotFoundException("قطعة الغيار غير موجودة");

        private async Task<SparePart> LoadInScopeAsync(int id, Viewer viewer)
        {
            var part = await LoadAsync(id);
            SparePartRules.EnsureIn(viewer, part);
            return part;
        }

        private async Task<SparePartResponseDto> ReloadDtoAsync(int id) => SparePartRules.ToDto(await LoadAsync(id));

        // ───────── القطع ─────────

        /// <summary>الأقسام التي يدير المستخدم مخزونها: قسمه، ومدير النظام كل الأقسام (لاختيار القسم في الواجهة)</summary>
        public async Task<List<NamedRefDto>> Handle(GetSparePartDepartmentsQuery request, CancellationToken ct)
        {
            var viewer = await ViewerAsync();
            if (viewer.IsSuperAdmin)
                return (await _departmentService.GetAllAsync())
                    .Select(d => new NamedRefDto { Id = d.Id, Name = d.Branch != null ? $"{d.Name} — {d.Branch.Name}" : d.Name })
                    .OrderBy(d => d.Name).ToList();

            if (viewer.User.DepartmentId is not int own) return [];
            var department = await _departmentService.GetByIdAsync(own);
            return department == null ? [] : [new NamedRefDto { Id = department.Id, Name = department.Name }];
        }

        public async Task<PagedResultDto<SparePartResponseDto>> Handle(SearchSparePartsQuery request, CancellationToken ct)
        {
            var viewer = await ViewerAsync();
            var department = SparePartRules.Department(viewer) ?? request.Filter.DepartmentId;
            var (page, pageSize) = MaintenanceRules.NormalizePaging(request.Filter.Page, request.Filter.PageSize);
            var (items, total) = await _service.SearchAsync(department, request.Filter, page, pageSize);

            return new PagedResultDto<SparePartResponseDto>
            {
                Items = items.Select(SparePartRules.ToDto).ToList(),
                TotalCount = total,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<SparePartResponseDto> Handle(GetSparePartByIdQuery request, CancellationToken ct) =>
            SparePartRules.ToDto(await LoadInScopeAsync(request.Id, await ViewerAsync()));

        private async Task EnsureCompatibilityAsync(SparePartRequestDto dto)
        {
            foreach (var id in dto.DeviceTypeIds.Distinct())
                if (!await _deviceTypeService.ExistsAsync(id)) throw new KeyNotFoundException("أحد أنواع الأجهزة المحددة غير موجود");
            foreach (var id in dto.DeviceCompanyIds.Distinct())
                if (!await _companyService.ExistsAsync(id)) throw new KeyNotFoundException("إحدى الشركات المحددة غير موجودة");
        }

        public async Task<SparePartResponseDto> Handle(CreateSparePartCommand request, CancellationToken ct)
        {
            var viewer = await ViewerAsync();
            var dto = request.Dto;

            // مدير النظام يختار القسم، وغيره يضيف إلى مخزون قسمه
            var departmentId = SparePartRules.Department(viewer)
                ?? dto.DepartmentId
                ?? throw new InvalidOperationException("اختر قسم المخزون");
            if (!await _departmentService.ExistsAsync(departmentId))
                throw new KeyNotFoundException("القسم المحدد غير موجود");

            await EnsureCompatibilityAsync(dto);

            var name = dto.Name.Trim();
            if (await _service.NameExistsAsync(departmentId, name))
                throw new InvalidOperationException("توجد قطعة بنفس الاسم في مخزون هذا القسم");

            var now = DateTime.UtcNow;
            var part = new SparePart
            {
                DepartmentId = departmentId,
                Name = name,
                PartNumber = dto.PartNumber.Trim(),
                Unit = dto.Unit.Trim(),
                Description = dto.Description.Trim(),
                MinQuantity = dto.MinQuantity,
                CreatedAt = now,
                UpdatedAt = now,
                DeviceTypes = dto.DeviceTypeIds.Distinct().Select(id => new SparePartDeviceType { DeviceTypeId = id }).ToList(),
                DeviceCompanies = dto.DeviceCompanyIds.Distinct().Select(id => new SparePartDeviceCompany { DeviceCompanyId = id }).ToList()
            };

            await _service.AddAsync(part);
            return await ReloadDtoAsync(part.Id);
        }

        public async Task<SparePartResponseDto> Handle(UpdateSparePartCommand request, CancellationToken ct)
        {
            var viewer = await ViewerAsync();
            var part = await LoadInScopeAsync(request.Id, viewer);
            var dto = request.Dto;

            await EnsureCompatibilityAsync(dto);

            var name = dto.Name.Trim();
            if (await _service.NameExistsAsync(part.DepartmentId, name, part.Id))
                throw new InvalidOperationException("توجد قطعة أخرى بنفس الاسم في مخزون هذا القسم");

            // القسم لا يتغيّر بعد الإنشاء (رصيدها وحركاتها تخصه)
            part.Name = name;
            part.PartNumber = dto.PartNumber.Trim();
            part.Unit = dto.Unit.Trim();
            part.Description = dto.Description.Trim();
            part.MinQuantity = dto.MinQuantity;
            part.UpdatedAt = DateTime.UtcNow;

            await _service.UpdateAsync(part, dto.DeviceTypeIds.Distinct().ToList(), dto.DeviceCompanyIds.Distinct().ToList());
            return await ReloadDtoAsync(part.Id);
        }

        public async Task<Unit> Handle(DeleteSparePartCommand request, CancellationToken ct)
        {
            var part = await LoadInScopeAsync(request.Id, await ViewerAsync());

            if (await _service.HasMovementsAsync(part.Id))
                throw new InvalidOperationException("لا يمكن حذف قطعة لها حركات مخزون — سجلها محفوظ");

            await _service.DeleteAsync(part);
            return Unit.Value;
        }

        // ───────── الحركات ─────────

        public async Task<SparePartResponseDto> Handle(ReceiveSparePartCommand request, CancellationToken ct)
        {
            var viewer = await ViewerAsync();
            var part = await LoadInScopeAsync(request.Id, viewer);
            var dto = request.Dto;

            await _service.ReceiveAsync(part.Id, dto.Quantity, dto.UnitCost, dto.Date ?? DateTime.Today, dto.Source.Trim(), viewer.Id);
            return await ReloadDtoAsync(part.Id);
        }

        public async Task<SparePartResponseDto> Handle(AdjustSparePartCommand request, CancellationToken ct)
        {
            var viewer = await ViewerAsync();
            var part = await LoadInScopeAsync(request.Id, viewer);

            var change = await _service.AdjustAsync(part.Id, request.Dto.Delta, request.Dto.Reason.Trim(), viewer.Id);
            if (SparePartRules.CrossedBelowMinimum(part, change))
                await MaintenanceNotifier.LowStockAsync(_notifications, _permissions, part, change.After, viewer.User);

            return await ReloadDtoAsync(part.Id);
        }

        public async Task<PagedResultDto<SparePartMovementDto>> Handle(GetSparePartMovementsQuery request, CancellationToken ct)
        {
            var part = await LoadInScopeAsync(request.Id, await ViewerAsync());
            var (page, pageSize) = MaintenanceRules.NormalizePaging(request.Page, request.PageSize);
            var (items, total) = await _service.GetMovementsAsync(part.Id, page, pageSize);

            return new PagedResultDto<SparePartMovementDto>
            {
                Items = items.Select(m => new SparePartMovementDto
                {
                    Id = m.Id,
                    Type = (int)m.Type,
                    TypeAr = SparePartRules.MovementAr(m.Type),
                    Quantity = m.Quantity,
                    UnitCost = m.UnitCost,
                    BalanceAfter = m.BalanceAfter,
                    Date = m.Date,
                    Note = m.Note,
                    MaintenanceRequestId = m.MaintenanceRequestId,
                    RequestNumber = m.MaintenanceRequest != null ? MaintenanceRules.RequestNumber(m.MaintenanceRequest.Id, m.MaintenanceRequest.CreatedAt) : null,
                    UserName = m.User?.FullName ?? string.Empty,
                    CreatedAt = m.CreatedAt
                }).ToList(),
                TotalCount = total,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<SparePartReportDto> Handle(GetSparePartReportQuery request, CancellationToken ct)
        {
            var viewer = await ViewerAsync();
            var department = SparePartRules.Department(viewer) ?? request.DepartmentId;
            var to = request.To?.Date ?? DateTime.Today;
            var from = request.From?.Date ?? new DateTime(to.Year, to.Month, 1);
            return await _service.GetReportAsync(department, from, to);
        }

        // ───────── قطع طلب الصيانة ─────────

        private async Task<MaintenanceRequest> LoadRequestAsync(int id) =>
            await _requestService.GetByIdAsync(id) ?? throw new KeyNotFoundException("طلب الصيانة غير موجود");

        private async Task<bool> CanIssueAsync(MaintenanceRequest r) =>
            r.DepartmentId != null && !MaintenanceRules.IsClosed(r)
            && MaintenanceRules.In(await MaintenanceRules.BoundaryAsync(_userService, _permissions, AppPermissions.IssueSparePart), r);

        /// <summary>الصرف والإرجاع: الصلاحية بحدّها، والطلب مفتوح وله قسم (مخزونه)</summary>
        private async Task EnsureCanIssueAsync(MaintenanceRequest r)
        {
            MaintenanceRules.Ensure(
                MaintenanceRules.In(await MaintenanceRules.BoundaryAsync(_userService, _permissions, AppPermissions.IssueSparePart), r),
                "لا تملك صرف القطع على هذا الطلب");
            MaintenanceRules.EnsureOpen(r);
            if (r.DepartmentId == null)
                throw new InvalidOperationException("الطلب لا يتبع لقسم له مخزون قطع غيار");
        }

        private async Task<RequestPartsDto> RequestPartsAsync(MaintenanceRequest r)
        {
            var parts = await _service.GetRequestPartsAsync(r.Id);
            var lifetime = await _service.GetDeviceCostAsync(r.DeviceMaintenanceId);

            return new RequestPartsDto
            {
                Items = parts.Select(p => new MaintenanceRequestPartDto
                {
                    Id = p.Id,
                    SparePartId = p.SparePartId,
                    PartName = p.SparePart?.Name ?? string.Empty,
                    PartNumber = p.SparePart?.PartNumber ?? string.Empty,
                    Unit = p.SparePart?.Unit ?? string.Empty,
                    Quantity = p.Quantity,
                    UnitCost = p.UnitCost,
                    Total = Math.Round(p.Quantity * p.UnitCost, 2),
                    IssuedByName = p.IssuedBy?.FullName ?? string.Empty,
                    IssuedAt = p.IssuedAt
                }).ToList(),
                Total = Math.Round(parts.Sum(p => p.Quantity * p.UnitCost), 2),
                DeviceLifetimeCost = Math.Round(lifetime, 2),
                CanIssue = await CanIssueAsync(r)
            };
        }

        public async Task<RequestPartsDto> Handle(GetRequestPartsQuery request, CancellationToken ct)
        {
            var r = await LoadRequestAsync(request.RequestId);
            MaintenanceRules.Ensure(
                MaintenanceRules.In(await MaintenanceRules.BoundaryAsync(_userService, _permissions, "ViewMaintenanceRequests"), r),
                "لا يمكنك عرض طلب صيانة خارج نطاقك");
            return await RequestPartsAsync(r);
        }

        public async Task<List<SparePartResponseDto>> Handle(GetAvailablePartsForRequestQuery request, CancellationToken ct)
        {
            var r = await LoadRequestAsync(request.RequestId);
            await EnsureCanIssueAsync(r);

            var parts = await _service.GetAvailableForRequestAsync(r.DepartmentId!.Value,
                r.DeviceMaintenance.DeviceTypeId, r.DeviceMaintenance.DeviceCompanyId, request.Search, 30);
            return parts.Select(SparePartRules.ToDto).ToList();
        }

        public async Task<RequestPartsDto> Handle(IssueSparePartCommand request, CancellationToken ct)
        {
            var viewer = await ViewerAsync();
            var r = await LoadRequestAsync(request.RequestId);
            await EnsureCanIssueAsync(r);

            var part = await LoadAsync(request.Dto.SparePartId);
            if (part.DepartmentId != r.DepartmentId)
                throw new InvalidOperationException("القطعة ليست من مخزون قسم الطلب");

            var (issued, change) = await _service.IssueAsync(r.Id, part.Id, request.Dto.Quantity, viewer.Id);

            await LogAsync(r, viewer.User, MaintenanceActivityType.PartIssued,
                $"صرف {SparePartRules.Amount(issued.Quantity)} {part.Unit} من «{part.Name}» ({SparePartRules.Amount(issued.Quantity * issued.UnitCost)} ل.س)");

            if (SparePartRules.CrossedBelowMinimum(part, change))
                await MaintenanceNotifier.LowStockAsync(_notifications, _permissions, part, change.After, viewer.User);

            return await RequestPartsAsync(r);
        }

        public async Task<RequestPartsDto> Handle(ReturnSparePartCommand request, CancellationToken ct)
        {
            var viewer = await ViewerAsync();
            var requestPart = await _service.GetRequestPartAsync(request.RequestPartId)
                ?? throw new KeyNotFoundException("القطعة غير موجودة على الطلب");
            var r = await LoadRequestAsync(requestPart.MaintenanceRequestId);
            await EnsureCanIssueAsync(r);

            var (quantity, unit, name) = (requestPart.Quantity, requestPart.SparePart.Unit, requestPart.SparePart.Name);
            await _service.ReturnAsync(requestPart, viewer.Id);

            await LogAsync(r, viewer.User, MaintenanceActivityType.PartReturned,
                $"أعاد {SparePartRules.Amount(quantity)} {unit} من «{name}» إلى المخزون");

            return await RequestPartsAsync(r);
        }

        private Task LogAsync(MaintenanceRequest request, User actor, MaintenanceActivityType type, string text) =>
            _requestService.AddActivityAsync(new MaintenanceRequestActivity
            {
                MaintenanceRequestId = request.Id,
                UserId = actor.Id,
                Type = type,
                Text = text,
                CreatedAt = DateTime.UtcNow
            });
    }
}
