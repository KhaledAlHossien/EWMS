using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;
using MediatR;

namespace Application.Features.CentralStudies.DiwanTransactions.Commands.Create
{
    public class CreateStudyDiwanTransactionCommandHandler : IRequestHandler<CreateStudyDiwanTransactionCommand, StudyDiwanTransactionDto>
    {
        private readonly IStudyDiwanTransactionService _service;
        private readonly IStudyResponseService _responses;
        private readonly IStudyNumberService _numbers;
        private readonly StudyKit _kit;
        private readonly StudyDiwanTransactionSupport _support;

        public CreateStudyDiwanTransactionCommandHandler(IStudyDiwanTransactionService service, IStudyResponseService responses,
            IStudyNumberService numbers, StudyKit kit, StudyDiwanTransactionSupport support)
        {
            _service = service;
            _responses = responses;
            _numbers = numbers;
            _kit = kit;
            _support = support;
        }

        public async Task<StudyDiwanTransactionDto> Handle(CreateStudyDiwanTransactionCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var response = await _responses.GetByIdAsync(request.Dto.ResponseId) ?? throw new KeyNotFoundException("إشعار الرد غير موجود");
            StudyRules.EnsureInScope(viewer, response.Assignment.Request.BranchId);
            StudyRules.EnsureApproved(response.Status, "إشعار الرد");
            if (response.StartOrder != null)
                throw new InvalidOperationException("للرد أمر بدء سابق لمعاملات الديوان — لا تُنشأ له معاملة");
            if (await _service.ExistsForResponseAsync(response.Id))
                throw new InvalidOperationException("لإشعار الرد معاملة ديوان مسبقاً — معاملة واحدة لكل رد");

            var entity = new StudyDiwanTransaction
            {
                ResponseId = response.Id,
                CreatedByUserId = viewer.Id,
                Status = StudyDocumentStatus.Draft,
                CreatedAt = DateTime.Now
            };
            StudyDiwanTransactionSupport.Apply(request.Dto, entity);
            var year = DateTime.Today.Year;
            entity.TransactionNumber = StudyRules.FormatNumber(StudyDocumentType.Diwan, year,
                await _numbers.NextAsync(StudyDocumentType.Diwan, year));

            await _service.AddAsync(entity);
            await _kit.LogAsync(response.Assignment.RequestId, StudyEntityType.DiwanTransaction, entity.Id, StudyLogAction.Created,
                $"سجّل الصادر {entity.DispatchNumber} إلى الديوان المركزي في المعاملة {entity.TransactionNumber}");
            return await _support.DetailAsync(entity.Id, viewer);
        }
    }
}
