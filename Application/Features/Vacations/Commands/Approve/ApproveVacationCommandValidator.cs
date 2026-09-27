using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Features.Vacations.Commands.Approve
{
    public class ApproveVacationCommandValidator
         : AbstractValidator<ApproveVacationCommand>
    {
        public ApproveVacationCommandValidator()
        {
            RuleFor(x => x.VacationId)
                .GreaterThan(0).WithMessage("رقم الإجازة غير صحيح");

            RuleFor(x => x.Dto)
                .NotNull().WithMessage("بيانات الموافقة مطلوبة");

            RuleFor(x => x.Dto.Reason)
                .MaximumLength(500).WithMessage("سبب الرفض طويل جداً");
        }
    }
}
