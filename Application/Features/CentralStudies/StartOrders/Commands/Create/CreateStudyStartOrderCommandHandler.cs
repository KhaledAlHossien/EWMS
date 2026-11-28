using Application.DTOs.Response;
using Application.Features.CentralStudies.DiwanTransactions;
using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;
using MediatR;

namespace Application.Features.CentralStudies.StartOrders.Commands.Create
{
    public class CreateStudyStartOrderCommandHandler : IRequestHandler<CreateStudyStartOrderCommand, StudyStartOrderDto>
    {
        private readonly IStudyStartOrderService _service;
        private readonly IStudyDiwanTransactionService _diwan;
        private readonly IStudyNumberService _numbers;
        private readonly StudyKit _kit;
        private readonly StudyStartOrderSupport _support;

        public CreateStudyStartOrderCommandHandler(IStudyStartOrderService service, IStudyDiwanTransactionService diwan, IStudyNumberService numbers, StudyKit kit, StudyStartOrderSupport support)
        {
            _service = service;
            _diwan = diwan;
            _numbers = numbers;
            _kit = kit;
            _support = support;
        }

        public async Task<StudyStartOrderDto> Handle(CreateStudyStartOrderCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var diwan = await _diwan.GetByIdAsync(request.Dto.DiwanTransactionId)
                ?? throw new KeyNotFoundException("معاملة الديوان غير موجودة");
            var response = diwan.Response;
            var branchId = response.Assignment.Request.BranchId;
            StudyRules.EnsureInScope(viewer, branchId);
            StudyRules.EnsureApproved(response.Status, "إشعار الرد");
            if (response.Recommendation == StudyRecommendation.NotAvailable)
                throw new InvalidOperationException("التوصية الفنية «غير متاح» — لا يصدر أمر بدء لهذه الدراسة");
            if (!StudyDiwanTransactionSupport.ReadyForStartOrder(diwan))
                throw new InvalidOperationException("يتطلب أمر البدء معاملة ديوان معتمدة بقرار موافقة مركزي");
            if (await _service.ExistsForResponseAsync(response.Id))
                throw new InvalidOperationException("للرد أمر بدء مسبقاً — أمر واحد لكل رد");
            await _support.ValidateAsync(request.Dto, branchId);

            var entity = new StudyStartOrder
            {
                ResponseId = response.Id,
                DiwanTransactionId = diwan.Id,
                CreatedByUserId = viewer.Id,
                Status = StudyDocumentStatus.Draft,
                CreatedAt = DateTime.Now
            };
            StudyStartOrderSupport.Apply(request.Dto, entity);
            var year = DateTime.Today.Year;
            entity.OrderNumber = StudyRules.FormatNumber(StudyDocumentType.StartOrder, year,
                await _numbers.NextAsync(StudyDocumentType.StartOrder, year));

            response.UpdatedAt = DateTime.Now;
            await _service.AddAsync(entity);
            await _kit.LogAsync(response.Assignment.RequestId, StudyEntityType.StartOrder, entity.Id, StudyLogAction.Created,
                $"أصدر أمر البدء {entity.OrderNumber}");
            return await _support.DetailAsync(entity.Id, viewer);
        }
    }
}
