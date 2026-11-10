using Application.Interfaces;
using MediatR;

namespace Application.Features.CentralStudies.Lookups.StageTypes.Commands.Delete
{
    public class DeleteStudyStageTypeCommandHandler : IRequestHandler<DeleteStudyStageTypeCommand, Unit>
    {
        private readonly IStudyLookupService _service;

        public DeleteStudyStageTypeCommandHandler(IStudyLookupService service)
        {
            _service = service;
        }

        public async Task<Unit> Handle(DeleteStudyStageTypeCommand request, CancellationToken ct)
        {
            var entity = await _service.GetStageTypeAsync(request.Id) ?? throw new KeyNotFoundException("نوع المرحلة غير موجود");
            if (await _service.StageTypeInUseAsync(entity.Id))
                throw new InvalidOperationException("لا يُحذف نوع مرحلة مستخدم في مشاريع — عطّله بدلاً من ذلك");
            await _service.RemoveAsync(entity);
            return Unit.Value;
        }
    }
}
