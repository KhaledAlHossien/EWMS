using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using MediatR;
using DepartmentEntity = Domain.Entities.Department;

namespace Application.Features.Department.Command.Create
{
    public class CreateDepartmentCommandHandler
        : IRequestHandler<CreateDepartmentCommand, DepartmentResponseDto>
    {
        private readonly IDepartmentService _departmentService;
        private readonly IBranchService _branchService;
        private readonly IMapper _mapper;

        public CreateDepartmentCommandHandler(
            IDepartmentService departmentService,
            IBranchService branchService,
            IMapper mapper)
        {
            _departmentService = departmentService;
            _branchService = branchService;
            _mapper = mapper;
        }

        
        public async Task<DepartmentResponseDto> Handle(
    CreateDepartmentCommand request,
    CancellationToken cancellationToken)
        {
            // 1. التحقق من وجود الفرع
            if (!await _branchService.ExistsAsync(request.DepartmentDto.BranchId))
                throw new KeyNotFoundException("الفرع المحدد غير موجود");

            // 2. التحقق من عدم تكرار الاسم
            if (await _departmentService.ExistsByNameAsync(request.DepartmentDto.Name))
                throw new InvalidOperationException("يوجد قسم بنفس الاسم مسبقاً");

            // 3. التحويل إلى Entity
            var department = _mapper.Map<DepartmentEntity>(request.DepartmentDto);

            // 4. الحفظ
            var created = await _departmentService.AddAsync(department);

            // 5. ✅ إعادة الجلب مع Include(Branch)
            var withBranch = await _departmentService.GetByIdAsync(created.Id)
                ?? created;

            // 6. إعادة الرد
            return _mapper.Map<DepartmentResponseDto>(withBranch);
        }
    }
}