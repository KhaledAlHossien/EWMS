using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using MediatR;

namespace Application.Features.Roles.Queries.GetAll
{
    public class GetAllRolesQueryHandler : IRequestHandler<GetAllRolesQuery, List<RoleResponseDto>>
    {
        private readonly IRoleService _roleService;
        private readonly IRolePermissionService _rolePermissionService;
        private readonly IMapper _mapper;

        public GetAllRolesQueryHandler(
            IRoleService roleService,
            IRolePermissionService rolePermissionService,
            IMapper mapper)
        {
            _roleService = roleService;
            _rolePermissionService = rolePermissionService;
            _mapper = mapper;
        }

        public async Task<List<RoleResponseDto>> Handle(GetAllRolesQuery request, CancellationToken cancellationToken)
        {
            var roles = await _roleService.GetAllAsync();
            var result = new List<RoleResponseDto>();

            foreach (var role in roles)
            {
                var response = _mapper.Map<RoleResponseDto>(role);
                response.Permissions = _mapper.Map<List<PermissionResponseDto>>(
                    (await _rolePermissionService.GetByRoleAsync(role.Id)).Select(rp => rp.Permission));
                result.Add(response);
            }

            return result;
        }
    }
}
