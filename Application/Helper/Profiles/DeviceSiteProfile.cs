using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using AutoMapper;
using Domain.Entities;

namespace Application.Helper.Profiles
{
    public class DeviceSiteProfile : Profile
    {
        public DeviceSiteProfile()
        {
            CreateMap<DeviceSite, DeviceSiteResponseDto>()
                .ForMember(dest => dest.DeviceName,
                    opt => opt.MapFrom(src => src.Device != null ? src.Device.Name : string.Empty))
                .ForMember(dest => dest.DeviceModel,
                    opt => opt.MapFrom(src => src.Device != null ? src.Device.Model : string.Empty))
                .ForMember(dest => dest.SiteName,
                    opt => opt.MapFrom(src => src.Site != null ? src.Site.Name : string.Empty))
                .ForMember(dest => dest.GovernorateCode,
                    opt => opt.MapFrom(src => src.Site != null ? src.Site.GovernorateCode : string.Empty))
                .ForMember(dest => dest.GovernorateName,
                    opt => opt.MapFrom(src => src.Site != null ? Governorates.NameOf(src.Site.GovernorateCode) : string.Empty));

            CreateMap<CreateDeviceSiteRequestDto, DeviceSite>();
            CreateMap<UpdateDeviceSiteRequestDto, DeviceSite>();
        }
    }
}
