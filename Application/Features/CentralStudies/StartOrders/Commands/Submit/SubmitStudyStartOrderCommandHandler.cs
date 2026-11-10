using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;
using MediatR;

namespace Application.Features.CentralStudies.StartOrders.Commands.Submit
{
    public class SubmitStudyStartOrderCommandHandler : IRequestHandler<SubmitStudyStartOrderCommand, StudyStartOrderDto>
    {
        private readonly IStudyStartOrderService _service;
        private readonly StudyKit _kit;
        private readonly StudyStartOrderSupport _support;

        public SubmitStudyStartOrderCommandHandler(IStudyStartOrderService service, StudyKit kit, StudyStartOrderSupport support)
        {
            _service = service;
            _kit = kit;
            _support = support;
        }

        public async Task<StudyStartOrderDto> Handle(SubmitStudyStartOrderCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var entity = await _support.LoadAsync(request.Id, viewer);
            StudyRules.EnsureEditable(entity.Status);
            if (entity.Engineers.Count == 0)
                throw new InvalidOperationException("خصّص مهندساً واحداً على الأقل للدراسة قبل الإرسال");

            entity.Status = StudyDocumentStatus.PendingApproval;
            entity.UpdatedAt = DateTime.Now;
            await _kit.ResetApprovalsAsync(StudyEntityType.StartOrder, entity.Id);
            await _service.UpdateAsync(entity);

            await _kit.LogAsync(entity.Response.Assignment.RequestId, StudyEntityType.StartOrder, entity.Id, StudyLogAction.Submitted,
                $"أرسل أمر البدء {entity.OrderNumber} للاعتماد");
            await _kit.NotifyNextSignersAsync(ApprovalOwner.Of(entity));
            return await _support.DetailAsync(entity.Id, viewer);
        }
    }
}
