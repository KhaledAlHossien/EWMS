using Application.DTOs.Response;
using MediatR;

namespace Application.Features.AssignedTasks.Commands.DeleteAttachment
{
    public record DeleteTaskAttachmentCommand(int AttachmentId) : IRequest<AssignedTaskDetailDto>;
}
