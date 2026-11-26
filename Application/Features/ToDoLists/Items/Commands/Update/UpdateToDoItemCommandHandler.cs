using Application.DTOs.Response;
using Application.Interfaces;
using MediatR;

namespace Application.Features.ToDoLists.Items.Commands.Update
{
    public class UpdateToDoItemCommandHandler : IRequestHandler<UpdateToDoItemCommand, ToDoListResponseDto>
    {
        private readonly IToDoListService _service;
        private readonly ToDoItemSupport _support;

        public UpdateToDoItemCommandHandler(IToDoListService service, ToDoItemSupport support)
        {
            _service = service;
            _support = support;
        }

        public async Task<ToDoListResponseDto> Handle(UpdateToDoItemCommand request, CancellationToken ct)
        {
            var viewer = await _support.ViewerAsync();
            var item = await _service.GetItemAsync(request.ItemId) ?? throw new KeyNotFoundException("البند غير موجود");
            ToDoItemSupport.EnsureOwner(viewer, item.ToDoList);
            ToDoItemSupport.EnsureNotArchived(item.ToDoList);

            var dto = request.Dto;
            var today = DateTime.Today;
            if (dto.Title != null) item.Title = dto.Title.Trim();
            if (dto.Note != null) item.Note = Clean(dto.Note);
            if (dto.IsImportant is bool important) item.IsImportant = important;
            if (dto.Color != null) item.Color = ToDoRules.CleanColor(dto.Color);

            var dueBefore = item.DueDate;
            if (dto.ClearDueDate) { item.DueDate = null; item.Repeat = null; }
            else if (dto.DueDate != null) item.DueDate = dto.DueDate.Value.Date;
            if (dto.Repeat != null) item.Repeat = ToDoRules.ParseRepeat(dto.Repeat);
            if (item.Repeat != null && item.DueDate == null) throw new InvalidOperationException("البند المتكرر يحتاج موعداً");
            if (item.DueDate != dueBefore) { item.DueSoonNotifiedAt = null; item.OverdueNotifiedAt = null; }

            if (dto.IsDone is bool done && done != item.IsDone)
            {
                if (done && item.Repeat is { } repeat && item.DueDate is { } due)
                {
                    // المتكرر لا يبقى منجزاً: يعود مفتوحاً بموعده التالي
                    item.DueDate = ToDoRules.NextDue(repeat, due, today);
                    item.LastCompletedAt = DateTime.UtcNow;
                    item.DueSoonNotifiedAt = null; item.OverdueNotifiedAt = null;
                }
                else
                {
                    item.IsDone = done;
                    item.DoneAt = done ? DateTime.UtcNow : null;
                    if (done) item.LastCompletedAt = item.DoneAt;
                }
            }
            await _service.SaveChangesAsync();
            return await _support.ResultAsync(item.ToDoListId, viewer);
        }

        private static string Clean(string? text) => (text ?? string.Empty).Trim();
    }
}
