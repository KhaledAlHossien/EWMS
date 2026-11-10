using Application.DTOs.Request;
using FluentValidation;

namespace Application.Features.CentralStudies.ArchitecturalVersions
{
    public class ArchitecturalVersionFormValidator : AbstractValidator<ArchitecturalVersionFormDto>
    {
        public ArchitecturalVersionFormValidator()
        {
            RuleFor(x => x.VersionDate).NotEmpty().WithMessage("تاريخ النسخة مطلوب");
            RuleFor(x => x.Content).MaximumLength(4000).WithMessage("المحتوى لا يتجاوز 4000 حرف");
        }
    }
}
