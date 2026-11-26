using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities;

namespace Application.Features.ToDoLists.Items
{
    /// <summary>الدوال المشتركة بين عمليات هذه الميزة</summary>
    public class ToDoItemSupport
    {
        private readonly IToDoListService _service;
        private readonly IUserService _userService;
        private readonly IUserPermissionService _permissions;
        private readonly IMapper _mapper;

        public ToDoItemSupport(IToDoListService service, IUserService userService, IUserPermissionService permissions, IMapper mapper)
        {
            _service = service;
            _userService = userService;
            _permissions = permissions;
            _mapper = mapper;
        }

        public Task<Viewer> ViewerAsync() => Viewer.CurrentAsync(_userService, _permissions);

        public static void EnsureOwner(Viewer viewer, ToDoList list)
        {
            if (!viewer.IsSuperAdmin && list.UserId != viewer.Id)
                throw new UnauthorizedAccessException("لا يمكنك تعديل قائمة غيرك");
        }

        public static void EnsureNotArchived(ToDoList list)
        {
            if (list.IsArchived) throw new InvalidOperationException("القائمة مؤرشفة — أعدها من الأرشيف قبل تعديل بنودها");
        }

        public async Task<(Viewer Viewer, ToDoList List)> LoadListAsync(int listId, bool forEdit = true)
        {
            var viewer = await ViewerAsync();
            var list = await _service.GetByIdAsync(listId) ?? throw new KeyNotFoundException("القائمة غير موجودة");
            EnsureOwner(viewer, list);
            if (forEdit) EnsureNotArchived(list);
            return (viewer, list);
        }

        /// <summary>القائمة بعد التحديث (تُعاد قراءتها من القاعدة) مع المهام المرتبطة</summary>
        public async Task<ToDoListResponseDto> ResultAsync(int listId, Viewer viewer)
        {
            var entity = await _service.GetByIdAsync(listId) ?? throw new KeyNotFoundException("القائمة غير موجودة");
            var dto = _mapper.Map<ToDoListResponseDto>(entity);
            ToDoRules.FillLinks(entity, dto, viewer);
            return dto;
        }
    }
}
