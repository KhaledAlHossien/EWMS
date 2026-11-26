using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Vacations.Queries.Attachments
{
    /// <summary>محتوى مرفق إجازة — لمن يستطيع عرض الإجازة نفسها (VacationAccess.CanView)</summary>
    public record GetVacationAttachmentQuery(int AttachmentId) : IRequest<VacationAttachmentFileDto>;
}
