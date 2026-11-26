using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.Responses.Queries.GetDiwanReplyFile
{
    public record GetStudyDiwanReplyFileQuery(int Id, int ReplyId) : IRequest<StudyAttachmentContentDto>;
}
