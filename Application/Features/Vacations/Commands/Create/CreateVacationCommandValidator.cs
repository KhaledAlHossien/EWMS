using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Features.Vacations.Commands.Create
{
    public class CreateVacationCommandValidator
      : AbstractValidator<CreateVacationCommand>
    {
        public CreateVacationCommandValidator()
        {
            RuleFor(x => x.VacationDto.VacationTypeId)
                .GreaterThan(0).WithMessage("نوع الإجازة مطلوب");

            RuleFor(x => x.VacationDto.StartVac)
                .NotEmpty().WithMessage("تاريخ البداية مطلوب");

            RuleFor(x => x.VacationDto.EndVac)
                .NotEmpty().WithMessage("تاريخ النهاية مطلوب")
                .GreaterThanOrEqualTo(x => x.VacationDto.StartVac)
                .WithMessage("تاريخ النهاية يجب أن يكون بعد أو يساوي تاريخ البداية");

            RuleFor(x => x.VacationDto.VacReason)
                .MaximumLength(500).WithMessage("السبب طويل جداً");

            // المرفقات: حتى 3 ملفات، PDF أو JPG/PNG، كلٌّ حتى 5MB (VacationAttachmentRules)
            RuleFor(x => x.Attachments.Count)
                .LessThanOrEqualTo(VacationAttachmentRules.MaxFiles)
                .WithMessage($"يمكن إرفاق {VacationAttachmentRules.MaxFiles} ملفات على الأكثر");
            RuleForEach(x => x.Attachments)
                .Must(f => VacationAttachmentRules.Problem(f) == null)
                .WithMessage((_, f) => VacationAttachmentRules.Problem(f)!);
        }
    }
}
