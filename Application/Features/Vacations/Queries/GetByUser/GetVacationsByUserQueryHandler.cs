using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Features.Vacations.Queries.GetByUser
{
    public class GetVacationsByUserQueryHandler
         : IRequestHandler<GetVacationsByUserQuery, List<VacationResponseDto>>
    {
        private readonly IVacationService _service;
        private readonly IUserService _userService;
        private readonly IMapper _mapper;

        public GetVacationsByUserQueryHandler(
            IVacationService service,
            IUserService userService,
            IMapper mapper)
        {
            _service = service;
            _userService = userService;
            _mapper = mapper;
        }

        public async Task<List<VacationResponseDto>> Handle(
            GetVacationsByUserQuery request, CancellationToken ct)
        {
            var currentUser = await _userService.GetByIdAsync(_userService.UserId)
                ?? throw new UnauthorizedAccessException("المستخدم غير مصادق");

            var roleName = currentUser.Role?.Name ?? "";
            var isSuperAdmin = roleName == "SuperAdmin";

            if (request.UserId != currentUser.Id && !isSuperAdmin)
            {
                var targetUser = await _userService.GetByIdAsync(request.UserId)
                    ?? throw new KeyNotFoundException("المستخدم غير موجود");

                var isDepartmentManager = roleName == "Manager" && currentUser.DepartmentId == targetUser.DepartmentId;
                var isBranchManager = roleName == "BranchManager" && currentUser.BranchId == targetUser.BranchId;

                var isOfficeManager = roleName == "OfficeManager" && currentUser.OfficeId != null
                    && currentUser.OfficeId == targetUser.OfficeId;

                if (!isDepartmentManager && !isBranchManager && !isOfficeManager)
                    throw new UnauthorizedAccessException("لا تملك صلاحية عرض إجازات هذا المستخدم");
            }

            var list = await _service.GetByUserIdAsync(request.UserId);
            return _mapper.Map<List<VacationResponseDto>>(list);
        }
    }
}
