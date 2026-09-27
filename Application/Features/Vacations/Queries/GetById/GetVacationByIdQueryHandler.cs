using Application.DTOs.Response;
using Application.Features.Vacations.Query.GetById;
using Application.Interfaces;
using AutoMapper;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Features.Vacations.Queries.GetById
{
    public class GetVacationByIdQueryHandler
        : IRequestHandler<GetVacationByIdQuery, VacationResponseDto>
    {
        private readonly IVacationService _service;
        private readonly IUserService _userService;
        private readonly IMapper _mapper;

        public GetVacationByIdQueryHandler(
            IVacationService service,
            IUserService userService,
            IMapper mapper)
        {
            _service = service;
            _userService = userService;
            _mapper = mapper;
        }

        public async Task<VacationResponseDto> Handle(
            GetVacationByIdQuery request, CancellationToken ct)
        {
            var vacation = await _service.GetWithDetailsAsync(request.Id)
                ?? throw new KeyNotFoundException("الإجازة غير موجودة");

            var currentUser = await _userService.GetByIdAsync(_userService.UserId)
                ?? throw new UnauthorizedAccessException("المستخدم غير مصادق");

            var roleName = currentUser.Role?.Name ?? "";
            var isOwner = vacation.UserId == currentUser.Id;
            var isSuperAdmin = roleName == "SuperAdmin";
            var isDepartmentManager = roleName == "Manager" && currentUser.DepartmentId == vacation.DepartmentId;
            var isBranchManager = roleName == "BranchManager" && currentUser.BranchId == vacation.BranchId;

            if (!isOwner && !isSuperAdmin && !isDepartmentManager && !isBranchManager)
                throw new UnauthorizedAccessException("لا تملك صلاحية عرض هذه الإجازة");

            return _mapper.Map<VacationResponseDto>(vacation);
        }
    }
}
