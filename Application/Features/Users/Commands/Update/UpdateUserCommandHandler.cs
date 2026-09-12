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
        private readonly IRoleService _roleService;
        private readonly IRolePermissionService _rolePermissionService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IPasswordHasher _passwordHasher;
        private readonly IMapper _mapper;

        public UpdateUserCommandHandler(
            IUserService userService,
            IBranchService branchService,
            IDepartmentService departmentService,
            IRoleService roleService,
            IRolePermissionService rolePermissionService,
            ICurrentUserService currentUserService,
            IPasswordHasher passwordHasher,
            IMapper mapper)
        {
            _userService = userService;
            _branchService = branchService;
            _departmentService = departmentService;
            _roleService = roleService;
            _rolePermissionService = rolePermissionService;
            _currentUserService = currentUserService;
            _passwordHasher = passwordHasher;
            _mapper = mapper;
        }

        public async Task<UserResponseDto> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
        {
            var user = await _userService.GetWithDetailsAsync(request.Id)
                ?? throw new KeyNotFoundException("المستخدم غير موجود");

            UserRules.EnsureCanChangeExistingUser(_currentUserService, user);

            if (!await _userService.IsEmailUniqueAsync(request.UserDto.Email, request.Id))
                throw new InvalidOperationException("البريد الإلكتروني مستخدم مسبقاً");

            await UserRules.EnsureUserReferencesAsync(
                _branchService,
                _departmentService,
                _roleService,
                request.UserDto.BranchId,
                request.UserDto.DepartmentId,
                request.UserDto.RoleId);

            await UserRules.EnsureCanManageUserAsync(
                _currentUserService,
                _roleService,
                _rolePermissionService,
                request.UserDto.BranchId,
                request.UserDto.RoleId);

            user.FullName = request.UserDto.FullName;
            user.Email = request.UserDto.Email;
            user.RoleId = request.UserDto.RoleId;
            user.DepartmentId = request.UserDto.DepartmentId;
            user.BranchId = request.UserDto.BranchId;
            user.IsActive = request.UserDto.IsActive;

            if (!string.IsNullOrWhiteSpace(request.UserDto.Password))
                user.PasswordHash = _passwordHasher.Hash(request.UserDto.Password);

            await _userService.UpdateAsync(user);
            var updated = await _userService.GetWithDetailsAsync(user.Id) ?? user;
            return _mapper.Map<UserResponseDto>(updated);
        }
    }
}
