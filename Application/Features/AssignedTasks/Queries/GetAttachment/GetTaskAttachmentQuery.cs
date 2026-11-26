using Application.DTOs.Response;
using MediatR;

namespace Application.Features.AssignedTasks.Queries.GetAttachment
{
    public record GetTaskAttachmentQuery(int AttachmentId) : IRequest<TaskAttachmentFileDto>;
}
