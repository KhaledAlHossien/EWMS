using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using Domain.Enums;
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

            await UserRules.EnsureCanChangeExistingUserAsync(_currentUserService, user);
            UserRules.EnsureNotDailyWorker(user);

            // البريد وكلمة المرور والدور لحساب النظام وحده: التحويل إلى «بلا دخول» يمحوها، والتحويل إلى حساب يطلب كلمة مرور
            var account = request.UserDto.Kind == UserKind.Account;
            var email = account ? request.UserDto.Email!.Trim() : null;
            var roleId = account ? request.UserDto.RoleId : null;
            if (account && user.PasswordHash == null && string.IsNullOrWhiteSpace(request.UserDto.Password))
                throw new InvalidOperationException("كلمة المرور مطلوبة لتحويل الموظف إلى حساب يدخل النظام");

            var personalId = UserRules.NormalizePersonalIdNumber(request.UserDto.PersonalIdNumber);
            if (personalId != null && !await _userService.IsPersonalIdNumberUniqueAsync(personalId, request.Id))
                throw new InvalidOperationException("الرقم الذاتي مسجّل لموظف آخر");

            if (email != null && !await _userService.IsEmailUniqueAsync(email, request.Id))
                throw new InvalidOperationException("البريد الإلكتروني مستخدم مسبقاً");

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

            // تغيير النوع أو الدور أو المكان أو التعطيل أو كلمة المرور يُنهي جلسات الموظف المفتوحة
            var sensitive = user.Kind != request.UserDto.Kind
                || user.RoleId != roleId
                || user.BranchId != placement.BranchId
                || user.DepartmentId != placement.DepartmentId
                || user.OfficeId != placement.OfficeId
                || (user.IsActive && !request.UserDto.IsActive)
                || !string.IsNullOrWhiteSpace(request.UserDto.Password);

            user.Kind = request.UserDto.Kind;
            user.FullName = request.UserDto.FullName;
            user.Email = email;
            user.PersonalIdNumber = personalId;
            user.PhoneNumber = Application.Common.PhoneRules.ToStored(request.UserDto.PhoneNumber);
            user.RoleId = roleId;
            if (!account) user.PasswordHash = null;
            user.DepartmentId = placement.DepartmentId;
            user.OfficeId = placement.OfficeId;
            user.BranchId = placement.BranchId;
            // تفعيل الحساب أو تعطيله صلاحية منفصلة عن تعديل البيانات (مدير النظام يملكها دائماً)
            if (request.UserDto.IsActive != user.IsActive
                && !OrganizationRole.IsSystemAdmin(await _currentUserService.GetUserAsync())
                && !await _permissions.HasAsync(_currentUserService.UserId, AppPermissions.ToggleUserActive))
                throw new UnauthorizedAccessException("لا تملك صلاحية تفعيل الحسابات أو تعطيلها");

            user.IsActive = request.UserDto.IsActive;

            if (account && !string.IsNullOrWhiteSpace(request.UserDto.Password))
            {
                user.PasswordHash = _passwordHasher.Hash(request.UserDto.Password);
                // كلمة مرور جديدة من المسؤول تفك قفل المحاولات الخاطئة
                user.FailedLoginCount = 0;
                user.LockoutUntil = null;
            }

            await _userService.UpdateAsync(user);
            if (sensitive) await _userService.RevokeSessionsAsync(user.Id);
            var updated = await _userService.GetWithDetailsAsync(user.Id) ?? user;
            return _mapper.Map<UserResponseDto>(updated);
        }
    }
}
