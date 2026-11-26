using Application.DTOs.Response;
using MediatR;

namespace Application.Features.ToDoLists.Queries.GetAll
{
    // قوائم المهام الشخصية: كل صلاحية على قوائم المستخدم نفسه فقط، والسوبر ادمن على الكل (حدّ ثابت، لا نطاقات)
    /// <summary>قوائمي. السوبر ادمن: كل القوائم، أو قوائم مستخدم معيّن بـ OwnerId</summary>
    public record GetAllToDoListsQuery(int? OwnerId) : IRequest<List<ToDoListResponseDto>>;
}
