using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using MediatR;

namespace Application.Features.Users.Queries.GetByBranch
{
    public class GetUsersByBranchQueryHandler : IRequestHandler<GetUsersByBranchQuery, List<UserResponseDto>>
    {
        private readonly IUserService _userService;
        private readonly IBranchService _branchService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IMapper _mapper;

        public GetUsersByBranchQueryHandler(
            IUserService userService,
            IBranchService branchService,
            ICurrentUserService currentUserService,
            IMapper mapper)
        {
            _userService = userService;
            _branchService = branchService;
            _currentUserService = currentUserService;
            _mapper = mapper;
        }

        public async Task<List<UserResponseDto>> Handle(GetUsersByBranchQuery request, CancellationToken cancellationToken)
        {
            if (!await _branchService.ExistsAsync(request.BranchId))
                throw new KeyNotFoundException("الفرع غير موجود");

            if (!_currentUserService.Role.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase)
                && _currentUserService.BranchId != request.BranchId)
                throw new UnauthorizedAccessException("لا يمكنك عرض مستخدمي فرع آخر");

            return _mapper.Map<List<UserResponseDto>>(await _userService.GetByBranchAsync(request.BranchId));
        }
    }
}
