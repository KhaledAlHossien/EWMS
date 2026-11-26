using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities;
using Domain.Entities.Maintenance;

namespace Application.Features.Maintenance.SpareParts
{
    /// <summary>الدوال المشتركة بين عمليات هذه الميزة</summary>
    public class SparePartSupport
    {
        private readonly ISparePartService _service;
        private readonly IMaintenanceRequestService _requestService;
        private readonly IDeviceTypeService _deviceTypeService;
        private readonly IDeviceCompanyService _companyService;
        private readonly IUserService _userService;
        private readonly IUserPermissionService _permissions;

        public SparePartSupport(ISparePartService service, IMaintenanceRequestService requestService, IDeviceTypeService deviceTypeService, IDeviceCompanyService companyService, IUserService userService, IUserPermissionService permissions)
        {
            _service = service;
            _requestService = requestService;
            _deviceTypeService = deviceTypeService;
            _companyService = companyService;
            _userService = userService;
            _permissions = permissions;
        }

        public async Task<SparePart> LoadAsync(int id) =>
            await _service.GetByIdAsync(id) ?? throw new KeyNotFoundException("قطعة الغيار غير موجودة");

        public async Task<SparePart> LoadInScopeAsync(int id, Viewer viewer)
        {
            var part = await LoadAsync(id);
            SparePartRules.EnsureIn(viewer, part);
            return part;
        }

        public async Task<SparePartResponseDto> ReloadDtoAsync(int id) => SparePartRules.ToDto(await LoadAsync(id));

        public async Task EnsureCompatibilityAsync(SparePartRequestDto dto)
        {
            foreach (var id in dto.DeviceTypeIds.Distinct())
                if (!await _deviceTypeService.ExistsAsync(id)) throw new KeyNotFoundException("أحد أنواع الأجهزة المحددة غير موجود");
            foreach (var id in dto.DeviceCompanyIds.Distinct())
                if (!await _companyService.ExistsAsync(id)) throw new KeyNotFoundException("إحدى الشركات المحددة غير موجودة");
        }

        public async Task<bool> CanIssueAsync(MaintenanceRequest r) =>
            r.DepartmentId != null && !MaintenanceRules.IsClosed(r)
            && MaintenanceRules.In(await MaintenanceRules.BoundaryAsync(_userService, _permissions, AppPermissions.IssueSparePart), r);

        /// <summary>الصرف والإرجاع: الصلاحية بحدّها، والطلب مفتوح وله قسم (مخزونه)</summary>
        public async Task EnsureCanIssueAsync(MaintenanceRequest r)
        {
            MaintenanceRules.Ensure(
                MaintenanceRules.In(await MaintenanceRules.BoundaryAsync(_userService, _permissions, AppPermissions.IssueSparePart), r),
                "لا تملك صرف القطع على هذا الطلب");
            MaintenanceRules.EnsureOpen(r);
            if (r.DepartmentId == null)
                throw new InvalidOperationException("الطلب لا يتبع لقسم له مخزون قطع غيار");
        }

        public async Task<RequestPartsDto> RequestPartsAsync(MaintenanceRequest r)
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

        public Task LogAsync(MaintenanceRequest request, User actor, MaintenanceActivityType type, string text) =>
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
