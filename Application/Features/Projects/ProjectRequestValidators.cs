using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using MediatR;

namespace Application.Features.Projects
{
    // محققات أجسام الطلب المشتركة بين أكثر من أمر
    public class ProjectRequestDtoValidator : AbstractValidator<ProjectRequestDto>
    {
        public ProjectRequestDtoValidator()
        {
            RuleFor(x => x.Name).NotEmpty().WithMessage("اسم المشروع مطلوب").MaximumLength(200).WithMessage("اسم المشروع لا يتجاوز 200 حرف");
            RuleFor(x => x.ReferenceNumber).MaximumLength(50).WithMessage("الرقم المرجعي لا يتجاوز 50 حرفاً");
            RuleFor(x => x.Beneficiary).MaximumLength(200).WithMessage("الجهة المستفيدة لا تتجاوز 200 حرف");
            RuleFor(x => x.Description).MaximumLength(4000).WithMessage("الوصف لا يتجاوز 4000 حرف");
            RuleFor(x => x.Stage).Must(s => s == null || ProjectRules.IsValidStage(s.Value)).WithMessage("مرحلة المشروع غير صحيحة");
        }
    }
}
