using Application.Interfaces;
using MediatR;

namespace Application.Features.Devices.Queries.GetMyAccess
{
    /// <summary>هل يشاهد/يدير المستخدم الحالي توثيق الأجهزة؟ (تستخدمه الواجهة لإظهار الصفحات والأزرار)</summary>
    public record GetMyDeviceAccessQuery : IRequest<DeviceAccess>;

    public class GetMyDeviceAccessQueryHandler : IRequestHandler<GetMyDeviceAccessQuery, DeviceAccess>
    {
        private readonly IDeviceAccessService _access;

        public GetMyDeviceAccessQueryHandler(IDeviceAccessService access)
        {
            _access = access;
        }

        public Task<DeviceAccess> Handle(GetMyDeviceAccessQuery request, CancellationToken cancellationToken)
            => _access.GetCurrentAsync();
    }
}
