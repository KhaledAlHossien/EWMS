using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities;
using MediatR;

namespace Application.Features.Roles.Commands.Update
{
    public class UpdateRoleCommandHandler : IRequestHandler<UpdateRoleCommand, RoleResponseDto>
    {
        private readonly IRoleService _roleService;
        private readonly IPermissionService _permissionService;
        private readonly IRolePermissionService _rolePermissionService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IMapper _mapper;

        public UpdateRoleCommandHandler(
            IRoleService roleService,
            IPermissionService permissionService,
            IRolePermissionService rolePermissionService,
            ICurrentUserService currentUserService,
            IMapper mapper)
        {
            _roleService = roleService;
            _permissionService = permissionService;
            _rolePermissionService = rolePermissionService;
            _currentUserService = currentUserService;
            _mapper = mapper;
        }

        public async Task<RoleResponseDto> Handle(UpdateRoleCommand request, CancellationToken cancellationToken)
        {
            var role = await _roleService.GetByIdAsync(request.Id)
                ?? throw new KeyNotFoundException("الدور غير موجود");

            var isSuperAdmin = _currentUserService.Role.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase);
            if (!isSuperAdmin && role.Name.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("لا يمكن تعديل دور SuperAdmin إلا من SuperAdmin");

            if (!isSuperAdmin && request.RoleDto.Name.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("فقط SuperAdmin يمكنه إنشاء أو تعديل دور SuperAdmin");

            if (await _roleService.ExistsAsync(request.RoleDto.Name, request.Id))
                throw new InvalidOperationException("يوجد دور آخر بنفس الاسم");

            await EnsureCanGrantPermissions(request.RoleDto.PermissionIds);
            await EnsurePermissionsExist(request.RoleDto.PermissionIds);

            role.Name = request.RoleDto.Name;
            await _roleService.UpdateAsync(role);

            await _rolePermissionService.RemoveByRoleAsync(role.Id);
            var rolePermissions = request.RoleDto.PermissionIds.Distinct().Select(permissionId => new RolePermission
            {
                RoleId = role.Id,
                PermissionId = permissionId
            });
            await _rolePermissionService.AddRangeAsync(rolePermissions);

            var response = _mapper.Map<RoleResponseDto>(role);
            response.Permissions = _mapper.Map<List<PermissionResponseDto>>(
                (await _rolePermissionService.GetByRoleAsync(role.Id)).Select(rp => rp.Permission));
            return response;
        }

        private async Task EnsurePermissionsExist(IEnumerable<int> permissionIds)
        {
            foreach (var permissionId in permissionIds.Distinct())
            {
                if (await _permissionService.GetByIdAsync(permissionId) == null)
                    throw new KeyNotFoundException($"الصلاحية رقم {permissionId} غير موجودة");
            }
        }

        private async Task EnsureCanGrantPermissions(IEnumerable<int> permissionIds)
        {
            if (_currentUserService.Role.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase))
                return;

            foreach (var permissionId in permissionIds.Distinct())
            {
                var permission = await _permissionService.GetByIdAsync(permissionId)
                    ?? throw new KeyNotFoundException($"الصلاحية رقم {permissionId} غير موجودة");

                if (permission.Name.Equals("ManageBranches", StringComparison.OrdinalIgnoreCase))
                    throw new UnauthorizedAccessException("لا يمكن لرئيس الفرع منح صلاحية إدارة الفروع");
            }
        }
    }
}
