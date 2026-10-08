using Application.DTOs.Request;
using Application.DTOs.Response;
using AutoMapper;
using Application.Features.ToDoLists;
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
                .ForMember(d => d.ItemsOverdue, o => o.MapFrom(s => s.Items.Count(i => !i.IsDone && i.DueDate != null && i.DueDate.Value.Date < DateTime.Today)))
                .ForMember(d => d.NextDueDate, o => o.MapFrom(s => s.Items.Where(i => !i.IsDone && i.DueDate != null).Min(i => i.DueDate)))
                .ForMember(d => d.Items, o => o.MapFrom(s => s.Items.OrderBy(i => i.SortOrder).ThenBy(i => i.Id)));

            // المهمة المرتبطة تُملأ في المعالج (تتطلب صلاحية عرض المهمة)
            CreateMap<ToDoItem, ToDoItemResponseDto>()
                .ForMember(d => d.IsOverdue, o => o.MapFrom(s => !s.IsDone && s.DueDate != null && s.DueDate.Value.Date < DateTime.Today))
                .ForMember(d => d.Repeat, o => o.MapFrom(s => s.Repeat != null ? s.Repeat.ToString() : null))
                .ForMember(d => d.RepeatAr, o => o.MapFrom(s => s.Repeat != null ? ToDoRules.RepeatAr(s.Repeat.Value) : null))
                .ForMember(d => d.LinkedTask, o => o.Ignore());

            CreateMap<ToDoItem, ToDoTodayItemDto>()
                .IncludeBase<ToDoItem, ToDoItemResponseDto>()
                .ForMember(d => d.ListName, o => o.Ignore())
                .ForMember(d => d.ListColor, o => o.Ignore())
                .ForMember(d => d.ListIcon, o => o.Ignore());

            CreateMap<ToDoListRequestDto, ToDoList>();
        }
    }
}
