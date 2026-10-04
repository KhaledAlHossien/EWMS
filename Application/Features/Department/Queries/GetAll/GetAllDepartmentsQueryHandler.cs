using Application.DTOs.Response;
using Application.Common;
using Application.Interfaces;
using AutoMapper;
using MediatR;

namespace Application.Features.Department.Queries.GetAll
{
    public class GetAllDepartmentsQueryHandler
        : IRequestHandler<GetAllDepartmentsQuery, List<DepartmentResponseDto>>
    {
        private readonly IDepartmentService _departmentService;
        private readonly IUserService _users;
        private readonly IUserPermissionService _permissions;
        private readonly IMapper _mapper;

        public GetAllDepartmentsQueryHandler(IDepartmentService departmentService, IMapper mapper, IUserService users, IUserPermissionService permissions)
        {
            _departmentService = departmentService;
            _mapper = mapper;
            _users = users;
            _permissions = permissions;
        }

        public async Task<List<DepartmentResponseDto>> Handle(
            GetAllDepartmentsQuery request,
            CancellationToken cancellationToken)
        {
            var viewer = await Viewer.CurrentAsync(_users, _permissions);
            var branch = StructureScope.BranchOf(viewer);
            var departments = (await _departmentService.GetAllAsync())
                .Where(d => branch == null || d.BranchId == branch).ToList();
            return _mapper.Map<List<DepartmentResponseDto>>(departments);
        }
    }
}