using Application.Interfaces;
using MediatR;

namespace Application.Features.VacationTypes.Commands.Delete
{
    public class DeleteVacationTypeCommandHandler
        : IRequestHandler<DeleteVacationTypeCommand, Unit>
    {
        private readonly IVacationTypeService _service;

        public DeleteVacationTypeCommandHandler(IVacationTypeService service)
        {
            _service = service;
        }

        public async Task<Unit> Handle(
            DeleteVacationTypeCommand request, CancellationToken ct)
        {
            var entity = await _service.GetByIdAsync(request.Id)
                ?? throw new KeyNotFoundException("نوع الإجازة غير موجود");

            // منع الحذف إذا كان مستخدماً في إجازات
            if (await _service.IsUsedAsync(request.Id))
                throw new InvalidOperationException(
                    "لا يمكن حذف نوع الإجازة لوجود إجازات مرتبطة به");

            await _service.DeleteAsync(entity);
            return Unit.Value;
        }
    }
}