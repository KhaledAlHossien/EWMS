using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using MediatR;

namespace Application.Features.Map.Queries.GetBranches
{
    public class GetMapBranchesQueryHandler : IRequestHandler<GetMapBranchesQuery, List<MapBranchOptionDto>>
    {
        private readonly IMapService _mapService;
        private readonly IUserService _userService;
        private readonly IUserPermissionService _permissions;

        public GetMapBranchesQueryHandler(IMapService mapService, IUserService userService, IUserPermissionService permissions)
        {
            _mapService = mapService;
            _userService = userService;
            _permissions = permissions;
        }

        public async Task<List<MapBranchOptionDto>> Handle(GetMapBranchesQuery request, CancellationToken cancellationToken)
        {
            var viewer = await Viewer.CurrentAsync(_userService, _permissions);
            var options = await _mapService.GetBranchOptionsAsync();

            if (viewer.IsOrganizationWide) return options;

            if (!viewer.Has(AppPermissions.ViewBranchMap) || viewer.User.BranchId is not int branchId)
                throw new UnauthorizedAccessException("لا تملك صلاحية عرض الخريطة");

            return options.Where(b => b.Id == branchId).ToList();
        }
    }
}
