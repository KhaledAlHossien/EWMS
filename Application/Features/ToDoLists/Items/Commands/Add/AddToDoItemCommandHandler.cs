using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities;
using MediatR;

namespace Application.Features.ToDoLists.Items.Commands.Add
{
    public class AddToDoItemCommandHandler : IRequestHandler<AddToDoItemCommand, ToDoListResponseDto>
    {
        private readonly IToDoListService _service;
        private readonly ToDoItemSupport _support;

        public AddToDoItemCommandHandler(IToDoListService service, ToDoItemSupport support)
        {
            _service = service;
            _support = support;
        }

        public async Task<ToDoListResponseDto> Handle(AddToDoItemCommand request, CancellationToken ct)
        {
            var (viewer, list) = await _support.LoadListAsync(request.ListId);
            if (list.Items.Count >= ToDoRules.MaxItemsPerList)
                throw new InvalidOperationException($"الحد الأقصى {ToDoRules.MaxItemsPerList} بنداً للقائمة");

            var dto = request.Dto;
            await _service.AddItemAsync(new ToDoItem
            {
                ToDoListId = list.Id,
                Title = dto.Title.Trim(),
                Note = Clean(dto.Note),
                IsImportant = dto.IsImportant,
                Color = ToDoRules.CleanColor(dto.Color),
                DueDate = dto.DueDate?.Date,
                Repeat = ToDoRules.ParseRepeat(dto.Repeat),
                SortOrder = await _service.FirstSortOrderAsync(list.Id),
                CreatedAt = DateTime.UtcNow
            });
            return await _support.ResultAsync(list.Id, viewer);
        }

        private static string Clean(string? text) => (text ?? string.Empty).Trim();
    }
}
