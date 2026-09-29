using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using MediatR;

namespace Application.Features.Maintenance.Requests
{
    /// <summary>بحث/عرض الطلبات ضمن نطاقي (طلباتي، أو طلبات قسمي لرئيس القسم، أو الكل للسوبر ادمن)</summary>
    public record SearchMaintenanceRequestsQuery(MaintenanceRequestFilterDto Filter)
        : IRequest<PagedResultDto<MaintenanceRequestResponseDto>>;

    public record GetMaintenanceRequestByIdQuery(int Id) : IRequest<MaintenanceRequestResponseDto>;

    /// <summary>الفنيون الذين لهم طلبات ضمن نطاقي (لقائمة فلتر "الفني")</summary>
    public record GetMaintenanceTechniciansQuery : IRequest<List<TechnicianOptionDto>>;

    public class MaintenanceRequestQueriesHandler :
        IRequestHandler<SearchMaintenanceRequestsQuery, PagedResultDto<MaintenanceRequestResponseDto>>,
        IRequestHandler<GetMaintenanceRequestByIdQuery, MaintenanceRequestResponseDto>,
        IRequestHandler<GetMaintenanceTechniciansQuery, List<TechnicianOptionDto>>
    {
        private readonly IMaintenanceRequestService _requestService;
        private readonly IUserService _userService;
        private readonly IMapper _mapper;

        public MaintenanceRequestQueriesHandler(
            IMaintenanceRequestService requestService,
            IUserService userService,
            IMapper mapper)
        {
            _requestService = requestService;
            _userService = userService;
            _mapper = mapper;
        }

        public async Task<PagedResultDto<MaintenanceRequestResponseDto>> Handle(
            SearchMaintenanceRequestsQuery request, CancellationToken ct)
        {
            var user = await MaintenanceRules.CurrentUserAsync(_userService);
            var (page, pageSize) = MaintenanceRules.NormalizePaging(request.Filter.Page, request.Filter.PageSize);

            var (items, total) = await _requestService.SearchAsync(
                MaintenanceRules.RequestScope(user), request.Filter, page, pageSize);

            return new PagedResultDto<MaintenanceRequestResponseDto>
            {
                Items = _mapper.Map<List<MaintenanceRequestResponseDto>>(items),
                TotalCount = total,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<MaintenanceRequestResponseDto> Handle(GetMaintenanceRequestByIdQuery request, CancellationToken ct)
        {
            var user = await MaintenanceRules.CurrentUserAsync(_userService);
            var entity = await _requestService.GetByIdAsync(request.Id)
                ?? throw new KeyNotFoundException("طلب الصيانة غير موجود");

            MaintenanceRules.EnsureCanAccess(user, entity.UserId, entity.DepartmentId,
                "لا يمكنك عرض طلب صيانة خارج نطاقك");

            return _mapper.Map<MaintenanceRequestResponseDto>(entity);
        }

        public async Task<List<TechnicianOptionDto>> Handle(GetMaintenanceTechniciansQuery request, CancellationToken ct)
        {
            var user = await MaintenanceRules.CurrentUserAsync(_userService);
            return await _requestService.GetTechniciansAsync(MaintenanceRules.RequestScope(user));
        }
    }
}
