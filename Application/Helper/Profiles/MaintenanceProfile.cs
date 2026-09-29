using Application.DTOs.Request;
using Application.DTOs.Response;
using AutoMapper;
using Domain.Entities.Maintenance;

namespace Application.Helper.Profiles
{
    public class MaintenanceProfile : Profile
    {
        public MaintenanceProfile()
        {
            // ----- الجداول المساعدة -----
            CreateMap<DeviceType, DeviceTypeResponseDto>();
            CreateMap<DeviceTypeRequestDto, DeviceType>();

            CreateMap<DeviceCompany, DeviceCompanyResponseDto>();
            CreateMap<DeviceCompanyRequestDto, DeviceCompany>();

            CreateMap<DamageType, DamageTypeResponseDto>();
            CreateMap<DamageTypeRequestDto, DamageType>();

            CreateMap<MaintenanceRequestStatus, MaintenanceRequestStatusResponseDto>();
            CreateMap<MaintenanceRequestStatusRequestDto, MaintenanceRequestStatus>();

            // ----- طلب الصيانة -----
            CreateMap<MaintenanceRequest, MaintenanceRequestResponseDto>()
                .ForMember(d => d.TechnicianName, o => o.MapFrom(s => s.User != null ? s.User.FullName : string.Empty))
                .ForMember(d => d.DepartmentName, o => o.MapFrom(s => s.Department != null ? s.Department.Name : string.Empty))
                .ForMember(d => d.DeviceTypeName, o => o.MapFrom(s => s.DeviceType != null ? s.DeviceType.Name : string.Empty))
                .ForMember(d => d.DamageTypeName, o => o.MapFrom(s => s.DamageType != null ? s.DamageType.Name : string.Empty))
                .ForMember(d => d.DeviceCompanyName, o => o.MapFrom(s => s.DeviceCompany != null ? s.DeviceCompany.Name : string.Empty))
                .ForMember(d => d.StatusName, o => o.MapFrom(s => s.MaintenanceRequestStatus != null ? s.MaintenanceRequestStatus.Name : string.Empty))
                .ForMember(d => d.StatusColor, o => o.MapFrom(s => s.MaintenanceRequestStatus != null ? s.MaintenanceRequestStatus.Color : string.Empty));

            CreateMap<SaveMaintenanceRequestDto, MaintenanceRequest>();

            // ----- مهمة الصيانة -----
            CreateMap<MaintenanceTask, MaintenanceTaskResponseDto>()
                .ForMember(d => d.UserName, o => o.MapFrom(s => s.User != null ? s.User.FullName : string.Empty))
                .ForMember(d => d.DepartmentName, o => o.MapFrom(s => s.Department != null ? s.Department.Name : string.Empty));

            CreateMap<SaveMaintenanceTaskDto, MaintenanceTask>();
        }
    }
}
