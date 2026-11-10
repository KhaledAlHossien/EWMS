using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;
using MediatR;

namespace Application.Features.CentralStudies.Attachments.Commands.Delete
{
    public class DeleteStudyAttachmentCommandHandler : IRequestHandler<DeleteStudyAttachmentCommand, Unit>
    {
        private readonly IStudyAttachmentService _service;
        private readonly StudyKit _kit;

        public DeleteStudyAttachmentCommandHandler(IStudyAttachmentService service, StudyKit kit)
        {
            _service = service;
            _kit = kit;
        }

        public async Task<Unit> Handle(DeleteStudyAttachmentCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var attachment = await _service.GetByIdAsync(request.Id) ?? throw new KeyNotFoundException("المرفق غير موجود");
            var owner = await _kit.ResolveOwnerAsync(StudyAttachmentSupport.OwnerTypeOf(attachment), StudyAttachmentSupport.OwnerIdOf(attachment));
            if (!StudyAttachmentSupport.CanEdit(viewer, owner)) throw new UnauthorizedAccessException("لا تملك صلاحية حذف هذا المرفق");
            var name = attachment.FileName;
            await _service.DeleteAsync(attachment);   // الملف في المجلد يحذفه المنظّف الدوري
            await _kit.LogAsync(owner.RequestId, StudyEntityType.Attachment, request.Id, StudyLogAction.Deleted,
                $"حذف المرفق «{name}» من {StudyRules.Name(owner.Type)}");
            return Unit.Value;
        }
    }
}
