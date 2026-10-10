using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using MediatR;

namespace Application.Features.Users.Queries.GetByOffice
{
    public class GetUsersByOfficeQueryHandler : IRequestHandler<GetUsersByOfficeQuery, List<UserResponseDto>>
    {
        private readonly IUserService _userService;
        private readonly IOfficeService _officeService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IMapper _mapper;

        public GetUsersByOfficeQueryHandler(
            IUserService userService,
            IOfficeService officeService,
            ICurrentUserService currentUserService,
            IMapper mapper)
        {
            _userService = userService;
            _officeService = officeService;
            _currentUserService = currentUserService;
            _mapper = mapper;
        }

        public async Task<List<UserResponseDto>> Handle(GetUsersByOfficeQuery request, CancellationToken cancellationToken)
        {
            var office = await _officeService.GetByIdAsync(request.OfficeId)
                ?? throw new KeyNotFoundException("المكتب غير موجود");

            var me = await _currentUserService.GetUserAsync();
            if (!OrganizationRole.IsSystemAdmin(me) && !OrganizationRole.InDepartment(me, office.DepartmentId))
                throw new UnauthorizedAccessException("لا يمكنك عرض مستخدمي مكتب خارج قسمك");

            return _mapper.Map<List<UserResponseDto>>(await _userService.GetByOfficeAsync(request.OfficeId));
        }
    }
}
