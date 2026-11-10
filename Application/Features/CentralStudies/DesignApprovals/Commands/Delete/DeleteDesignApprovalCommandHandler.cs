using Application.Features.CentralStudies.Drawings;
using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;
using MediatR;

namespace Application.Features.CentralStudies.DesignApprovals.Commands.Delete
{
    public class DeleteDesignApprovalCommandHandler : IRequestHandler<DeleteDesignApprovalCommand, Unit>
    {
        private readonly IStudyDrawingService _service;
        private readonly StudyKit _kit;
        private readonly StudyDrawingSupport _support;

        public DeleteDesignApprovalCommandHandler(IStudyDrawingService service, StudyKit kit, StudyDrawingSupport support)
        {
            _service = service;
            _kit = kit;
            _support = support;
        }

        public async Task<Unit> Handle(DeleteDesignApprovalCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var (cda, requestId) = await _support.LoadCdaAsync(request.Id, viewer);
            StudyDrawingSupport.EnsureOpen(cda.Version.Drawing.Project);
            await _support.EnsureNoDecisionsAsync(cda);

            // النسخة تعود قيد الإعداد، والمخطط معها ما لم يكن له نسخة معتمدة
            cda.Version.Status = DrawingStatus.InPreparation;
            if (cda.Version.Drawing.Status != DrawingStatus.Approved) cda.Version.Drawing.Status = DrawingStatus.InPreparation;
            var number = cda.CdaNumber;
            await _service.DeleteDesignApprovalAsync(cda);   // سطور التوقيع تُحذف معه
            await _kit.LogAsync(requestId, StudyEntityType.DesignApproval, request.Id, StudyLogAction.Deleted, $"حذف اعتماد المخططات {number}");
            return Unit.Value;
        }
    }
}
