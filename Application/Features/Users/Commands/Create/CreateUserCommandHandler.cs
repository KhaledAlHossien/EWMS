using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities;
using MediatR;

namespace Application.Features.Users.Commands.Create
{
    public class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, UserResponseDto>
    {
        private readonly IUserService _userService;
        private readonly IBranchService _branchService;
        private readonly IDepartmentService _departmentService;
        private readonly IOfficeService _officeService;
        private readonly IRoleService _roleService;
        private readonly IRolePermissionService _rolePermissionService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IPasswordHasher _passwordHasher;
        private readonly IMapper _mapper;

        public CreateUserCommandHandler(
            IUserService userService,
            IBranchService branchService,
            IDepartmentService departmentService,
            IOfficeService officeService,
            IRoleService roleService,
            IRolePermissionService rolePermissionService,
            ICurrentUserService currentUserService,
            IPasswordHasher passwordHasher,
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
            _mapper = mapper;
        }

        public async Task<UserResponseDto> Handle(CreateUserCommand request, CancellationToken cancellationToken)
        {
            if (!await _userService.IsEmailUniqueAsync(request.UserDto.Email))
                throw new InvalidOperationException("البريد الإلكتروني مستخدم مسبقاً");

            var personalId = UserRules.NormalizePersonalIdNumber(request.UserDto.PersonalIdNumber);
            if (personalId != null && !await _userService.IsPersonalIdNumberUniqueAsync(personalId))
                throw new InvalidOperationException("الرقم الذاتي مسجّل لموظف آخر");

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

            var user = new User
            {
                FullName = request.UserDto.FullName,
                Email = request.UserDto.Email,
                PersonalIdNumber = personalId,
                PhoneNumber = Application.Common.PhoneRules.ToStored(request.UserDto.PhoneNumber),
                PasswordHash = _passwordHasher.Hash(request.UserDto.Password),
                RoleId = request.UserDto.RoleId,
                DepartmentId = placement.DepartmentId,
                OfficeId = placement.OfficeId,
                BranchId = placement.BranchId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            await _userService.AddAsync(user);
            var created = await _userService.GetWithDetailsAsync(user.Id) ?? user;
            return _mapper.Map<UserResponseDto>(created);
        }
    }
}
