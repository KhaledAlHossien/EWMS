using Application.DTOs.Response;
using AutoMapper;
using Domain.Entities;

namespace Application.Helper.Profiles
{
    public class NotificationProfile : Profile
    {
        public NotificationProfile()
        {
            CreateMap<Notification, NotificationResponseDto>()
                .ForMember(d => d.Type, o => o.MapFrom(s => s.Type.ToString()));
        }
    }
}
