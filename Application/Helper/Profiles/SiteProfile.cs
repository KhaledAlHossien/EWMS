using Application.DTOs.Request;
using Application.DTOs.Response;
using AutoMapper;
using Domain.Entities;

namespace Application.Helper.Profiles
{
    public class SiteProfile : Profile
    {
        public SiteProfile()
        {
            CreateMap<Site, SiteResponseDto>()
                .ForMember(dest => dest.RegionName,
                    opt => opt.MapFrom(src => src.Region != null ? src.Region.Name : string.Empty));

            CreateMap<CreateSiteRequestDto, Site>();
            CreateMap<UpdateSiteRequestDto, Site>();
        }
    }
}
