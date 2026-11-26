using Application.DTOs.Response;
using Application.Interfaces;
using MediatR;

namespace Application.Features.CentralStudies.Responses.Queries.GetDiwanReplyFile
{
    public sealed class GetStudyDiwanReplyFileQueryHandler(StudyResponseSupport support, StudyKit kit, IFileStore files) : IRequestHandler<GetStudyDiwanReplyFileQuery, StudyAttachmentContentDto>
    {
        public async Task<StudyAttachmentContentDto> Handle(GetStudyDiwanReplyFileQuery request, CancellationToken ct)
        {
            var viewer = await kit.ViewerAsync();
            StudyRules.Require(viewer, StudyPermissions.ViewResponses);
            var response = await support.LoadAsync(request.Id, viewer);
            var reply = response.DiwanCase?.Replies.SingleOrDefault(r => r.Id == request.ReplyId)
                ?? throw new KeyNotFoundException("كتاب الرد المركزي غير موجود");
            return new(reply.FileName, reply.ContentType, files.OpenRead(reply.StorageKey), true);
        }
    }
}
