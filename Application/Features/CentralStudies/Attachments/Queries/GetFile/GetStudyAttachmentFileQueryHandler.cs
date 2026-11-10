using Application.DTOs.Response;
using Application.Features.MapLayers;
using Application.Interfaces;
using MediatR;

namespace Application.Features.CentralStudies.Attachments.Queries.GetFile
{
    public class GetStudyAttachmentFileQueryHandler : IRequestHandler<GetStudyAttachmentFileQuery, StudyAttachmentContentDto>
    {
        private readonly IStudyAttachmentService _service;
        private readonly IFileStore _files;
        private readonly StudyKit _kit;

        public GetStudyAttachmentFileQueryHandler(IStudyAttachmentService service, IFileStore files, StudyKit kit)
        {
            _service = service;
            _files = files;
            _kit = kit;
        }

        public async Task<StudyAttachmentContentDto> Handle(GetStudyAttachmentFileQuery request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var attachment = await _service.GetByIdAsync(request.Id) ?? throw new KeyNotFoundException("المرفق غير موجود");
            var owner = await _kit.ResolveOwnerAsync(StudyAttachmentSupport.OwnerTypeOf(attachment), StudyAttachmentSupport.OwnerIdOf(attachment));
            if (!StudyAttachmentSupport.CanView(viewer, owner)) throw new UnauthorizedAccessException("لا تملك صلاحية عرض هذا المرفق");
            var stream = _files.OpenRead(attachment.StorageKey);
            return new StudyAttachmentContentDto(attachment.FileName, attachment.ContentType, stream, MapFileRules.IsPreviewable(attachment.ContentType));
        }
    }
}
