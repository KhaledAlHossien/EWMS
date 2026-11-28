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
            StudyDiwanTransactionSupport.EnsureAtDiwan(entity);
            StudyDiwanTransactionSupport.EnsureResponseApproved(entity);

            // صادر جديد بعد اعتماد الرد المعدّل: جولة جديدة على المعاملة نفسها، برقم صادر غير صادر الجولة السابقة
            var last = StudyRules.LastRound(entity);
            if (last != null && string.Equals(request.Dto.DispatchNumber.Trim(), last.DispatchNumber, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"سجّل صادراً جديداً: الرقم {last.DispatchNumber} هو صادر الجولة {last.RoundNumber}");
            var newRound = entity.Stage == StudyDiwanStage.ReadyToResend;
            if (newRound) entity.RoundNumber = (last?.RoundNumber ?? entity.RoundNumber) + 1;

            StudyDiwanTransactionSupport.Apply(request.Dto, entity);
            entity.CentralDecision = null;   // قرار الجولة يحدده رئيس الفرع عند الإحالة
            StudyDiwanTransactionSupport.MoveTo(entity, StudyDiwanStage.Registering);
            await _service.UpdateAsync(entity, request.Dto.RowVersion);
            await _kit.LogAsync(entity.Response.Assignment.RequestId, StudyEntityType.DiwanTransaction, entity.Id, StudyLogAction.Edited,
                newRound
                    ? $"سجّل الصادر الجديد {entity.DispatchNumber} في معاملة الديوان {entity.TransactionNumber} (الجولة {entity.RoundNumber})"
                    : $"عدّل معاملة الديوان {entity.TransactionNumber}",
                entity.ReplyNumber == null ? $"الصادر {entity.DispatchNumber}" : $"الصادر {entity.DispatchNumber}، الرد المركزي {entity.ReplyNumber}");
            return await _support.DetailAsync(entity.Id, viewer);
        }
    }
}
