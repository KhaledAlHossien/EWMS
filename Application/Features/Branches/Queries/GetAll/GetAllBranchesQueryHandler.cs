using Application.DTOs.Response;
using Application.Common;
using Application.Interfaces;
using AutoMapper;
using MediatR;

namespace Application.Features.Branches.Queries.GetAll
{
    public class GetAllBranchesQueryHandler : IRequestHandler<GetAllBranchesQuery, List<BranchResponseDto>>
    {
        private readonly IBranchService _branchService;
        private readonly IUserService _users;
        private readonly IUserPermissionService _permissions;
        private readonly IMapper _mapper;

        public GetAllBranchesQueryHandler(IBranchService branchService, IMapper mapper, IUserService users, IUserPermissionService permissions)
        {
            _branchService = branchService;
            _mapper = mapper;
            _users = users;
            _permissions = permissions;
        }

        public async Task<List<BranchResponseDto>> Handle(GetAllBranchesQuery request, CancellationToken cancellationToken)
        {
            var viewer = await Viewer.CurrentAsync(_users, _permissions);
            var branch = StructureScope.BranchOf(viewer);
            var branches = (await _branchService.GetAllAsync()).Where(b => branch == null || b.Id == branch).ToList();
            return _mapper.Map<List<BranchResponseDto>>(branches);
        }
    }
}
