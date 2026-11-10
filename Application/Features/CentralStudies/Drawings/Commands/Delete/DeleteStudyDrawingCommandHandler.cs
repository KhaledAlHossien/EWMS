using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;
using MediatR;

namespace Application.Features.CentralStudies.Drawings.Commands.Delete
{
    public class DeleteStudyDrawingCommandHandler : IRequestHandler<DeleteStudyDrawingCommand, Unit>
    {
        private readonly IStudyDrawingService _service;
        private readonly StudyKit _kit;
        private readonly StudyDrawingSupport _support;

        public DeleteStudyDrawingCommandHandler(IStudyDrawingService service, StudyKit kit, StudyDrawingSupport support)
        {
            _service = service;
            _kit = kit;
            _support = support;
        }

        public async Task<Unit> Handle(DeleteStudyDrawingCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var drawing = await _support.LoadAsync(request.Id, viewer);
            StudyDrawingSupport.EnsureOpen(drawing.Project);
            StudyDrawingSupport.EnsureOwnDepartment(viewer, drawing);
            if (drawing.Status == DrawingStatus.Approved) throw new InvalidOperationException("المخطط معتمد — لا يُحذف");
            if (await _service.HasVersionsAsync(drawing.Id)) throw new InvalidOperationException("للمخطط نسخ — احذفها أولاً");

            var (projectId, number) = (drawing.ProjectId, drawing.DrawingNumber);
            await _service.DeleteDrawingAsync(drawing);   // مرفقاته تُحذف معه
            await _support.LogAsync(projectId, StudyEntityType.Drawing, request.Id, StudyLogAction.Deleted, $"حذف المخطط {number}");
            return Unit.Value;
        }
    }
}
