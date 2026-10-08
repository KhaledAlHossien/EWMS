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
                .ForMember(d => d.OwnerName, o => o.MapFrom(s => s.User != null ? s.User.FullName : string.Empty))
                .ForMember(d => d.ItemsTotal, o => o.MapFrom(s => s.Items.Count))
                .ForMember(d => d.ItemsDone, o => o.MapFrom(s => s.Items.Count(i => i.IsDone)))
                .ForMember(d => d.Items, o => o.MapFrom(s => s.Items.OrderBy(i => i.SortOrder).ThenBy(i => i.Id)));

            CreateMap<ToDoItem, ToDoItemResponseDto>();

            CreateMap<ToDoListRequestDto, ToDoList>();
        }
    }
}
