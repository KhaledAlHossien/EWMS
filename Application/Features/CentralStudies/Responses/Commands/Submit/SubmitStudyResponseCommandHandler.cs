using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;
using MediatR;

namespace Application.Features.CentralStudies.Responses.Commands.Submit
{
    public class SubmitStudyResponseCommandHandler : IRequestHandler<SubmitStudyResponseCommand, StudyResponseDto>
    {
        private readonly IStudyResponseService _service;
        private readonly StudyKit _kit;
        private readonly StudyResponseSupport _support;

        public SubmitStudyResponseCommandHandler(IStudyResponseService service, StudyKit kit, StudyResponseSupport support)
        {
            _service = service;
            _kit = kit;
            _support = support;
        }

        public async Task<StudyResponseDto> Handle(SubmitStudyResponseCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var entity = await _support.LoadAsync(request.Id, viewer);
            StudyRules.EnsureEditable(entity.Status);
            StudyResponseSupport.EnsureFollowingDepartment(viewer, entity);

            entity.Status = StudyDocumentStatus.PendingApproval;
            entity.UpdatedAt = DateTime.Now;
            await _kit.ResetApprovalsAsync(StudyEntityType.Response, entity.Id);
            await _service.UpdateAsync(entity);

            await _kit.LogAsync(entity.Assignment.RequestId, StudyEntityType.Response, entity.Id, StudyLogAction.Submitted,
                $"أرسل إشعار الرد على {entity.Assignment.AssignmentNumber} للاعتماد");
            await _kit.NotifyNextSignersAsync(ApprovalOwner.Of(entity));
            return await _support.DetailAsync(entity.Id, viewer);
        }
    }
}
