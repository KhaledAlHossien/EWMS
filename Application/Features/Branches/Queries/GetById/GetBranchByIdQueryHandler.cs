using Application.DTOs.Response;
using Application.Common;
using Application.Interfaces;
using AutoMapper;
using MediatR;

namespace Application.Features.Branches.Queries.GetById
{
    public class GetBranchByIdQueryHandler : IRequestHandler<GetBranchByIdQuery, BranchResponseDto>
    {
        private readonly IBranchService _branchService;
        private readonly IUserService _users;
        private readonly IUserPermissionService _permissions;
        private readonly IMapper _mapper;

        public GetBranchByIdQueryHandler(IBranchService branchService, IMapper mapper, IUserService users, IUserPermissionService permissions)
        {
            _branchService = branchService;
            _mapper = mapper;
            _users = users;
            _permissions = permissions;
        }

        public async Task<BranchResponseDto> Handle(GetBranchByIdQuery request, CancellationToken cancellationToken)
        {
            var branch = await _branchService.GetByIdAsync(request.Id)
                ?? throw new KeyNotFoundException("الفرع غير موجود");

            var viewer = await Viewer.CurrentAsync(_users, _permissions);
            StructureScope.EnsureIncludes(viewer, branch.Id, "لا يمكنك عرض فرع غير فرعك");

            return _mapper.Map<BranchResponseDto>(branch);
        }
    }
}
