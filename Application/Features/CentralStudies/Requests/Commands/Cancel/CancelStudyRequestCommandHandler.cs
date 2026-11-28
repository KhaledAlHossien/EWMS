using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;
using MediatR;

namespace Application.Features.CentralStudies.Requests.Commands.Cancel
{
    public class CancelStudyRequestCommandHandler : IRequestHandler<CancelStudyRequestCommand, StudyRequestDto>
    {
        private readonly IStudyRequestService _service;
        private readonly StudyKit _kit;
        private readonly StudyRequestSupport _support;

        public CancelStudyRequestCommandHandler(IStudyRequestService service, StudyKit kit, StudyRequestSupport support)
        {
            _service = service;
            _kit = kit;
            _support = support;
        }

        public async Task<StudyRequestDto> Handle(CancelStudyRequestCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var entity = await _support.LoadAsync(request.Id, viewer);
            if (entity.Status == StudyDocumentStatus.Cancelled)
                throw new InvalidOperationException("الطلب ملغى مسبقاً");
            if (entity.Status == StudyDocumentStatus.Draft)
                throw new InvalidOperationException("المسودة تُحذف ولا تُلغى");

            // التكليف أو المشروع القائم يُفحص بقفل سطر الطلب (القفل نفسه في إعادة تفعيل مشروع الفرع الجغرافي)
            var activity = StudyKit.CreateActivity(viewer, entity.Id, StudyEntityType.Request, entity.Id, StudyLogAction.Cancelled,
                $"ألغى الطلب {entity.RequestNumber}", request.Reason.Trim());
            await _service.CancelAsync(entity, activity, ct);
            return await _support.DetailAsync(entity.Id, viewer);
        }
    }
}
