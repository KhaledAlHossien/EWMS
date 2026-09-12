using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using MediatR;

namespace Application.Features.Users.Queries.GetById
{
    public class GetUserByIdQueryHandler : IRequestHandler<GetUserByIdQuery, UserResponseDto>
    {
        private readonly IUserService _userService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IMapper _mapper;

        public GetUserByIdQueryHandler(
            IUserService userService,
            ICurrentUserService currentUserService,
            IMapper mapper)
        {
            _userService = userService;
            _currentUserService = currentUserService;
            _mapper = mapper;
        }

        public async Task<UserResponseDto> Handle(GetUserByIdQuery request, CancellationToken cancellationToken)
        {
            var user = await _userService.GetWithDetailsAsync(request.Id)
                ?? throw new KeyNotFoundException("المستخدم غير موجود");

            if (!_currentUserService.Role.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase)
                && _currentUserService.BranchId != user.BranchId)
                throw new UnauthorizedAccessException("لا يمكنك عرض مستخدم خارج فرعك");

            return _mapper.Map<UserResponseDto>(user);
        }
    }
}
