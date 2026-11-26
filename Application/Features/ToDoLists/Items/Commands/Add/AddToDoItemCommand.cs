using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.ToDoLists.Items.Commands.Add
{
    // مفكرتي (قرار المستخدم 2026-10-08): بنود القوائم الشخصية وما حولها. كلها على قوائمي فقط (والسوبر ادمن).
    // كل عملية على البنود تُرجع القائمة بعد التحديث (البنود مرتبة والأعداد) ليستبدلها العميل كما هي.
    public record AddToDoItemCommand(int ListId, AddToDoItemRequestDto Dto) : IRequest<ToDoListResponseDto>;
}
