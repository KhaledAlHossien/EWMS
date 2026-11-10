using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;
using MediatR;

namespace Application.Features.CentralStudies.StartOrders.Commands.Update
{
    public class UpdateStudyStartOrderCommandHandler : IRequestHandler<UpdateStudyStartOrderCommand, StudyStartOrderDto>
    {
        private readonly IStudyStartOrderService _service;
        private readonly StudyKit _kit;
        private readonly StudyStartOrderSupport _support;

        public UpdateStudyStartOrderCommandHandler(IStudyStartOrderService service, StudyKit kit, StudyStartOrderSupport support)
        {
            _service = service;
            _kit = kit;
            _support = support;
        }

        public async Task<StudyStartOrderDto> Handle(UpdateStudyStartOrderCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var entity = await _support.LoadAsync(request.Id, viewer);
            StudyRules.EnsureEditable(entity.Status);
            await _support.ValidateAsync(request.Dto, entity.Response.Assignment.Request.BranchId);

            StudyStartOrderSupport.Apply(request.Dto, entity);
            entity.Status = StudyDocumentStatus.Draft;
            entity.UpdatedAt = DateTime.Now;
            await _service.UpdateAsync(entity, request.Dto.RowVersion);
            await _kit.LogAsync(entity.Response.Assignment.RequestId, StudyEntityType.StartOrder, entity.Id, StudyLogAction.Edited,
                $"عدّل أمر البدء {entity.OrderNumber}");
            return await _support.DetailAsync(entity.Id, viewer);
        }
    }
}
