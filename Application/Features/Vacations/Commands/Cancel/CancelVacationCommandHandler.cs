using Application.Interfaces;
using Domain.Enums;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Features.Vacations.Commands.Cancel
{
    public class CancelVacationCommandHandler : IRequestHandler<CancelVacationCommand, Unit>
    {
        private readonly IVacationService _service;
        private readonly IUserService _userService;

        public CancelVacationCommandHandler(IVacationService service, IUserService userService)
        {
            _service = service; _userService = userService;
        }

        public async Task<Unit> Handle(CancelVacationCommand request, CancellationToken ct)
        {
            var vacation = await _service.GetByIdAsync(request.VacationId)
                ?? throw new KeyNotFoundException("الإجازة غير موجودة");

            // فقط صاحب الإجازة يستطيع الإلغاء
            if (vacation.UserId != _userService.UserId)
                throw new UnauthorizedAccessException("لا تستطيع إلغاء إجازة غيرك");

            // لا يمكن الإلغاء بعد الاعتماد النهائي
            if (vacation.Status == VacationStatus.Approved)
                throw new InvalidOperationException("لا يمكن إلغاء إجازة معتمدة نهائياً");

            if (vacation.Status == VacationStatus.Rejected)
                throw new InvalidOperationException("الإجازة مرفوضة مسبقاً");

            await _service.DeleteAsync(vacation);
            return Unit.Value;
        }
    }
}
