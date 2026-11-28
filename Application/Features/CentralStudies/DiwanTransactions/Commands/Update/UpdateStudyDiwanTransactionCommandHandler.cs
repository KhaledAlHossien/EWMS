using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;
using MediatR;

namespace Application.Features.CentralStudies.DiwanTransactions.Commands.Update
{
    public class UpdateStudyDiwanTransactionCommandHandler : IRequestHandler<UpdateStudyDiwanTransactionCommand, StudyDiwanTransactionDto>
    {
        private readonly IStudyDiwanTransactionService _service;
        private readonly StudyKit _kit;
        private readonly StudyDiwanTransactionSupport _support;

        public UpdateStudyDiwanTransactionCommandHandler(IStudyDiwanTransactionService service, StudyKit kit, StudyDiwanTransactionSupport support)
        {
            _service = service;
            _kit = kit;
            _support = support;
        }

        public async Task<StudyDiwanTransactionDto> Handle(UpdateStudyDiwanTransactionCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var entity = await _support.LoadAsync(request.Id, viewer);
            StudyRules.EnsureEditable(entity.Status);
            StudyDiwanTransactionSupport.EnsureResponseApproved(entity);

            StudyDiwanTransactionSupport.Apply(request.Dto, entity);
            entity.Status = StudyDocumentStatus.Draft;
            entity.UpdatedAt = DateTime.Now;
            await _service.UpdateAsync(entity, request.Dto.RowVersion);
            await _kit.LogAsync(entity.Response.Assignment.RequestId, StudyEntityType.DiwanTransaction, entity.Id, StudyLogAction.Edited,
                $"عدّل معاملة الديوان {entity.TransactionNumber}",
                entity.ReplyNumber == null ? $"الصادر {entity.DispatchNumber}" : $"الصادر {entity.DispatchNumber}، الرد المركزي {entity.ReplyNumber}");
            return await _support.DetailAsync(entity.Id, viewer);
        }
    }
}
