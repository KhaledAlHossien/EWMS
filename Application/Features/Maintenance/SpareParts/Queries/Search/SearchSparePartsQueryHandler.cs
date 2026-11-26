using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using MediatR;

namespace Application.Features.Maintenance.SpareParts.Queries.Search
{
    public class SearchSparePartsQueryHandler : IRequestHandler<SearchSparePartsQuery, PagedResultDto<SparePartResponseDto>>
    {
        private readonly ISparePartService _service;
        private readonly IUserService _userService;
        private readonly IUserPermissionService _permissions;

        public SearchSparePartsQueryHandler(ISparePartService service, IUserService userService, IUserPermissionService permissions)
        {
            _service = service;
            _userService = userService;
            _permissions = permissions;
        }

        public async Task<PagedResultDto<SparePartResponseDto>> Handle(SearchSparePartsQuery request, CancellationToken ct)
        {
            var viewer = await ViewerAsync();
            var department = SparePartRules.Department(viewer) ?? request.Filter.DepartmentId;
            var (page, pageSize) = MaintenanceRules.NormalizePaging(request.Filter.Page, request.Filter.PageSize);
            var (items, total) = await _service.SearchAsync(department, request.Filter, page, pageSize);

            return new PagedResultDto<SparePartResponseDto>
            {
                Items = items.Select(SparePartRules.ToDto).ToList(),
                TotalCount = total,
                Page = page,
                PageSize = pageSize
            };
        }

        private Task<Viewer> ViewerAsync() => Viewer.CurrentAsync(_userService, _permissions);
    }
}
