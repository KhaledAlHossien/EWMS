using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.AssignedTasks.Commands.UploadAttachments
{
    // مرفقات المهمة، قائمة التحقق، و«أتولّى هذه المهمة» (قرارات المستخدم 2026-10-07). كلها تُرجع تفصيل المهمة محدَّثاً.
    public record UploadTaskAttachmentsCommand(int TaskId, List<UploadedFileDto> Files) : IRequest<AssignedTaskDetailDto>;
}
