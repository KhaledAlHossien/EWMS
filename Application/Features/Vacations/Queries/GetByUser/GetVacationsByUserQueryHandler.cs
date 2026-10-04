using Application.Common;
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
        private readonly IUserPermissionService _permissions;
        private readonly IMapper _mapper;

        public GetVacationsByUserQueryHandler(
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

        public async Task<List<VacationResponseDto>> Handle(
            GetVacationsByUserQuery request, CancellationToken ct)
        {
            var viewer = await Viewer.CurrentAsync(_userService, _permissions);

            if (request.UserId != viewer.Id)
            {
                var targetUser = await _userService.GetByIdAsync(request.UserId)
                    ?? throw new KeyNotFoundException("المستخدم غير موجود");

                if (!VacationAccess.CanViewUser(viewer, targetUser))
                    throw new UnauthorizedAccessException("لا تملك صلاحية عرض إجازات هذا المستخدم");
            }

            var list = await _service.GetByUserIdAsync(request.UserId);
            return _mapper.Map<List<VacationResponseDto>>(list);
        }
    }
}
