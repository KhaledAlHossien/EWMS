using Application.Common;
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
                .ForMember(dest => dest.GovernorateName,
                    opt => opt.MapFrom(src => Governorates.NameOf(src.GovernorateCode)));

            // المحافظة لا تأتي من العميل: يحدّدها المعالج من الإحداثيات
            CreateMap<CreateSiteRequestDto, Site>()
                .ForMember(dest => dest.GovernorateCode, opt => opt.Ignore());
            CreateMap<UpdateSiteRequestDto, Site>()
                .ForMember(dest => dest.GovernorateCode, opt => opt.Ignore());
        }
    }
}
