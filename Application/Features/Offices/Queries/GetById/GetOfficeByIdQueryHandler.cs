using Application.DTOs.Response;
using Application.Common;
using Application.Interfaces;
using AutoMapper;
using MediatR;

namespace Application.Features.Offices.Queries.GetById
{
    public class GetOfficeByIdQueryHandler
        : IRequestHandler<GetOfficeByIdQuery, OfficeResponseDto>
    {
        private readonly IOfficeService _officeService;
        private readonly IUserService _users;
        private readonly IUserPermissionService _permissions;
        private readonly IMapper _mapper;

        public GetOfficeByIdQueryHandler(IOfficeService officeService, IMapper mapper, IUserService users, IUserPermissionService permissions)
        {
            _officeService = officeService;
            _mapper = mapper;
            _users = users;
            _permissions = permissions;
        }

        public async Task<OfficeResponseDto> Handle(
            GetOfficeByIdQuery request,
            CancellationToken cancellationToken)
        {
            var office = await _officeService.GetByIdAsync(request.Id)
                ?? throw new KeyNotFoundException("المكتب غير موجود");

            var viewer = await Viewer.CurrentAsync(_users, _permissions);
            StructureScope.EnsureIncludes(viewer, office.Department?.BranchId, "لا يمكنك عرض مكتب خارج فرعك");

            return _mapper.Map<OfficeResponseDto>(office);
        }
    }
}
