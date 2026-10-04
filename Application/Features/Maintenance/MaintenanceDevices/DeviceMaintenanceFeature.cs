using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities.Maintenance;
using FluentValidation;
using MediatR;

namespace Application.Features.Maintenance.MaintenanceDevices
{
    // أجهزة الصيانة سجل مشترك لمن يملك الصلاحية (قرار المستخدم 2026-10-04: تُمنح لموظفي قسم الصيانة فقط)،
    // فلا حدّ قسم هنا — طلبات كل جهاز تبقى محصورة بنطاق الطلبات (MaintenanceRequests/GetAll?deviceMaintenanceId=)
    public record SearchDeviceMaintenancesQuery(DeviceMaintenanceFilterDto Filter) : IRequest<PagedResultDto<DeviceMaintenanceResponseDto>>;
    public record GetDeviceMaintenanceByIdQuery(int Id) : IRequest<DeviceMaintenanceResponseDto>;
    public record GetDeviceMaintenanceBySerialQuery(string SerialNumber) : IRequest<DeviceMaintenanceResponseDto>;
    public record CreateDeviceMaintenanceCommand(DeviceMaintenanceRequestDto Dto) : IRequest<DeviceMaintenanceResponseDto>;
    public record UpdateDeviceMaintenanceCommand(int Id, DeviceMaintenanceRequestDto Dto) : IRequest<DeviceMaintenanceResponseDto>;
    public record DeleteDeviceMaintenanceCommand(int Id) : IRequest<Unit>;

    // ════════════════════ التحقق من المدخلات ════════════════════

    public class DeviceMaintenanceRequestDtoValidator : AbstractValidator<DeviceMaintenanceRequestDto>
    {
        public DeviceMaintenanceRequestDtoValidator()
        {
            RuleFor(x => x.SerialNumber)
                .NotEmpty().WithMessage("الرقم التسلسلي مطلوب")
                .MaximumLength(100).WithMessage("الرقم التسلسلي لا يتجاوز 100 حرف");

            RuleFor(x => x.Name).MaximumLength(200).WithMessage("اسم الجهاز لا يتجاوز 200 حرف");
            RuleFor(x => x.Model).MaximumLength(100).WithMessage("الموديل لا يتجاوز 100 حرف");
            RuleFor(x => x.Description).MaximumLength(1000).WithMessage("الوصف لا يتجاوز 1000 حرف");

            RuleFor(x => x.DeviceTypeId).GreaterThan(0).WithMessage("يجب اختيار نوع الجهاز");
            RuleFor(x => x.DeviceCompanyId).GreaterThan(0).WithMessage("يجب اختيار الشركة المصنعة");
        }
    }

    public class CreateDeviceMaintenanceCommandValidator : AbstractValidator<CreateDeviceMaintenanceCommand>
    {
        public CreateDeviceMaintenanceCommandValidator() =>
            RuleFor(x => x.Dto).SetValidator(new DeviceMaintenanceRequestDtoValidator());
    }

    public class UpdateDeviceMaintenanceCommandValidator : AbstractValidator<UpdateDeviceMaintenanceCommand>
    {
        public UpdateDeviceMaintenanceCommandValidator() =>
            RuleFor(x => x.Dto).SetValidator(new DeviceMaintenanceRequestDtoValidator());
    }

    public class GetDeviceMaintenanceBySerialQueryValidator : AbstractValidator<GetDeviceMaintenanceBySerialQuery>
    {
        public GetDeviceMaintenanceBySerialQueryValidator() =>
            RuleFor(x => x.SerialNumber).NotEmpty().WithMessage("اكتب الرقم التسلسلي");
    }

    // ════════════════════ المعالج ════════════════════

    public class DeviceMaintenanceHandler :
        IRequestHandler<SearchDeviceMaintenancesQuery, PagedResultDto<DeviceMaintenanceResponseDto>>,
        IRequestHandler<GetDeviceMaintenanceByIdQuery, DeviceMaintenanceResponseDto>,
        IRequestHandler<GetDeviceMaintenanceBySerialQuery, DeviceMaintenanceResponseDto>,
        IRequestHandler<CreateDeviceMaintenanceCommand, DeviceMaintenanceResponseDto>,
        IRequestHandler<UpdateDeviceMaintenanceCommand, DeviceMaintenanceResponseDto>,
        IRequestHandler<DeleteDeviceMaintenanceCommand, Unit>
    {
        private readonly IDeviceMaintenanceService _service;
        private readonly IDeviceTypeService _deviceTypeService;
        private readonly IDeviceCompanyService _companyService;
        private readonly IMapper _mapper;

        public DeviceMaintenanceHandler(
            IDeviceMaintenanceService service,
            IDeviceTypeService deviceTypeService,
            IDeviceCompanyService companyService,
            IMapper mapper)
        {
            _service = service;
            _deviceTypeService = deviceTypeService;
            _companyService = companyService;
            _mapper = mapper;
        }

        private async Task<DeviceMaintenance> LoadAsync(int id) =>
            await _service.GetByIdAsync(id) ?? throw new KeyNotFoundException("الجهاز غير موجود");

        private async Task EnsureReferencesAsync(DeviceMaintenanceRequestDto dto)
        {
            if (!await _deviceTypeService.ExistsAsync(dto.DeviceTypeId))
                throw new KeyNotFoundException("نوع الجهاز المحدد غير موجود");
            if (!await _companyService.ExistsAsync(dto.DeviceCompanyId))
                throw new KeyNotFoundException("الشركة المصنعة المحددة غير موجودة");
        }

        // الرقم التسلسلي والموديل يُبحث عنهما بـ"يبدأ بـ" فيُحفظان بلا فراغات زائدة
        private static void Trim(DeviceMaintenance d)
        {
            d.Name = d.Name.Trim();
            d.SerialNumber = d.SerialNumber.Trim();
            d.Model = d.Model.Trim();
            d.Description = d.Description.Trim();
        }

        public async Task<PagedResultDto<DeviceMaintenanceResponseDto>> Handle(SearchDeviceMaintenancesQuery request, CancellationToken ct)
        {
            var (page, pageSize) = MaintenanceRules.NormalizePaging(request.Filter.Page, request.Filter.PageSize);
            var (items, total) = await _service.SearchAsync(request.Filter, page, pageSize);

            return new PagedResultDto<DeviceMaintenanceResponseDto>
            {
                Items = _mapper.Map<List<DeviceMaintenanceResponseDto>>(items),
                TotalCount = total,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<DeviceMaintenanceResponseDto> Handle(GetDeviceMaintenanceByIdQuery request, CancellationToken ct) =>
            _mapper.Map<DeviceMaintenanceResponseDto>(await LoadAsync(request.Id));

        public async Task<DeviceMaintenanceResponseDto> Handle(GetDeviceMaintenanceBySerialQuery request, CancellationToken ct)
        {
            var device = await _service.GetBySerialAsync(request.SerialNumber.Trim())
                ?? throw new KeyNotFoundException("لا يوجد جهاز بهذا الرقم التسلسلي — أضفه أولاً");

            return _mapper.Map<DeviceMaintenanceResponseDto>(device);
        }

        public async Task<DeviceMaintenanceResponseDto> Handle(CreateDeviceMaintenanceCommand request, CancellationToken ct)
        {
            await EnsureReferencesAsync(request.Dto);

            var entity = _mapper.Map<DeviceMaintenance>(request.Dto);
            Trim(entity);

            if (await _service.ExistsBySerialAsync(entity.SerialNumber))
                throw new InvalidOperationException("يوجد جهاز بنفس الرقم التسلسلي مسبقاً — اختره بدل إضافته من جديد");

            var created = await _service.AddAsync(entity);
            return _mapper.Map<DeviceMaintenanceResponseDto>(await LoadAsync(created.Id));
        }

        public async Task<DeviceMaintenanceResponseDto> Handle(UpdateDeviceMaintenanceCommand request, CancellationToken ct)
        {
            var entity = await LoadAsync(request.Id);
            await EnsureReferencesAsync(request.Dto);

            _mapper.Map(request.Dto, entity);
            Trim(entity);

            if (await _service.ExistsBySerialAsync(entity.SerialNumber, request.Id))
                throw new InvalidOperationException("يوجد جهاز آخر بنفس الرقم التسلسلي");

            await _service.UpdateAsync(entity);
            return _mapper.Map<DeviceMaintenanceResponseDto>(await LoadAsync(entity.Id));
        }

        public async Task<Unit> Handle(DeleteDeviceMaintenanceCommand request, CancellationToken ct)
        {
            var entity = await LoadAsync(request.Id);

            if (await _service.HasRequestsAsync(request.Id))
                throw new InvalidOperationException("لا يمكن حذف جهاز له طلبات صيانة — سجل إصلاحاته محفوظ");

            await _service.DeleteAsync(entity);
            return Unit.Value;
        }
    }
}
