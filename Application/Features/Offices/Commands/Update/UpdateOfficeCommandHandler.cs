using Application.DTOs.Response;
using Application.Features.Offices;
using Application.Interfaces;
using AutoMapper;
using MediatR;

namespace Application.Features.Offices.Commands.Update
{
    public class UpdateOfficeCommandHandler
        : IRequestHandler<UpdateOfficeCommand, OfficeResponseDto>
    {
        private readonly IOfficeService _officeService;
        private readonly IDepartmentService _departmentService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IMapper _mapper;

        public UpdateOfficeCommandHandler(
            IOfficeService officeService,
            IDepartmentService departmentService,
            ICurrentUserService currentUserService,
            IMapper mapper)
        {
            _officeService = officeService;
            _departmentService = departmentService;
            _currentUserService = currentUserService;
            _mapper = mapper;
        }

        public async Task<OfficeResponseDto> Handle(
            UpdateOfficeCommand request,
            CancellationToken cancellationToken)
        {
            // 1. جلب المكتب
            var office = await _officeService.GetByIdAsync(request.Id)
                ?? throw new KeyNotFoundException("المكتب غير موجود");

            // 2. التحقق من القسم
            if (!await _departmentService.ExistsAsync(request.OfficeDto.DepartmentId))
                throw new KeyNotFoundException("القسم المحدد غير موجود");

            // المكتب الحالي والقسم الجديد كلاهما يجب أن يكونا ضمن نطاقي
            await OfficeRules.EnsureCanManageDepartmentAsync(
                _currentUserService, _departmentService, office.DepartmentId,
                "لا يمكنك تعديل مكتب خارج نطاقك");
            await OfficeRules.EnsureCanManageDepartmentAsync(
                _currentUserService, _departmentService, request.OfficeDto.DepartmentId,
                "لا يمكنك نقل المكتب إلى قسم خارج نطاقك");

            // 3. التحقق من عدم تكرار الاسم (مع استثناء المكتب الحالي)
            if (await _officeService.ExistsByNameAsync(request.OfficeDto.Name, request.Id))
                throw new InvalidOperationException("يوجد مكتب آخر بنفس الاسم");

            // 4. تحديث الحقول
            _mapper.Map(request.OfficeDto, office);

            // 5. الحفظ
            await _officeService.UpdateAsync(office);

            // 6. إعادة الرد
            return _mapper.Map<OfficeResponseDto>(office);
        }
    }
}
