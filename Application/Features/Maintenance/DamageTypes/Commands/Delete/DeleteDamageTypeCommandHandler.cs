using Application.Interfaces;
using Domain.Entities.Maintenance;
using MediatR;

namespace Application.Features.Maintenance.DamageTypes.Commands.Delete
{
    public class DeleteDamageTypeCommandHandler : IRequestHandler<DeleteDamageTypeCommand, Unit>
    {
        private readonly IDamageTypeService _service;

        public DeleteDamageTypeCommandHandler(IDamageTypeService service)
        {
            _service = service;
        }

        public async Task<Unit> Handle(DeleteDamageTypeCommand request, CancellationToken ct)
        {
            var entity = await LoadAsync(request.Id);

            if (await _service.IsUsedAsync(request.Id))
                throw new InvalidOperationException("لا يمكن حذف نوع عطل مستخدم في طلبات صيانة");

            await _service.DeleteAsync(entity);
            return Unit.Value;
        }

        private async Task<DamageType> LoadAsync(int id) =>
            await _service.GetByIdAsync(id) ?? throw new KeyNotFoundException("نوع العطل غير موجود");
    }
}
