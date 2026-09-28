using Application.DTOs.Request;
using Application.DTOs.Response;
using AutoMapper;
using Domain.Entities;

namespace Application.Helper.Profiles
{
    public class DeviceProfile : Profile
    {
        public DeviceProfile()
        {
            CreateMap<Device, DeviceResponseDto>();

            CreateMap<CreateDeviceRequestDto, Device>();
            CreateMap<UpdateDeviceRequestDto, Device>();
        }
    }
}
