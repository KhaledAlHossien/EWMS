using Domain.Entities.CentralStudiesBranch;
using FluentValidation;

namespace Application.Features.CentralStudies.Attachments.Commands.Upload
{
    public class UploadStudyAttachmentCommandValidator : AbstractValidator<UploadStudyAttachmentCommand>
    {
        public UploadStudyAttachmentCommandValidator()
        {
            RuleFor(x => x.OwnerType).Must(t => StudyAttachmentRules.Owners.Contains((StudyEntityType)t)).WithMessage("صاحب المرفق غير مدعوم");
            RuleFor(x => x.OwnerId).GreaterThan(0).WithMessage("حدد صاحب المرفق");
            RuleFor(x => x.AttachmentTypeId).GreaterThan(0).WithMessage("اختر نوع المرفق");
        }
    }
}
