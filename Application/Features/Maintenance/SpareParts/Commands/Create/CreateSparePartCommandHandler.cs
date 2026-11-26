using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities.Maintenance;
using MediatR;

namespace Application.Features.Maintenance.SpareParts.Commands.Create
{
    public class CreateSparePartCommandHandler : IRequestHandler<CreateSparePartCommand, SparePartResponseDto>
    {
        private readonly ISparePartService _service;
        private readonly IDepartmentService _departmentService;
        private readonly IUserService _userService;
        private readonly IUserPermissionService _permissions;
        private readonly SparePartSupport _support;

        public CreateSparePartCommandHandler(ISparePartService service, IDepartmentService departmentService, IUserService userService, IUserPermissionService permissions, SparePartSupport support)
        {
            _service = service;
            _departmentService = departmentService;
            _userService = userService;
            _permissions = permissions;
            _support = support;
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

            await _support.EnsureCompatibilityAsync(dto);

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
            return await _support.ReloadDtoAsync(part.Id);
        }

        private Task<Viewer> ViewerAsync() => Viewer.CurrentAsync(_userService, _permissions);
    }
}
