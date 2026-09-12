using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using MediatR;

namespace Application.Features.Roles.Queries.GetById
{
    public class GetRoleByIdQueryHandler : IRequestHandler<GetRoleByIdQuery, RoleResponseDto>
    {
        private readonly IRoleService _roleService;
        private readonly IRolePermissionService _rolePermissionService;
        private readonly IMapper _mapper;

        public GetRoleByIdQueryHandler(
            IRoleService roleService,
            IRolePermissionService rolePermissionService,
            IMapper mapper)
        {
            _roleService = roleService;
            _rolePermissionService = rolePermissionService;
            _mapper = mapper;
        }

        public async Task<RoleResponseDto> Handle(GetRoleByIdQuery request, CancellationToken cancellationToken)
        {
            var role = await _roleService.GetByIdAsync(request.Id)
                ?? throw new KeyNotFoundException("الدور غير موجود");

            var response = _mapper.Map<RoleResponseDto>(role);
            response.Permissions = _mapper.Map<List<PermissionResponseDto>>(
                (await _rolePermissionService.GetByRoleAsync(role.Id)).Select(rp => rp.Permission));
            return response;
        }
    }
}
