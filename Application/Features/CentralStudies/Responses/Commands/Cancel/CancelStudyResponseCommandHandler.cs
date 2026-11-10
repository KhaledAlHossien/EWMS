using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;
using MediatR;

namespace Application.Features.CentralStudies.Responses.Commands.Cancel
{
    public class CancelStudyResponseCommandHandler : IRequestHandler<CancelStudyResponseCommand, StudyResponseDto>
    {
        private readonly IStudyResponseService _service;
        private readonly StudyKit _kit;
        private readonly StudyResponseSupport _support;

        public CancelStudyResponseCommandHandler(IStudyResponseService service, StudyKit kit, StudyResponseSupport support)
        {
            _service = service;
            _kit = kit;
            _support = support;
        }

        public async Task<StudyResponseDto> Handle(CancelStudyResponseCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var entity = await _support.LoadAsync(request.Id, viewer);
            if (entity.Status == StudyDocumentStatus.Cancelled) throw new InvalidOperationException("الرد ملغى مسبقاً");
            if (entity.Status == StudyDocumentStatus.Draft) throw new InvalidOperationException("المسودة تُحذف ولا تُلغى");
            if (entity.StartOrder != null && entity.StartOrder.Status != StudyDocumentStatus.Cancelled)
                throw new InvalidOperationException("للرد أمر بدء قائم — ألغِ أمر البدء أولاً");

            entity.Status = StudyDocumentStatus.Cancelled;
            entity.UpdatedAt = DateTime.Now;
            await _service.UpdateAsync(entity);
            await _kit.LogAsync(entity.Assignment.RequestId, StudyEntityType.Response, entity.Id, StudyLogAction.Cancelled,
                $"ألغى إشعار الرد على {entity.Assignment.AssignmentNumber}", request.Reason.Trim());
            return await _support.DetailAsync(entity.Id, viewer);
        }
    }
}
