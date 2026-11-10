using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;
using MediatR;

namespace Application.Features.CentralStudies.Drawings.Commands.Review
{
    public class ReviewStudyDrawingCommandHandler : IRequestHandler<ReviewStudyDrawingCommand, StudyDrawingDto>
    {
        private readonly IStudyDrawingService _service;
        private readonly StudyKit _kit;
        private readonly StudyDrawingSupport _support;

        public ReviewStudyDrawingCommandHandler(IStudyDrawingService service, StudyKit kit, StudyDrawingSupport support)
        {
            _service = service;
            _kit = kit;
            _support = support;
        }

        public async Task<StudyDrawingDto> Handle(ReviewStudyDrawingCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var drawing = await _support.LoadAsync(request.Id, viewer);
            StudyDrawingSupport.EnsureOpen(drawing.Project);
            if (drawing.Kind == DrawingKind.Architectural)
                throw new InvalidOperationException("المخطط المعماري يُعتمد باعتماد المخططات الأولي (CDA)");
            if (drawing.Status != DrawingStatus.PendingReview)
                throw new InvalidOperationException("المخطط ليس بانتظار المراجعة");

            drawing.Status = request.Dto.Approve ? DrawingStatus.Approved : DrawingStatus.Rejected;
            drawing.UpdatedAt = DateTime.Now;
            await _service.UpdateDrawingAsync(drawing);
            await _support.LogAsync(drawing.ProjectId, StudyEntityType.Drawing, drawing.Id,
                request.Dto.Approve ? StudyLogAction.Approved : StudyLogAction.Rejected,
                $"{(request.Dto.Approve ? "اعتمد" : "رفض")} المخطط {drawing.DrawingNumber}", request.Dto.Note?.Trim());
            return await _support.DetailAsync(drawing.Id, viewer);
        }
    }
}
