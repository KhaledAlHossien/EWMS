using Application.DTOs.Response;
using Application.Features.Vacations.Query.GetAll;
using Application.Interfaces;
using AutoMapper;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Features.Vacations.Queries.GetAll
{
    // ملاحظة: هذا الاستعلام "واعٍ بالدور" (Role-aware) — يرجع كل ما يخص المستخدم الحالي
    // ضمن نطاقه: SuperAdmin يرى الكل، BranchManager يرى فرعه، Manager يرى قسمه،
    // وأي موظف آخر يرى إجازاته فقط (نفس سلوك My).
    public class GetAllVacationsQueryHandler
       : IRequestHandler<GetAllVacationsQuery, List<VacationResponseDto>>
    {
        private readonly IVacationService _service;
        private readonly IUserService _userService;
        private readonly IMapper _mapper;

        public GetAllVacationsQueryHandler(
            IVacationService service,
            IUserService userService,
            IMapper mapper)
        {
            _service = service;
            _userService = userService;
            _mapper = mapper;
        }

        public async Task<List<VacationResponseDto>> Handle(
            GetAllVacationsQuery request, CancellationToken ct)
        {
            var currentUser = await _userService.GetByIdAsync(_userService.UserId)
                ?? throw new UnauthorizedAccessException("المستخدم غير مصادق");

            var roleName = currentUser.Role?.Name ?? "";

            var list = roleName switch
            {
                "SuperAdmin" => await _service.GetAllAsync(),
                "BranchManager" => await _service.GetByBranchIdAsync(currentUser.BranchId ?? 0),
                "Manager" => await _service.GetByDepartmentIdAsync(currentUser.DepartmentId ?? 0),
                "OfficeManager" => await _service.GetByOfficeIdAsync(currentUser.OfficeId ?? 0),
                _ => await _service.GetByUserIdAsync(currentUser.Id)
            };

            return _mapper.Map<List<VacationResponseDto>>(list);
        }
    }
}
