using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using MediatR;

namespace Application.Features.Department.Queries.GetById
{
    public class GetDepartmentByIdQueryHandler
        : IRequestHandler<GetDepartmentByIdQuery, DepartmentResponseDto>
    {
        private readonly IDepartmentService _departmentService;
        private readonly IMapper _mapper;

        public GetDepartmentByIdQueryHandler(IDepartmentService departmentService, IMapper mapper)
        {
            _departmentService = departmentService;
            _mapper = mapper;
        }

        public async Task<DepartmentResponseDto> Handle(
            GetDepartmentByIdQuery request,
            CancellationToken cancellationToken)
        {
            var department = await _departmentService.GetByIdAsync(request.Id)
                ?? throw new KeyNotFoundException("القسم غير موجود");

            return _mapper.Map<DepartmentResponseDto>(department);
        }
    }
}