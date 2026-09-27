using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using MediatR;

namespace Application.Features.Users.Queries.GetMe
{
    public class GetMeQueryHandler : IRequestHandler<GetMeQuery, UserResponseDto>
    {
        private readonly IUserService _userService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IMapper _mapper;

        public GetMeQueryHandler(
            IUserService userService,
            ICurrentUserService currentUserService,
            IMapper mapper)
        {
            _userService = userService;
            _currentUserService = currentUserService;
            _mapper = mapper;
        }

        public async Task<UserResponseDto> Handle(GetMeQuery request, CancellationToken cancellationToken)
        {
            var user = await _userService.GetWithDetailsAsync(_currentUserService.UserId)
                ?? throw new KeyNotFoundException("المستخدم غير موجود");

            return _mapper.Map<UserResponseDto>(user);
        }
    }
}
