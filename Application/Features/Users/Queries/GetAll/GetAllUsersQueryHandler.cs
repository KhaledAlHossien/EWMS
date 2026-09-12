using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using MediatR;

namespace Application.Features.Users.Queries.GetAll
{
    public class GetAllUsersQueryHandler : IRequestHandler<GetAllUsersQuery, List<UserResponseDto>>
    {
        private readonly IUserService _userService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IMapper _mapper;

        public GetAllUsersQueryHandler(
            IUserService userService,
            ICurrentUserService currentUserService,
            IMapper mapper)
        {
            _userService = userService;
            _currentUserService = currentUserService;
            _mapper = mapper;
        }

        public async Task<List<UserResponseDto>> Handle(GetAllUsersQuery request, CancellationToken cancellationToken)
        {
            var users = _currentUserService.Role.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase)
                ? await _userService.GetAllAsync()
                : await _userService.GetByBranchAsync(_currentUserService.BranchId);

            return _mapper.Map<List<UserResponseDto>>(users);
        }
    }
}
