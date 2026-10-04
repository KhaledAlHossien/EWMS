using Application.DTOs.Response;
using Application.Features.Vacations.Query.GetPendingForMe;
using Application.Interfaces;
using AutoMapper;
using Domain.Enums;
using MediatR;

using Application.Common;

namespace Application.Features.Vacations.Queries.GetPendingForMe
{
    /// <summary>
    /// الإجازات التي تنتظر قراري: ApproveVacationFirst → المرحلة الأولى لإجازات فرعي،
    /// ApproveVacationFinal → الاعتماد النهائي لإجازات فرعي (إلا ما وافقتُ عليه بنفسي في المرحلة الأولى). SuperAdmin الكل.
    /// </summary>
    public class GetPendingVacationsForMeQueryHandler
        : IRequestHandler<GetPendingVacationsForMeQuery, List<VacationResponseDto>>
    {
        private readonly IVacationService _service;
        private readonly IUserService _userService;
        private readonly IUserPermissionService _permissions;
        private readonly IMapper _mapper;

        public GetPendingVacationsForMeQueryHandler(
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
            GetPendingVacationsForMeQuery request, CancellationToken ct)
        {
            var viewer = await Viewer.CurrentAsync(_userService, _permissions);

            if (viewer.IsSuperAdmin)
                return _mapper.Map<List<VacationResponseDto>>(await _service.GetAllPendingAsync());

            if (!VacationAccess.CanApproveAny(viewer))
                throw new UnauthorizedAccessException("ليس لديك صلاحية مراجعة الإجازات");

            if (viewer.User.BranchId is not int branchId)
                throw new InvalidOperationException("حسابك غير مرتبط بفرع");

            var list = new List<Domain.Entities.Vacation>();
            if (viewer.Has(AppPermissions.ApproveVacationFirst))
                list.AddRange(await _service.GetPendingInBranchAsync(VacationStatus.PendingManager, branchId));
            if (viewer.Has(AppPermissions.ApproveVacationFinal))
                list.AddRange((await _service.GetPendingInBranchAsync(VacationStatus.PendingBranchManager, branchId))
                    .Where(v => v.FirstApprovedByUserId != viewer.Id));

            // لا يراجع أحد إجازته الخاصة
            var result = list.Where(v => v.UserId != viewer.Id).OrderBy(v => v.StartVac).ToList();
            return _mapper.Map<List<VacationResponseDto>>(result);
        }
    }
}
