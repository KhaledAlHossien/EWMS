using Application.DTOs.Response;
using AutoMapper;
using MediatR;

namespace Application.Features.Maintenance.Requests.Queries.GetById
{
    public class GetMaintenanceRequestByIdQueryHandler : IRequestHandler<GetMaintenanceRequestByIdQuery, MaintenanceRequestResponseDto>
    {
        private readonly IMapper _mapper;
        private readonly MaintenanceRequestQuerySupport _support;

        public GetMaintenanceRequestByIdQueryHandler(IMapper mapper, MaintenanceRequestQuerySupport support)
        {
            _mapper = mapper;
            _support = support;
        }

        public async Task<MaintenanceRequestResponseDto> Handle(GetMaintenanceRequestByIdQuery request, CancellationToken ct)
        {
            var (entity, scopes) = await _support.LoadForViewAsync(request.Id);
            return MaintenanceRequestMapping.ToDto(_mapper, entity, scopes);
        }
    }
}
