using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using MediatR;

namespace Application.Features.Maintenance.Requests.Queries.GetActivities
{
    public class GetMaintenanceRequestActivitiesQueryHandler : IRequestHandler<GetMaintenanceRequestActivitiesQuery, List<MaintenanceActivityDto>>
    {
        private readonly IMaintenanceRequestService _requestService;
        private readonly IMapper _mapper;
        private readonly MaintenanceRequestQuerySupport _support;

        public GetMaintenanceRequestActivitiesQueryHandler(IMaintenanceRequestService requestService, IMapper mapper, MaintenanceRequestQuerySupport support)
        {
            _requestService = requestService;
            _mapper = mapper;
            _support = support;
        }

        public async Task<List<MaintenanceActivityDto>> Handle(GetMaintenanceRequestActivitiesQuery request, CancellationToken ct)
        {
            await _support.LoadForViewAsync(request.Id);
            return _mapper.Map<List<MaintenanceActivityDto>>(await _requestService.GetActivitiesAsync(request.Id));
        }
    }
}
