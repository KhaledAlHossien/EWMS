using FluentValidation;

namespace Application.Features.Maintenance.SpareParts.Queries.GetReport
{
    public class GetSparePartReportQueryValidator : AbstractValidator<GetSparePartReportQuery>
    {
        public GetSparePartReportQueryValidator() =>
            RuleFor(x => x).Must(x => x.From == null || x.To == null || x.From.Value.Date <= x.To.Value.Date)
                .WithMessage("بداية الفترة لا تكون بعد نهايتها");
    }
}
