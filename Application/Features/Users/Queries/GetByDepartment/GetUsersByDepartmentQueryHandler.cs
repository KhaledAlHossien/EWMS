using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using MediatR;

namespace Application.Features.Users.Queries.GetByDepartment
{
    public class GetUsersByDepartmentQueryHandler
        : IRequestHandler<GetUsersByDepartmentQuery, List<UserResponseDto>>
    {
        private readonly IUserService _userService;
        private readonly IDepartmentService _departmentService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IMapper _mapper;

        public GetUsersByDepartmentQueryHandler(
            IUserService userService,
            IDepartmentService departmentService,
            ICurrentUserService currentUserService,
            IMapper mapper)
        {
            _userService = userService;
            _departmentService = departmentService;
            _currentUserService = currentUserService;
            _mapper = mapper;
        }

        public async Task<List<UserResponseDto>> Handle(
            GetUsersByDepartmentQuery request,
            CancellationToken cancellationToken)
        {
            var department = await _departmentService.GetByIdAsync(request.DepartmentId)
                ?? throw new KeyNotFoundException("القسم غير موجود");

            if (!_currentUserService.Role.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase)
                && _currentUserService.BranchId != department.BranchId)
                throw new UnauthorizedAccessException("لا يمكنك عرض مستخدمي قسم خارج فرعك");

            return _mapper.Map<List<UserResponseDto>>(await _userService.GetByDepartmentAsync(request.DepartmentId));
        }
    }
}
