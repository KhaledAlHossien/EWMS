using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using MediatR;

namespace Application.Features.Maintenance.Requests.Queries.Search
{
    public class SearchMaintenanceRequestsQueryHandler : IRequestHandler<SearchMaintenanceRequestsQuery, PagedResultDto<MaintenanceRequestResponseDto>>
    {
        private readonly IMaintenanceRequestService _requestService;
        private readonly IUserService _userService;
        private readonly IUserPermissionService _permissions;
        private readonly IMapper _mapper;

        public SearchMaintenanceRequestsQueryHandler(IMaintenanceRequestService requestService, IUserService userService, IUserPermissionService permissions, IMapper mapper)
        {
            _requestService = requestService;
            _userService = userService;
            _permissions = permissions;
            _mapper = mapper;
        }

        public async Task<PagedResultDto<MaintenanceRequestResponseDto>> Handle(
            SearchMaintenanceRequestsQuery request, CancellationToken ct)
        {
            var scopes = await MaintenanceRules.RequestScopesAsync(_userService, _permissions);
            var (page, pageSize) = MaintenanceRules.NormalizePaging(request.Filter.Page, request.Filter.PageSize);

            var (items, total) = await _requestService.SearchAsync(
                MaintenanceRules.RequestFilter(scopes.View), request.Filter, page, pageSize);

            return new PagedResultDto<MaintenanceRequestResponseDto>
            {
                Items = items.Select(r => MaintenanceRequestMapping.ToDto(_mapper, r, scopes)).ToList(),
                TotalCount = total,
                Page = page,
                PageSize = pageSize
            };
        }
    }
}
