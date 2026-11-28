using Domain.Entities.CentralStudiesBranch;
using FluentValidation;

namespace Application.Features.CentralStudies.DiwanTransactions.Commands.RequestRevision
{
    public class RequestStudyDiwanRevisionCommandValidator : AbstractValidator<RequestStudyDiwanRevisionCommand>
    {
        public RequestStudyDiwanRevisionCommandValidator()
        {
            RuleFor(x => x.Dto.Kind).Must(v => Enum.IsDefined(typeof(StudyDiwanRevision), v))
                .WithMessage("حدد نوع التعديل: 1 تخطيطي أو 2 فني");
            RuleFor(x => x.Dto.Reason).Must(r => !string.IsNullOrWhiteSpace(r)).WithMessage("اذكر سبب التعديل")
                .MaximumLength(1000).WithMessage("السبب لا يتجاوز 1000 حرف");
        }
    }
}
