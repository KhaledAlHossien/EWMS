using MediatR;

namespace Application.Features.CentralStudies.Attachments.Commands.Delete
{
    public record DeleteStudyAttachmentCommand(int Id) : IRequest<Unit>;
}
