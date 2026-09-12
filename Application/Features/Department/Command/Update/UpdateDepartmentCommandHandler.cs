using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using MediatR;

namespace Application.Features.Department.Command.Update
{
    public class UpdateDepartmentCommandHandler
        : IRequestHandler<UpdateDepartmentCommand, DepartmentResponseDto>
    {
        private readonly IDepartmentService _departmentService;
        private readonly IBranchService _branchService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IMapper _mapper;

        public UpdateDepartmentCommandHandler(
            IDepartmentService departmentService,
            IBranchService branchService,
            ICurrentUserService currentUserService,
            IMapper mapper)
        {
            _departmentService = departmentService;
            _branchService = branchService;
            _currentUserService = currentUserService;
            _mapper = mapper;
        }

        public async Task<DepartmentResponseDto> Handle(
            UpdateDepartmentCommand request,
            CancellationToken cancellationToken)
        {
            // 1. جلب القسم
            var department = await _departmentService.GetByIdAsync(request.Id)
                ?? throw new KeyNotFoundException("القسم غير موجود");

            // 2. التحقق من الفرع
            if (!await _branchService.ExistsAsync(request.DepartmentDto.BranchId))
                throw new KeyNotFoundException("الفرع المحدد غير موجود");

            if (!_currentUserService.Role.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase)
                && (_currentUserService.BranchId != department.BranchId
                    || _currentUserService.BranchId != request.DepartmentDto.BranchId))
                throw new UnauthorizedAccessException("لا يمكنك تعديل قسم خارج فرعك");

            // 3. التحقق من عدم تكرار الاسم (مع استثناء القسم الحالي)
            if (await _departmentService.ExistsByNameAsync(request.DepartmentDto.Name, request.Id))
                throw new InvalidOperationException("يوجد قسم آخر بنفس الاسم");

            // 4. تحديث الحقول
            _mapper.Map(request.DepartmentDto, department);

            // 5. الحفظ
            await _departmentService.UpdateAsync(department);

            // 6. إعادة الرد
            return _mapper.Map<DepartmentResponseDto>(department);
        }
    }
}
