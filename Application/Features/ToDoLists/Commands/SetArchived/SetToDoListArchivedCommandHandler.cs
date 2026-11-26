using Application.DTOs.Response;
using Application.Features.ToDoLists.Items;
using Application.Interfaces;
using MediatR;

namespace Application.Features.ToDoLists.Commands.SetArchived
{
    public class SetToDoListArchivedCommandHandler : IRequestHandler<SetToDoListArchivedCommand, ToDoListResponseDto>
    {
        private readonly IToDoListService _service;
        private readonly ToDoItemSupport _support;

        public SetToDoListArchivedCommandHandler(IToDoListService service, ToDoItemSupport support)
        {
            _service = service;
            _support = support;
        }

        public async Task<ToDoListResponseDto> Handle(SetToDoListArchivedCommand request, CancellationToken ct)
        {
            var (viewer, list) = await _support.LoadListAsync(request.ListId, forEdit: false);
            list.IsArchived = request.Value;
            if (request.Value) list.IsPinned = false;            // المؤرشفة لا تُثبَّت
            await _service.SaveChangesAsync();
            return await _support.ResultAsync(list.Id, viewer);
        }
    }
}
