using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.Attachments.Queries.GetFile
{
    public record GetStudyAttachmentFileQuery(int Id) : IRequest<StudyAttachmentContentDto>;
}
