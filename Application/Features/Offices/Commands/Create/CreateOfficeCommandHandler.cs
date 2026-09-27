using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using MediatR;
using OfficeEntity = Domain.Entities.Office;

namespace Application.Features.Offices.Commands.Create
{
    public class CreateOfficeCommandHandler
        : IRequestHandler<CreateOfficeCommand, OfficeResponseDto>
    {
        private readonly IOfficeService _officeService;
        private readonly IDepartmentService _departmentService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IMapper _mapper;

        public CreateOfficeCommandHandler(
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
            CreateOfficeCommand request,
            CancellationToken cancellationToken)
        {
            // 1. التحقق من وجود القسم
            if (!await _departmentService.ExistsAsync(request.OfficeDto.DepartmentId))
                throw new KeyNotFoundException("القسم المحدد غير موجود");

            if (!_currentUserService.Role.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase)
                && _currentUserService.DepartmentId != request.OfficeDto.DepartmentId)
                throw new UnauthorizedAccessException("لا يمكنك إضافة مكتب خارج قسمك");

            // 2. التحقق من عدم تكرار الاسم
            if (await _officeService.ExistsByNameAsync(request.OfficeDto.Name))
                throw new InvalidOperationException("يوجد مكتب بنفس الاسم مسبقاً");

            // 3. التحويل إلى Entity
            var office = _mapper.Map<OfficeEntity>(request.OfficeDto);

            // 4. الحفظ
            var created = await _officeService.AddAsync(office);

            // 5. إعادة الجلب مع Include(Department, Branch)
            var withDetails = await _officeService.GetByIdAsync(created.Id)
                ?? created;

            // 6. إعادة الرد
            return _mapper.Map<OfficeResponseDto>(withDetails);
        }
    }
}
