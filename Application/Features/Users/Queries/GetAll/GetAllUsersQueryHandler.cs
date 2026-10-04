using Application.DTOs.Response;
using Application.Common;
using Application.Interfaces;
using AutoMapper;
using MediatR;

namespace Application.Features.Users.Queries.GetAll
{
    public class GetAllUsersQueryHandler : IRequestHandler<GetAllUsersQuery, List<UserResponseDto>>
    {
        private readonly IUserService _userService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IUserPermissionService _permissions;
        private readonly IMapper _mapper;

        public GetAllUsersQueryHandler(
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

        public async Task<List<UserResponseDto>> Handle(GetAllUsersQuery request, CancellationToken cancellationToken)
        {
            // حدّ ViewUsers: موظفو فرعي، أو الكل لمن يتصفح المؤسسة (StructureScope)
            var viewer = await Viewer.CurrentAsync(_userService, _permissions);
            var branch = StructureScope.BranchOf(viewer);
            var users = branch == null ? await _userService.GetAllAsync() : await _userService.GetByBranchAsync(branch.Value);

            return _mapper.Map<List<UserResponseDto>>(users);
        }
    }
}
