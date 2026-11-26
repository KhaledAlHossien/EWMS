using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using MediatR;

namespace Application.Features.Maintenance.SpareParts.Commands.Update
{
    public class UpdateSparePartCommandHandler : IRequestHandler<UpdateSparePartCommand, SparePartResponseDto>
    {
        private readonly ISparePartService _service;
        private readonly IUserService _userService;
        private readonly IUserPermissionService _permissions;
        private readonly SparePartSupport _support;

        public UpdateSparePartCommandHandler(ISparePartService service, IUserService userService, IUserPermissionService permissions, SparePartSupport support)
        {
            _service = service;
            _userService = userService;
            _permissions = permissions;
            _support = support;
        }

        public async Task<SparePartResponseDto> Handle(UpdateSparePartCommand request, CancellationToken ct)
        {
            var viewer = await ViewerAsync();
            var part = await _support.LoadInScopeAsync(request.Id, viewer);
            var dto = request.Dto;

            await _support.EnsureCompatibilityAsync(dto);

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
            return await _support.ReloadDtoAsync(part.Id);
        }

        private Task<Viewer> ViewerAsync() => Viewer.CurrentAsync(_userService, _permissions);
    }
}
