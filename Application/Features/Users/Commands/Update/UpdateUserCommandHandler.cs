using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using MediatR;

namespace Application.Features.Users.Commands.Update
{
    public class UpdateUserCommandHandler : IRequestHandler<UpdateUserCommand, UserResponseDto>
    {
        private readonly IUserService _userService;
        private readonly IBranchService _branchService;
        private readonly IDepartmentService _departmentService;
        private readonly IOfficeService _officeService;
        private readonly IRoleService _roleService;
        private readonly IRolePermissionService _rolePermissionService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IPasswordHasher _passwordHasher;
        private readonly IUserPermissionService _permissions;
        private readonly IMapper _mapper;

        public UpdateUserCommandHandler(
            IUserService userService,
            IBranchService branchService,
            IDepartmentService departmentService,
            IOfficeService officeService,
            IRoleService roleService,
            IRolePermissionService rolePermissionService,
            ICurrentUserService currentUserService,
            IPasswordHasher passwordHasher,
            IUserPermissionService permissions,
            IMapper mapper)
        {
            _userService = userService;
            _branchService = branchService;
            _departmentService = departmentService;
            _officeService = officeService;
            _roleService = roleService;
            _rolePermissionService = rolePermissionService;
            _currentUserService = currentUserService;
            _passwordHasher = passwordHasher;
            _permissions = permissions;
            _mapper = mapper;
        }

        public async Task<UserResponseDto> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
        {
            var user = await _userService.GetWithDetailsAsync(request.Id)
                ?? throw new KeyNotFoundException("المستخدم غير موجود");

            UserRules.EnsureCanChangeExistingUser(_currentUserService, user);

            if (!await _userService.IsEmailUniqueAsync(request.UserDto.Email, request.Id))
                throw new InvalidOperationException("البريد الإلكتروني مستخدم مسبقاً");

            var placement = await UserRules.EnsureUserReferencesAsync(
                _branchService,
                _departmentService,
                _officeService,
                _roleService,
                request.UserDto.BranchId,
                request.UserDto.DepartmentId,
                request.UserDto.OfficeId,
                request.UserDto.RoleId);

            await UserRules.EnsureCanManageUserAsync(
                _currentUserService,
                _roleService,
                _rolePermissionService,
                placement.BranchId,
                request.UserDto.RoleId);

            user.FullName = request.UserDto.FullName;
            user.Email = request.UserDto.Email;
            user.RoleId = request.UserDto.RoleId;
            user.DepartmentId = placement.DepartmentId;
            user.OfficeId = placement.OfficeId;
            user.BranchId = placement.BranchId;
            // تفعيل الحساب أو تعطيله صلاحية منفصلة عن تعديل البيانات (مدير النظام يملكها دائماً)
            if (request.UserDto.IsActive != user.IsActive
                && !_currentUserService.Role.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase)
                && !await _permissions.HasAsync(_currentUserService.UserId, AppPermissions.ToggleUserActive))
                throw new UnauthorizedAccessException("لا تملك صلاحية تفعيل الحسابات أو تعطيلها");

            user.IsActive = request.UserDto.IsActive;

            if (!string.IsNullOrWhiteSpace(request.UserDto.Password))
                user.PasswordHash = _passwordHasher.Hash(request.UserDto.Password);

            await _userService.UpdateAsync(user);
            var updated = await _userService.GetWithDetailsAsync(user.Id) ?? user;
            return _mapper.Map<UserResponseDto>(updated);
        }
    }
}
