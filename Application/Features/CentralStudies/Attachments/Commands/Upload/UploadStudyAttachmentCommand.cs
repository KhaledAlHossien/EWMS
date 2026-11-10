using Application.DTOs.Response;
using Application.Interfaces;
using MediatR;

namespace Application.Features.CentralStudies.Attachments.Commands.Upload
{
    public record UploadStudyAttachmentCommand(int OwnerType, int OwnerId, int AttachmentTypeId, IUploadReader Upload) : IRequest<StudyAttachmentDto>;
}
