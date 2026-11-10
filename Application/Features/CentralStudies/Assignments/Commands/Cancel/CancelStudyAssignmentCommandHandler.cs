using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;
using MediatR;

namespace Application.Features.CentralStudies.Assignments.Commands.Cancel
{
    public class CancelStudyAssignmentCommandHandler : IRequestHandler<CancelStudyAssignmentCommand, StudyAssignmentDto>
    {
        private readonly IStudyAssignmentService _service;
        private readonly StudyKit _kit;
        private readonly StudyAssignmentSupport _support;

        public CancelStudyAssignmentCommandHandler(IStudyAssignmentService service, StudyKit kit, StudyAssignmentSupport support)
        {
            _service = service;
            _kit = kit;
            _support = support;
        }

        public async Task<StudyAssignmentDto> Handle(CancelStudyAssignmentCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var entity = await _support.LoadAsync(request.Id, viewer);
            if (entity.Status == StudyDocumentStatus.Cancelled) throw new InvalidOperationException("التكليف ملغى مسبقاً");
            if (entity.Status == StudyDocumentStatus.Draft) throw new InvalidOperationException("المسودة تُحذف ولا تُلغى");
            if (entity.Response != null && entity.Response.Status != StudyDocumentStatus.Cancelled)
                throw new InvalidOperationException("للتكليف إشعار رد قائم — ألغِ الرد أولاً");

            entity.Status = StudyDocumentStatus.Cancelled;
            entity.UpdatedAt = DateTime.Now;
            await _service.UpdateAsync(entity);
            await _kit.LogAsync(entity.RequestId, StudyEntityType.Assignment, entity.Id, StudyLogAction.Cancelled,
                $"ألغى التكليف {entity.AssignmentNumber}", request.Reason.Trim());
            return await _support.DetailAsync(entity.Id, viewer);
        }
    }
}
