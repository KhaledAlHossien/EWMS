using Application.Interfaces;
using Domain.Entities.Maintenance;
using MediatR;

namespace Application.Features.Maintenance.DeviceCompanies.Commands.Delete
{
    public class DeleteDeviceCompanyCommandHandler : IRequestHandler<DeleteDeviceCompanyCommand, Unit>
    {
        private readonly IDeviceCompanyService _service;

        public DeleteDeviceCompanyCommandHandler(IDeviceCompanyService service)
        {
            _service = service;
        }

        public async Task<Unit> Handle(DeleteDeviceCompanyCommand request, CancellationToken ct)
        {
            var entity = await LoadAsync(request.Id);

            if (await _service.IsUsedAsync(request.Id))
                throw new InvalidOperationException("لا يمكن حذف شركة مستخدمة في أجهزة الصيانة");

            await _service.DeleteAsync(entity);
            return Unit.Value;
        }

        private async Task<DeviceCompany> LoadAsync(int id) =>
            await _service.GetByIdAsync(id) ?? throw new KeyNotFoundException("الشركة المصنعة غير موجودة");
    }
}
