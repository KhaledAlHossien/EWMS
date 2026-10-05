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

            CreateMap<MaintenanceRequestStatus, MaintenanceRequestStatusResponseDto>()
                .ForMember(d => d.Stage, o => o.MapFrom(s => (int)s.Stage));
            CreateMap<MaintenanceRequestStatusRequestDto, MaintenanceRequestStatus>()
                .ForMember(d => d.Stage, o => o.MapFrom(s => (MaintenanceStage)s.Stage));

            // ----- جهاز الصيانة -----
            CreateMap<DeviceMaintenance, DeviceMaintenanceResponseDto>()
                .ForMember(d => d.DeviceTypeName, o => o.MapFrom(s => s.DeviceType != null ? s.DeviceType.Name : string.Empty))
                .ForMember(d => d.DeviceCompanyName, o => o.MapFrom(s => s.DeviceCompany != null ? s.DeviceCompany.Name : string.Empty));
            CreateMap<DeviceMaintenanceRequestDto, DeviceMaintenance>();

            // ----- طلب الصيانة -----
            CreateMap<MaintenanceRequest, MaintenanceRequestResponseDto>()
                .ForMember(d => d.Number, o => o.MapFrom(s => Application.Features.Maintenance.MaintenanceRules.RequestNumber(s.Id, s.CreatedAt)))
                .ForMember(d => d.CanEdit, o => o.Ignore())
                .ForMember(d => d.CanDelete, o => o.Ignore())
                .ForMember(d => d.CanAssign, o => o.Ignore())
                .ForMember(d => d.TechnicianName, o => o.MapFrom(s => s.User != null ? s.User.FullName : string.Empty))
                .ForMember(d => d.DepartmentName, o => o.MapFrom(s => s.Department != null ? s.Department.Name : string.Empty))
                .ForMember(d => d.ClientDepartmentName, o => o.MapFrom(s => s.ClientUser != null && s.ClientUser.Department != null ? s.ClientUser.Department.Name : string.Empty))
                .ForMember(d => d.DeviceName, o => o.MapFrom(s => s.DeviceMaintenance != null ? s.DeviceMaintenance.Name : string.Empty))
                .ForMember(d => d.SerialNumber, o => o.MapFrom(s => s.DeviceMaintenance != null ? s.DeviceMaintenance.SerialNumber : string.Empty))
                .ForMember(d => d.Model, o => o.MapFrom(s => s.DeviceMaintenance != null ? s.DeviceMaintenance.Model : string.Empty))
                .ForMember(d => d.DeviceTypeId, o => o.MapFrom(s => s.DeviceMaintenance != null ? s.DeviceMaintenance.DeviceTypeId : 0))
                .ForMember(d => d.DeviceTypeName, o => o.MapFrom(s => s.DeviceMaintenance != null && s.DeviceMaintenance.DeviceType != null ? s.DeviceMaintenance.DeviceType.Name : string.Empty))
                .ForMember(d => d.DeviceCompanyId, o => o.MapFrom(s => s.DeviceMaintenance != null ? s.DeviceMaintenance.DeviceCompanyId : 0))
                .ForMember(d => d.DeviceCompanyName, o => o.MapFrom(s => s.DeviceMaintenance != null && s.DeviceMaintenance.DeviceCompany != null ? s.DeviceMaintenance.DeviceCompany.Name : string.Empty))
                .ForMember(d => d.DamageTypeName, o => o.MapFrom(s => s.DamageType != null ? s.DamageType.Name : string.Empty))
                .ForMember(d => d.StatusName, o => o.MapFrom(s => s.MaintenanceRequestStatus != null ? s.MaintenanceRequestStatus.Name : string.Empty))
                .ForMember(d => d.StatusColor, o => o.MapFrom(s => s.MaintenanceRequestStatus != null ? s.MaintenanceRequestStatus.Color : string.Empty))
                .ForMember(d => d.StatusStage, o => o.MapFrom(s => s.MaintenanceRequestStatus != null ? (int)s.MaintenanceRequestStatus.Stage : 0));

            CreateMap<SaveMaintenanceRequestDto, MaintenanceRequest>();

            // ----- مهمة الصيانة -----
            CreateMap<MaintenanceTask, MaintenanceTaskResponseDto>()
                .ForMember(d => d.CanEdit, o => o.Ignore())
                .ForMember(d => d.CanDelete, o => o.Ignore())
                .ForMember(d => d.CanAssign, o => o.Ignore())
                .ForMember(d => d.UserName, o => o.MapFrom(s => s.User != null ? s.User.FullName : string.Empty))
                .ForMember(d => d.DepartmentName, o => o.MapFrom(s => s.Department != null ? s.Department.Name : string.Empty));

            CreateMap<SaveMaintenanceTaskDto, MaintenanceTask>();

            CreateMap<MaintenanceRequestActivity, MaintenanceActivityDto>()
                .ForMember(d => d.Type, o => o.MapFrom(s => (int)s.Type))
                .ForMember(d => d.UserName, o => o.MapFrom(s => s.User != null ? s.User.FullName : string.Empty));
        }
    }
}
