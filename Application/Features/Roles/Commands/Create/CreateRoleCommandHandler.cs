using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities;
using MediatR;

namespace Application.Features.Roles.Commands.Create
{
    public class CreateRoleCommandHandler : IRequestHandler<CreateRoleCommand, RoleResponseDto>
    {
        private readonly IRoleService _roleService;
        private readonly IPermissionService _permissionService;
        private readonly IRolePermissionService _rolePermissionService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IMapper _mapper;

        public CreateRoleCommandHandler(
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

        public async Task<RoleResponseDto> Handle(CreateRoleCommand request, CancellationToken cancellationToken)
        {
            if (await _roleService.ExistsAsync(request.RoleDto.Name))
                throw new InvalidOperationException("يوجد دور بنفس الاسم مسبقاً");

            if (!_currentUserService.Role.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase)
                && request.RoleDto.Name.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("فقط SuperAdmin يمكنه إنشاء دور SuperAdmin");

            await EnsureCanGrantPermissions(request.RoleDto.PermissionIds);
            await EnsurePermissionsExist(request.RoleDto.PermissionIds);

            var role = new Role { Name = request.RoleDto.Name };
            await _roleService.AddAsync(role);

            var rolePermissions = request.RoleDto.PermissionIds.Distinct().Select(permissionId => new RolePermission
            {
                RoleId = role.Id,
                PermissionId = permissionId
            });

            await _rolePermissionService.AddRangeAsync(rolePermissions);

            return await BuildResponse(role);
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

        private async Task<RoleResponseDto> BuildResponse(Role role)
        {
            var response = _mapper.Map<RoleResponseDto>(role);
            var rolePermissions = await _rolePermissionService.GetByRoleAsync(role.Id);
            response.Permissions = _mapper.Map<List<PermissionResponseDto>>(
                rolePermissions.Select(rp => rp.Permission));
            return response;
        }
    }
}
