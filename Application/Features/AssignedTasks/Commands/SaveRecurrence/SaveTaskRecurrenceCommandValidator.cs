using FluentValidation;

namespace Application.Features.AssignedTasks.Commands.SaveRecurrence
{
    public class SaveTaskRecurrenceCommandValidator : AbstractValidator<SaveTaskRecurrenceCommand>
    {
        public SaveTaskRecurrenceCommandValidator()
        {
            RuleFor(x => x.Dto.TemplateId).GreaterThan(0).WithMessage("اختر القالب");
            RuleFor(x => x.Dto.TargetId).GreaterThan(0).WithMessage("اختر الجهة المُسندة إليها المهمة");
            RuleFor(x => x.Dto.Frequency).InclusiveBetween(1, 3).WithMessage("نوع التكرار غير صحيح");
            RuleFor(x => x.Dto.DayOfWeek).NotNull().When(x => x.Dto.Frequency == 2).WithMessage("اختر يوم الأسبوع")
                .InclusiveBetween(0, 6).When(x => x.Dto.DayOfWeek != null).WithMessage("يوم الأسبوع غير صحيح");
            RuleFor(x => x.Dto.DayOfMonth).NotNull().When(x => x.Dto.Frequency == 3).WithMessage("اختر يوم الشهر")
                .InclusiveBetween(1, 31).When(x => x.Dto.DayOfMonth != null).WithMessage("يوم الشهر بين 1 و31");
            RuleFor(x => x.Dto.DueAfterDays).Must(d => d == null || (d >= 0 && d <= 365)).WithMessage("مدة التسليم بين 0 و365 يوماً");
            RuleFor(x => x.Dto.EndDate).Must((x, end) => end == null || end.Value.Date >= x.Dto.StartDate.Date)
                .WithMessage("تاريخ الانتهاء قبل تاريخ البدء");
        }
    }
}
