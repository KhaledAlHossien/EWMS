using Application.Common;
using Application.Interfaces;
using Domain.Entities;

namespace Application.Features.ToDoLists
{
    /// <summary>الدوال المشتركة بين عمليات هذه الميزة</summary>
    public class ToDoListSupport
    {
        private readonly IToDoListService _service;

        public ToDoListSupport(IToDoListService service)
        {
            _service = service;
        }

        // القائمة لصاحبها فقط (والسوبر ادمن)، ويُرفض غيره بـ 403
        public async Task<ToDoList> LoadOwnedAsync(Viewer viewer, int id, string deniedMessage)
        {
            var list = await _service.GetByIdAsync(id) ?? throw new KeyNotFoundException("القائمة غير موجودة");

            if (!viewer.IsSuperAdmin && list.UserId != viewer.Id)
                throw new UnauthorizedAccessException(deniedMessage);

            return list;
        }

        public static void Trim(ToDoList l)
        {
            l.Name = l.Name.Trim();
            l.Description = l.Description.Trim();
            l.Color = l.Color.Trim().ToLowerInvariant();
            l.Icon = l.Icon.Trim();
        }
    }
}
