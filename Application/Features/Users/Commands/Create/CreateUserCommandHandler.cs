using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
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
            // البريد وكلمة المرور والدور لحساب النظام وحده؛ الموظف بلا دخول يُحفظ بدونها مهما أُرسل
            var account = request.UserDto.Kind == UserKind.Account;
            var email = account ? request.UserDto.Email!.Trim() : null;
            var roleId = account ? request.UserDto.RoleId : null;

            if (email != null && !await _userService.IsEmailUniqueAsync(email))
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
                roleId);

            await UserRules.EnsureCanManageUserAsync(
                _currentUserService,
                _roleService,
                _rolePermissionService,
                placement.BranchId,
                roleId);

            var user = new User
            {
                Kind = request.UserDto.Kind,
                FullName = request.UserDto.FullName,
                Email = email,
                PersonalIdNumber = personalId,
                PhoneNumber = Application.Common.PhoneRules.ToStored(request.UserDto.PhoneNumber),
                PasswordHash = account ? _passwordHasher.Hash(request.UserDto.Password!) : null,
                RoleId = roleId,
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
