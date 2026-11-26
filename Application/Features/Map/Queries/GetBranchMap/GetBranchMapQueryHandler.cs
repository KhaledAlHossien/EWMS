using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using MediatR;

namespace Application.Features.Map.Queries.GetBranchMap
{
    public class GetBranchMapQueryHandler : IRequestHandler<GetBranchMapQuery, BranchMapDto>
    {
        private readonly IMapService _mapService;
        private readonly IUserService _userService;
        private readonly IUserPermissionService _permissions;

        public GetBranchMapQueryHandler(IMapService mapService, IUserService userService, IUserPermissionService permissions)
        {
            _mapService = mapService;
            _userService = userService;
            _permissions = permissions;
        }

        public async Task<BranchMapDto> Handle(GetBranchMapQuery request, CancellationToken cancellationToken)
        {
            var viewer = await Viewer.CurrentAsync(_userService, _permissions);

            var allowed = viewer.IsOrganizationWide
                || (viewer.Has(AppPermissions.ViewBranchMap) && viewer.User.BranchId == request.BranchId);
            if (!allowed)
                throw new UnauthorizedAccessException("لا تملك صلاحية عرض خريطة هذا الفرع");

            return await _mapService.GetBranchMapAsync(request.BranchId)
                ?? throw new KeyNotFoundException("الفرع غير موجود");
        }
    }
}
