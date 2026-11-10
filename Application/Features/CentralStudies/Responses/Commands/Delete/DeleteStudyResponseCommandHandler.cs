using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;
using MediatR;

namespace Application.Features.CentralStudies.Responses.Commands.Delete
{
    public class DeleteStudyResponseCommandHandler : IRequestHandler<DeleteStudyResponseCommand, Unit>
    {
        private readonly IStudyResponseService _service;
        private readonly StudyKit _kit;
        private readonly StudyResponseSupport _support;

        public DeleteStudyResponseCommandHandler(IStudyResponseService service, StudyKit kit, StudyResponseSupport support)
        {
            _service = service;
            _kit = kit;
            _support = support;
        }

        public async Task<Unit> Handle(DeleteStudyResponseCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var entity = await _support.LoadAsync(request.Id, viewer);
            StudyRules.EnsureDraftForDelete(entity.Status);
            if (entity.StartOrder != null) throw new InvalidOperationException("للرد أمر بدء — لا يُحذف");
            var requestId = entity.Assignment.RequestId;
            var number = entity.Assignment.AssignmentNumber;
            await _service.DeleteAsync(entity);
            await _kit.LogAsync(requestId, StudyEntityType.Response, request.Id, StudyLogAction.Deleted, $"حذف مسودة الرد على {number}");
            return Unit.Value;
        }
    }
}
