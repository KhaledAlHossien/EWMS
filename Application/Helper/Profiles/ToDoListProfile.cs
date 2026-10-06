using Application.DTOs.Request;
using Application.DTOs.Response;
using AutoMapper;
using Domain.Entities;

namespace Application.Helper.Profiles
{
    public class ToDoListProfile : Profile
    {
        public ToDoListProfile()
        {
            CreateMap<ToDoList, ToDoListResponseDto>()
                .ForMember(d => d.OwnerName, o => o.MapFrom(s => s.User != null ? s.User.FullName : string.Empty));

            CreateMap<ToDoListRequestDto, ToDoList>();
        }
    }
}
