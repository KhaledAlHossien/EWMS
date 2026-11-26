using Application.DTOs.Response;
using Application.Interfaces;
using FluentValidation;
using MediatR;

namespace Application.Features.ToDoLists.Items.Commands.ClearDone
{
    public class ClearDoneToDoItemsCommandHandler : IRequestHandler<ClearDoneToDoItemsCommand, ToDoListResponseDto>
    {
        private readonly IToDoListService _service;
        private readonly ToDoItemSupport _support;

        public ClearDoneToDoItemsCommandHandler(IToDoListService service, ToDoItemSupport support)
        {
            _service = service;
            _support = support;
        }

        public async Task<ToDoListResponseDto> Handle(ClearDoneToDoItemsCommand request, CancellationToken ct)
        {
            var (viewer, list) = await _support.LoadListAsync(request.ListId);
            var done = list.Items.Where(i => i.IsDone).ToList();
            if (done.Count > 0) await _service.DeleteItemsAsync(done);
            return await _support.ResultAsync(list.Id, viewer);
        }
    }
}
