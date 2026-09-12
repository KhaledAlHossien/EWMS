using Application.Interfaces;
using MediatR;

namespace Application.Features.Roles.Commands.Delete
{
    public class DeleteRoleCommandHandler : IRequestHandler<DeleteRoleCommand, bool>
    {
        private readonly IRoleService _roleService;
        private readonly IRolePermissionService _rolePermissionService;
        private readonly ICurrentUserService _currentUserService;

        public DeleteRoleCommandHandler(
            IRoleService roleService,
            IRolePermissionService rolePermissionService,
            ICurrentUserService currentUserService)
        {
            _roleService = roleService;
            _rolePermissionService = rolePermissionService;
            _currentUserService = currentUserService;
        }

        public async Task<bool> Handle(DeleteRoleCommand request, CancellationToken cancellationToken)
        {
            var role = await _roleService.GetByIdAsync(request.Id)
                ?? throw new KeyNotFoundException("الدور غير موجود");

            if (role.Name.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase)
                && !_currentUserService.Role.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("لا يمكن حذف دور SuperAdmin إلا من SuperAdmin");

            if (await _roleService.IsRoleUsedAsync(role.Id))
                throw new InvalidOperationException("لا يمكن حذف دور مرتبط بمستخدمين");

            await _rolePermissionService.RemoveByRoleAsync(role.Id);
            return await _roleService.DeleteAsync(role);
        }
    }
}
