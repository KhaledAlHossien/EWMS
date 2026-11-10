using Application.DTOs.Response;
using Application.Interfaces;
using MediatR;

namespace Application.Features.CentralStudies.Attachments.Queries.GetByOwner
{
    public class GetStudyAttachmentsQueryHandler : IRequestHandler<GetStudyAttachmentsQuery, List<StudyAttachmentDto>>
    {
        private readonly IStudyAttachmentService _service;
        private readonly StudyKit _kit;
        private readonly StudyAttachmentSupport _support;

        public GetStudyAttachmentsQueryHandler(IStudyAttachmentService service, StudyKit kit, StudyAttachmentSupport support)
        {
            _service = service;
            _kit = kit;
            _support = support;
        }

        public async Task<List<StudyAttachmentDto>> Handle(GetStudyAttachmentsQuery request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var owner = await _support.OwnerAsync(request.OwnerType, request.OwnerId);
            if (!StudyAttachmentSupport.CanView(viewer, owner)) throw new UnauthorizedAccessException("لا تملك صلاحية عرض مرفقات هذا السجل");
            var canEdit = StudyAttachmentSupport.CanEdit(viewer, owner);
            return (await _service.GetForOwnerAsync(owner.Type, owner.Id)).Select(a =>
            {
                var dto = _kit.Mapper.Map<StudyAttachmentDto>(a);
                dto.CanDelete = canEdit;
                return dto;
            }).ToList();
        }
    }
}
