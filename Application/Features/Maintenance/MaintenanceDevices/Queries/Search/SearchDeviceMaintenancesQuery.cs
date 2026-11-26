using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Maintenance.MaintenanceDevices.Queries.Search
{
    // أجهزة الصيانة سجل مشترك لمن يملك الصلاحية (قرار المستخدم 2026-10-04: تُمنح لموظفي قسم الصيانة فقط)،
    // فلا حدّ قسم هنا — طلبات كل جهاز تبقى محصورة بنطاق الطلبات (MaintenanceRequests/GetAll?deviceMaintenanceId=)
    public record SearchDeviceMaintenancesQuery(DeviceMaintenanceFilterDto Filter) : IRequest<PagedResultDto<DeviceMaintenanceResponseDto>>;
}
