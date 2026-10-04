using Application.Common;
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
        private readonly IUserPermissionService _permissions;
        private readonly IMapper _mapper;

        public GetVacationByIdQueryHandler(
            IVacationService service,
            IUserService userService,
            IUserPermissionService permissions,
            IMapper mapper)
        {
            _service = service;
            _userService = userService;
            _permissions = permissions;
            _mapper = mapper;
        }

        public async Task<VacationResponseDto> Handle(
            GetVacationByIdQuery request, CancellationToken ct)
        {
            var vacation = await _service.GetWithDetailsAsync(request.Id)
                ?? throw new KeyNotFoundException("الإجازة غير موجودة");

            var viewer = await Viewer.CurrentAsync(_userService, _permissions);
            if (!VacationAccess.CanView(viewer, vacation))
                throw new UnauthorizedAccessException("لا تملك صلاحية عرض هذه الإجازة");

            return _mapper.Map<VacationResponseDto>(vacation);
        }
    }
}
