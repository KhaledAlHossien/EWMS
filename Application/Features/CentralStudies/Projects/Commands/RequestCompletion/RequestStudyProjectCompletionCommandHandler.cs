using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;
using MediatR;

namespace Application.Features.CentralStudies.Projects.Commands.RequestCompletion
{
    public class RequestStudyProjectCompletionCommandHandler : IRequestHandler<RequestStudyProjectCompletionCommand, StudyProjectDto>
    {
        private readonly IStudyProjectService _service;
        private readonly IStudyApprovalService _approvals;
        private readonly StudyKit _kit;
        private readonly StudyProjectSupport _support;

        public RequestStudyProjectCompletionCommandHandler(IStudyProjectService service, IStudyApprovalService approvals, StudyKit kit, StudyProjectSupport support)
        {
            _service = service;
            _approvals = approvals;
            _kit = kit;
            _support = support;
        }

        public async Task<StudyProjectDto> Handle(RequestStudyProjectCompletionCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var entity = await _support.LoadAsync(request.Id, viewer);
            if (entity.Status != StudyProjectStatus.Running)
                throw new InvalidOperationException("يُطلب اعتماد الإنجاز لمشروع جارٍ فقط");
            if (entity.Stages.Count == 0 || entity.Stages.Any(s => s.Status != StudyStageStatus.Closed))
                throw new InvalidOperationException("أغلق كل مراحل المشروع قبل طلب اعتماد الإنجاز");
            var rows = await _approvals.GetForOwnerAsync(StudyEntityType.Project, entity.Id);
            if (rows.Any(r => r.Decision == StudyDecision.Pending) && rows.All(r => r.Decision != StudyDecision.Rejected))
                throw new InvalidOperationException("طلب اعتماد الإنجاز قيد التوقيع مسبقاً");

            await _kit.ResetApprovalsAsync(StudyEntityType.Project, entity.Id);
            entity.UpdatedAt = DateTime.Now;
            await _service.UpdateAsync(entity);

            var requestId = await _support.RequestIdOf(entity);
            await _kit.LogAsync(requestId, StudyEntityType.Project, entity.Id, StudyLogAction.Submitted,
                $"طلب اعتماد إنجاز المشروع {entity.Code}");
            await _kit.NotifyNextSignersAsync(ApprovalOwner.Of(entity, requestId));
            return await _support.DetailAsync(entity.Id, viewer);
        }
    }
}
