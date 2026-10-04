using Application.DTOs.Response;
using Application.Common;
using Application.Interfaces;
using AutoMapper;
using MediatR;

namespace Application.Features.Department.Queries.GetById
{
    public class GetDepartmentByIdQueryHandler
        : IRequestHandler<GetDepartmentByIdQuery, DepartmentResponseDto>
    {
        private readonly IDepartmentService _departmentService;
        private readonly IUserService _users;
        private readonly IUserPermissionService _permissions;
        private readonly IMapper _mapper;

        public GetDepartmentByIdQueryHandler(IDepartmentService departmentService, IMapper mapper, IUserService users, IUserPermissionService permissions)
        {
            _departmentService = departmentService;
            _mapper = mapper;
            _users = users;
            _permissions = permissions;
        }

        public async Task<DepartmentResponseDto> Handle(
            GetDepartmentByIdQuery request,
            CancellationToken cancellationToken)
        {
            var department = await _departmentService.GetByIdAsync(request.Id)
                ?? throw new KeyNotFoundException("القسم غير موجود");

            var viewer = await Viewer.CurrentAsync(_users, _permissions);
            StructureScope.EnsureIncludes(viewer, department.BranchId, "لا يمكنك عرض قسم خارج فرعك");

            return _mapper.Map<DepartmentResponseDto>(department);
        }
    }
}