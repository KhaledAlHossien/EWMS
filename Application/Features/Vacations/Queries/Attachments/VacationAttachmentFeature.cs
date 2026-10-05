using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using MediatR;

namespace Application.Features.Vacations.Queries.Attachments
{
    /// <summary>محتوى مرفق إجازة — لمن يستطيع عرض الإجازة نفسها (VacationAccess.CanView)</summary>
    public record GetVacationAttachmentQuery(int AttachmentId) : IRequest<VacationAttachmentFileDto>;

    public class GetVacationAttachmentQueryHandler : IRequestHandler<GetVacationAttachmentQuery, VacationAttachmentFileDto>
    {
        private readonly IVacationService _service;
        private readonly IUserService _userService;
        private readonly IUserPermissionService _permissions;

        public GetVacationAttachmentQueryHandler(IVacationService service, IUserService userService, IUserPermissionService permissions)
        {
            _service = service;
            _userService = userService;
            _permissions = permissions;
        }

        public async Task<VacationAttachmentFileDto> Handle(GetVacationAttachmentQuery request, CancellationToken ct)
        {
            var attachment = await _service.GetAttachmentAsync(request.AttachmentId)
                ?? throw new KeyNotFoundException("المرفق غير موجود");

            var viewer = await Viewer.CurrentAsync(_userService, _permissions);
            if (!VacationAccess.CanView(viewer, attachment.Vacation))
                throw new UnauthorizedAccessException("لا تملك صلاحية عرض مرفقات هذه الإجازة");

            return new VacationAttachmentFileDto(attachment.FileName, attachment.ContentType, attachment.Content.Data);
        }
    }
}
