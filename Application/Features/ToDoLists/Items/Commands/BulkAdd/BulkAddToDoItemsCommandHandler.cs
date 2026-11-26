using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities;
using FluentValidation;
using MediatR;

namespace Application.Features.ToDoLists.Items.Commands.BulkAdd
{
    public class BulkAddToDoItemsCommandHandler : IRequestHandler<BulkAddToDoItemsCommand, ToDoListResponseDto>
    {
        private readonly IToDoListService _service;
        private readonly ToDoItemSupport _support;

        public BulkAddToDoItemsCommandHandler(IToDoListService service, ToDoItemSupport support)
        {
            _service = service;
            _support = support;
        }

        public async Task<ToDoListResponseDto> Handle(BulkAddToDoItemsCommand request, CancellationToken ct)
        {
            var (viewer, list) = await _support.LoadListAsync(request.ListId);
            var titles = request.Titles.Select(Clean).Where(t => t.Length > 0).ToList();
            if (list.Items.Count + titles.Count > ToDoRules.MaxItemsPerList)
                throw new InvalidOperationException($"لا تتسع القائمة: الحد الأقصى {ToDoRules.MaxItemsPerList} بنداً وفيها {list.Items.Count}");

            var order = await _service.FirstSortOrderAsync(list.Id) - titles.Count + 1;
            var now = DateTime.UtcNow;
            await _service.AddItemsAsync(titles.Select((t, i) => new ToDoItem
            {
                ToDoListId = list.Id, Title = t, SortOrder = order + i, CreatedAt = now
            }));
            return await _support.ResultAsync(list.Id, viewer);
        }

        private static string Clean(string? text) => (text ?? string.Empty).Trim();
    }
}
