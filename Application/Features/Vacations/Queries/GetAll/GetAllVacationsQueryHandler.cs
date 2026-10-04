using Application.DTOs.Response;
using Application.Features.Vacations.Query.GetAll;
using Application.Interfaces;
using AutoMapper;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

using Application.Common;

namespace Application.Features.Vacations.Queries.GetAll
{
    // "الإجازات التي تخصني" حسب صلاحياتي (VacationAccess): SuperAdmin الكل، ViewBranchVacations أو الموافقة → فرعي،
    // ViewDepartmentVacations → قسمي، وإجازاتي دائماً.
    public class GetAllVacationsQueryHandler
       : IRequestHandler<GetAllVacationsQuery, List<VacationResponseDto>>
    {
        private readonly IVacationService _service;
        private readonly IUserService _userService;
        private readonly IUserPermissionService _permissions;
        private readonly IMapper _mapper;

        public GetAllVacationsQueryHandler(
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
            GetAllVacationsQuery request, CancellationToken ct)
        {
            var viewer = await Viewer.CurrentAsync(_userService, _permissions);
            var user = viewer.User;

            List<Domain.Entities.Vacation> list;
            if (viewer.IsSuperAdmin)
            {
                list = await _service.GetAllAsync();
            }
            else
            {
                if (!VacationAccess.CanUseVacations(viewer))
                    throw new UnauthorizedAccessException("لا تملك صلاحية عرض الإجازات");

                list = VacationAccess.CanSeeBranch(viewer, user.BranchId) ? await _service.GetByBranchIdAsync(user.BranchId!.Value)
                    : VacationAccess.CanSeeDepartment(viewer, user.DepartmentId) ? await _service.GetByDepartmentIdAsync(user.DepartmentId!.Value)
                    : [];

                // إجازاتي دائماً (قد تكون بقسم/فرع سابق قبل النقل)
                var ids = list.Select(v => v.Id).ToHashSet();
                list.AddRange((await _service.GetByUserIdAsync(user.Id)).Where(v => !ids.Contains(v.Id)));
            }

            return _mapper.Map<List<VacationResponseDto>>(list);
        }
    }
}
