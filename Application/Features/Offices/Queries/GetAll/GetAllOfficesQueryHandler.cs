using Application.DTOs.Response;
using Application.Common;
using Application.Interfaces;
using AutoMapper;
using MediatR;

namespace Application.Features.Offices.Queries.GetAll
{
    public class GetAllOfficesQueryHandler
        : IRequestHandler<GetAllOfficesQuery, List<OfficeResponseDto>>
    {
        private readonly IOfficeService _officeService;
        private readonly IUserService _users;
        private readonly IUserPermissionService _permissions;
        private readonly IMapper _mapper;

        public GetAllOfficesQueryHandler(IOfficeService officeService, IMapper mapper, IUserService users, IUserPermissionService permissions)
        {
            _officeService = officeService;
            _mapper = mapper;
            _users = users;
            _permissions = permissions;
        }

        public async Task<List<OfficeResponseDto>> Handle(
            GetAllOfficesQuery request,
            CancellationToken cancellationToken)
        {
            var viewer = await Viewer.CurrentAsync(_users, _permissions);
            var branch = StructureScope.BranchOf(viewer);
            var offices = (await _officeService.GetAllAsync())
                .Where(o => branch == null || o.Department?.BranchId == branch).ToList();
            return _mapper.Map<List<OfficeResponseDto>>(offices);
        }
    }
}
