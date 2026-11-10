using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;
using MediatR;

namespace Application.Features.CentralStudies.Assignments.Commands.Delete
{
    public class DeleteStudyAssignmentCommandHandler : IRequestHandler<DeleteStudyAssignmentCommand, Unit>
    {
        private readonly IStudyAssignmentService _service;
        private readonly StudyKit _kit;
        private readonly StudyAssignmentSupport _support;

        public DeleteStudyAssignmentCommandHandler(IStudyAssignmentService service, StudyKit kit, StudyAssignmentSupport support)
        {
            _service = service;
            _kit = kit;
            _support = support;
        }

        public async Task<Unit> Handle(DeleteStudyAssignmentCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var entity = await _support.LoadAsync(request.Id, viewer);
            StudyRules.EnsureDraftForDelete(entity.Status);
            if (entity.Response != null) throw new InvalidOperationException("للتكليف إشعار رد — لا يُحذف");
            var requestId = entity.RequestId;
            var number = entity.AssignmentNumber;
            await _service.DeleteAsync(entity);
            await _kit.LogAsync(requestId, StudyEntityType.Assignment, request.Id, StudyLogAction.Deleted, $"حذف مسودة التكليف {number}");
            return Unit.Value;
        }
    }
}
