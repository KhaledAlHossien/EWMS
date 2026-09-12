using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using MediatR;

namespace Application.Features.Department.Queries.GetAll
{
    public class GetAllDepartmentsQueryHandler
        : IRequestHandler<GetAllDepartmentsQuery, List<DepartmentResponseDto>>
    {
        private readonly IDepartmentService _departmentService;
        private readonly IMapper _mapper;

        public GetAllDepartmentsQueryHandler(IDepartmentService departmentService, IMapper mapper)
        {
            _departmentService = departmentService;
            _mapper = mapper;
        }

        public async Task<List<DepartmentResponseDto>> Handle(
            GetAllDepartmentsQuery request,
            CancellationToken cancellationToken)
        {
            var departments = await _departmentService.GetAllAsync();
            return _mapper.Map<List<DepartmentResponseDto>>(departments);
        }
    }
}