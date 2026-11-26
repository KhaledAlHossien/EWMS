using Application.DTOs.Response;
using Application.Interfaces;
using MediatR;

namespace Application.Features.ToDoLists.Items.Commands.Delete
{
    public class DeleteToDoItemCommandHandler : IRequestHandler<DeleteToDoItemCommand, ToDoListResponseDto>
    {
        private readonly IToDoListService _service;
        private readonly ToDoItemSupport _support;

        public DeleteToDoItemCommandHandler(IToDoListService service, ToDoItemSupport support)
        {
            _service = service;
            _support = support;
        }

        public async Task<ToDoListResponseDto> Handle(DeleteToDoItemCommand request, CancellationToken ct)
        {
            var viewer = await _support.ViewerAsync();
            var item = await _service.GetItemAsync(request.ItemId) ?? throw new KeyNotFoundException("البند غير موجود");
            ToDoItemSupport.EnsureOwner(viewer, item.ToDoList);
            ToDoItemSupport.EnsureNotArchived(item.ToDoList);

            var listId = item.ToDoListId;
            await _service.DeleteItemsAsync([item]);
            return await _support.ResultAsync(listId, viewer);
        }
    }
}
