using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;
using MediatR;

namespace Application.Features.CentralStudies.Projects.Commands.Delete
{
    public class DeleteStudyProjectCommandHandler : IRequestHandler<DeleteStudyProjectCommand, Unit>
    {
        private readonly IStudyProjectService _service;
        private readonly StudyKit _kit;
        private readonly StudyProjectSupport _support;

        public DeleteStudyProjectCommandHandler(IStudyProjectService service, StudyKit kit, StudyProjectSupport support)
        {
            _service = service;
            _kit = kit;
            _support = support;
        }

        public async Task<Unit> Handle(DeleteStudyProjectCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var entity = await _support.LoadAsync(request.Id, viewer);
            if (entity.Status != StudyProjectStatus.Running)
                throw new InvalidOperationException("لا يُحذف إلا مشروع جارٍ لم يُعمل عليه — غيره يُلغى");
            if (await _service.HasContentAsync(entity.Id))
                throw new InvalidOperationException("للمشروع مهام أو مخططات أو مرفقات — ألغِه بدلاً من حذفه");
            var requestId = await _support.RequestIdOf(entity);
            var code = entity.Code;
            await _service.DeleteAsync(entity);   // المراحل والأقسام والتواقيع تُحذف معه
            await _kit.LogAsync(requestId, StudyEntityType.Project, request.Id, StudyLogAction.Deleted, $"حذف المشروع {code}");
            return Unit.Value;
        }
    }
}
