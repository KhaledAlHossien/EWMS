using Application.DTOs.Response;
using Application.Common;
using Application.Interfaces;
using AutoMapper;
using MediatR;

namespace Application.Features.Users.Queries.GetById
{
    public class GetUserByIdQueryHandler : IRequestHandler<GetUserByIdQuery, UserResponseDto>
    {
        private readonly IUserService _userService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IUserPermissionService _permissions;
        private readonly IMapper _mapper;

        public GetUserByIdQueryHandler(
            IUserService userService,
            ICurrentUserService currentUserService,
            IUserPermissionService permissions,
            IMapper mapper)
        {
            _userService = userService;
            _currentUserService = currentUserService;
            _mapper = mapper;
            _permissions = permissions;
        }

        public async Task<UserResponseDto> Handle(GetUserByIdQuery request, CancellationToken cancellationToken)
        {
            var user = await _userService.GetWithDetailsAsync(request.Id)
                ?? throw new KeyNotFoundException("المستخدم غير موجود");

            // نفس حدّ ViewUsers: موظفو فرعي، أو الكل لمن يتصفح المؤسسة
            var viewer = await Viewer.CurrentAsync(_userService, _permissions);
            if (viewer.Id != user.Id)
                StructureScope.EnsureIncludes(viewer, user.BranchId, "لا يمكنك عرض مستخدم خارج فرعك");

            return _mapper.Map<UserResponseDto>(user);
        }
    }
}
