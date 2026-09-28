using Application.DTOs.Request;
using Application.DTOs.Response;
using AutoMapper;
using Domain.Entities;

namespace Application.Helper.Profiles
{
    public class RegionProfile : Profile
    {
        public RegionProfile()
        {
            CreateMap<Region, RegionResponseDto>();

            CreateMap<CreateRegionRequestDto, Region>();
            CreateMap<UpdateRegionRequestDto, Region>();
        }
    }
}
