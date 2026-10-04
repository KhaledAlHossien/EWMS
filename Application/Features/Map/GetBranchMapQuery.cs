using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using MediatR;

namespace Application.Features.Map
{
    /// <summary>
    /// خريطة الفرع في لوحة المتابعة (Role-Permission، 2026-10-03):
    /// - SuperAdmin يتصفح خريطة أي فرع.
    /// - من يملك ViewBranchMap يرى خريطة فرعه فقط ببيانات فرعه (ولا يرى فروعاً أخرى).
    /// </summary>
    public record GetBranchMapQuery(int BranchId) : IRequest<BranchMapDto>;
    public record GetMapBranchesQuery : IRequest<List<MapBranchOptionDto>>;

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
