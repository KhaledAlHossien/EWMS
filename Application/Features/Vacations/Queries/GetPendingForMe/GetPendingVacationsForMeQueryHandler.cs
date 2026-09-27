using Application.DTOs.Response;
using Application.Features.Vacations.Query.GetPendingForMe;
using Application.Interfaces;
using AutoMapper;
using Domain.Enums;
using MediatR;

namespace Application.Features.Vacations.Queries.GetPendingForMe
{
    public class GetPendingVacationsForMeQueryHandler
        : IRequestHandler<GetPendingVacationsForMeQuery, List<VacationResponseDto>>
    {
        private readonly IVacationService _service;
        private readonly IUserService _userService;
        private readonly IMapper _mapper;

        public GetPendingVacationsForMeQueryHandler(
            IVacationService service,
            IUserService userService,
            IMapper mapper)
        {
            _service = service;
            _userService = userService;
            _mapper = mapper;
        }

        public async Task<List<VacationResponseDto>> Handle(
            GetPendingVacationsForMeQuery request, CancellationToken ct)
        {
            var currentUser = await _userService.GetByIdAsync(_userService.UserId)
                ?? throw new UnauthorizedAccessException("المستخدم غير مصادق");

            var roleName = currentUser.Role?.Name ?? "";

            // ══════════════════════════════════════════════
            // المنطق حسب الدور
            // ══════════════════════════════════════════════
            List<Domain.Entities.Vacation> list;

            switch (roleName)
            {
                // ─────────── مدير: يرى إجازات قسمه فقط ───────────
                case "Manager":
                    list = await _service.GetPendingForManagerAsync(
                        currentUser.DepartmentId);
                    break;

                // ─────────── رئيس فرع: يرى كل إجازات فرعه ───────────
                case "BranchManager":
                    list = await _service.GetPendingForBranchManagerAsync(
                        currentUser.BranchId);
                    break;

                // ─────────── SuperAdmin: يرى كل شيء ───────────
                case "SuperAdmin":
                    list = await _service.GetAllPendingAsync();
                    break;

                default:
                    throw new UnauthorizedAccessException(
                        "ليس لديك صلاحية مراجعة الإجازات");
            }

            return _mapper.Map<List<VacationResponseDto>>(list);
        }
    }
}