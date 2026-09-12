using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using MediatR;

namespace Application.Features.Permissions.Queries.GetAll
{
    public class GetAllPermissionsQueryHandler
        : IRequestHandler<GetAllPermissionsQuery, List<PermissionResponseDto>>
    {
        private readonly IPermissionService _permissionService;
        private readonly IMapper _mapper;

        public GetAllPermissionsQueryHandler(IPermissionService permissionService, IMapper mapper)
        {
            _permissionService = permissionService;
            _mapper = mapper;
        }

        public async Task<List<PermissionResponseDto>> Handle(
            GetAllPermissionsQuery request,
            CancellationToken cancellationToken)
        {
            return _mapper.Map<List<PermissionResponseDto>>(await _permissionService.GetAllAsync());
        }
    }
}
