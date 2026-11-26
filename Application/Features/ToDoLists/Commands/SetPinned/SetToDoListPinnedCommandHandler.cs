using Application.DTOs.Response;
using Application.Features.ToDoLists.Items;
using Application.Interfaces;
using MediatR;

namespace Application.Features.ToDoLists.Commands.SetPinned
{
    public class SetToDoListPinnedCommandHandler : IRequestHandler<SetToDoListPinnedCommand, ToDoListResponseDto>
    {
        private readonly IToDoListService _service;
        private readonly ToDoItemSupport _support;

        public SetToDoListPinnedCommandHandler(IToDoListService service, ToDoItemSupport support)
        {
            _service = service;
            _support = support;
        }

        public async Task<ToDoListResponseDto> Handle(SetToDoListPinnedCommand request, CancellationToken ct)
        {
            var (viewer, list) = await _support.LoadListAsync(request.ListId, forEdit: false);
            list.IsPinned = request.Value;
            await _service.SaveChangesAsync();
            return await _support.ResultAsync(list.Id, viewer);
        }
    }
}
