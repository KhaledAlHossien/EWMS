using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using MediatR;

namespace Application.Features.Maintenance.Requests.Queries.GetPrint
{
    public class GetMaintenanceRequestPrintQueryHandler : IRequestHandler<GetMaintenanceRequestPrintQuery, MaintenancePrintDto>
    {
        private readonly IUserSignatureService _signatureService;
        private readonly IUserPermissionService _permissions;
        private readonly IMapper _mapper;
        private readonly MaintenanceRequestQuerySupport _support;

        public GetMaintenanceRequestPrintQueryHandler(IUserSignatureService signatureService, IUserPermissionService permissions, IMapper mapper, MaintenanceRequestQuerySupport support)
        {
            _signatureService = signatureService;
            _permissions = permissions;
            _mapper = mapper;
            _support = support;
        }

        public async Task<MaintenancePrintDto> Handle(GetMaintenanceRequestPrintQuery request, CancellationToken ct)
        {
            var (entity, scopes) = await _support.LoadForViewAsync(request.Id);

            var result = new MaintenancePrintDto { Request = MaintenanceRequestMapping.ToDto(_mapper, entity, scopes) };

            if (entity.DeliveredAt != null)
            {
                // سُلِّم: الموقّع وتوقيعه كما ثُبِّتا لحظة التسليم (لا يتغيران بعد ذلك)
                result.Delivered = true;
                result.DeliveredAt = entity.DeliveredAt;
                result.ManagerName = entity.DeliverySigner?.FullName ?? string.Empty;
                result.ManagerSignature = await _signatureService.GetImageAsync(entity.DeliverySignatureId);
            }
            else if (entity.DepartmentId is int departmentId)
            {
                // لم يُسلَّم بعد: اسم الموقّع المتوقع فقط، بلا توقيع (يُثبَّت عند تحويل الطلب إلى حالة تسليم)
                var manager = (await _permissions.GetUsersWithPermissionAsync(AppPermissions.SignMaintenanceReceipt, departmentId: departmentId))
                    .OrderBy(u => u.Id)
                    .FirstOrDefault();
                result.ManagerName = manager?.FullName ?? string.Empty;
            }

            return result;
        }
    }
}
